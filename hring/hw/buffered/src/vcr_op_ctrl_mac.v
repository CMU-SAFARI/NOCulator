// $Id: vcr_op_ctrl_mac.v 2063 2010-05-31 06:55:02Z dub $

/*
Copyright (c) 2007-2009, Trustees of The Leland Stanford Junior University
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

Redistributions of source code must retain the above copyright notice, this list
of conditions and the following disclaimer.
Redistributions in binary form must reproduce the above copyright notice, this 
list of conditions and the following disclaimer in the documentation and/or 
other materials provided with the distribution.
Neither the name of the Stanford University nor the names of its contributors 
may be used to endorse or promote products derived from this software without 
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR 
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; 
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON 
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS 
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/



// output port controller (tracks state of buffers in downstream router)
module vcr_op_ctrl_mac
  (clk, reset, flow_ctrl_in, vc_gnt_ovc, vc_gnt_ovc_ip, vc_gnt_ovc_ivc, 
   sw_alloc_in_ip, sw_alloc_out_ip, int_flit_ctrl_in, flit_data_in, 
   flit_ctrl_out, flit_data_out, elig_ovc, error);
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
   
   // flit buffer entries per VC
   parameter num_flit_buffers = 8;
   
   // maximum number of packets that can be in a given VC buffer simultaneously
   parameter num_header_buffers = 4;
   
   // number of traffic classes (e.g. request, reply)
   parameter num_message_classes = 2;
   
   // number of resource classes (e.g. minimal, adaptive)
   parameter num_resource_classes = 2;
   
   // total number of packet classes
   localparam num_packet_classes = num_message_classes * num_resource_classes;
   
   // number of VCs available for each class
   parameter num_vcs_per_class = 1;
   
   // number of VCs
   localparam num_vcs = num_packet_classes * num_vcs_per_class;
   
   // width required to select individual VC
   localparam vc_idx_width = clogb(num_vcs);
   
   // number of routers in each dimension
   parameter num_routers_per_dim = 4;
   
   // number of dimensions in network
   parameter num_dimensions = 2;
   
   // number of nodes per router (a.k.a. consentration factor)
   parameter num_nodes_per_router = 1;
   
   // connectivity within each dimension
   parameter connectivity = `CONNECTIVITY_LINE;
   
   // number of adjacent routers in each dimension
   localparam num_neighbors_per_dim
     = ((connectivity == `CONNECTIVITY_LINE) ||
	(connectivity == `CONNECTIVITY_RING)) ?
       2 :
       (connectivity == `CONNECTIVITY_FULL) ?
       (num_routers_per_dim - 1) :
       -1;
   
   // number of input and output ports on router
   localparam num_ports
     = num_dimensions * num_neighbors_per_dim + num_nodes_per_router;
   
   // select packet format
   parameter packet_format = `PACKET_FORMAT_EXPLICIT_LENGTH;
   
   // width of flit control signals
   localparam flit_ctrl_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (1 + vc_idx_width + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + vc_idx_width + 1) : 
       -1;
   
   // width of flit payload data
   parameter flit_data_width = 64;
   
   // width of flow control signals
   localparam flow_ctrl_width = 1 + vc_idx_width;
   
   // configure error checking logic
   parameter error_capture_mode = `ERROR_CAPTURE_MODE_NO_HOLD;
   
   // select whether VCs must have credits available in order to be considered 
   // for VC allocation
   parameter vc_alloc_requires_credit = 0;
   
   // select whether to set a packet's outgoing VC ID at the input or output 
   // controller
   parameter track_vcs_at_output = 0;
   
   // select method for credit signaling from output to input controller
   parameter int_flow_ctrl_type = `INT_FLOW_CTRL_TYPE_PUSH;
   
   // number of bits to be used for credit level reporting
   // (note: must be less than or equal to cred_count_width as given below)
   // (note: this parameter is only used for INT_FLOW_CTRL_TYPE_LEVEL)
   parameter cred_level_width = 2;
   
   // width required for internal flit control signalling
   localparam int_flit_ctrl_width = 1 + vc_idx_width + 1 + 1;
   
   // width required for internal flow control signalling
   localparam int_flow_ctrl_width
     = (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_LEVEL) ?
       cred_level_width :
       (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_PUSH) ?
       1 :
       -1;
   
   // select implementation variant for switch allocator
   parameter sw_alloc_type = `SW_ALLOC_TYPE_SEP_IF;
   
   // select which arbiter type to use for switch allocator
   parameter sw_alloc_arbiter_type = `ARBITER_TYPE_ROUND_ROBIN;
   
   // select speculation type for switch allocator
   parameter sw_alloc_spec_type = `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS;
   
   // number of bits requires for request signals
   localparam sw_alloc_req_width
     = (sw_alloc_spec_type == `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : (1 + 1);
   
   // number of bits required for grant signals
   localparam sw_alloc_gnt_width = sw_alloc_req_width;
   
   // width of incoming allocator control signals
   localparam sw_alloc_in_width
     = (sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF) ?
       sw_alloc_req_width :
       (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF) ? 
       (sw_alloc_req_width + sw_alloc_gnt_width) : 
       ((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
	(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT)) ? 
       sw_alloc_gnt_width : 
       -1;
   
   // width of outgoing allocator control signals
   localparam sw_alloc_out_width
     = (sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF) ?
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width + 
	((sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : 0)) : 
       (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF) ? 
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width) : 
       ((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
	(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT)) ? 
       num_vcs*int_flow_ctrl_width : 
       -1;
   
   // ID of current input port
   parameter port_id = 0;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // incoming flow control signals
   input [0:flow_ctrl_width-1] flow_ctrl_in;
   
   // output VC was granted to an input VC
   input [0:num_vcs-1] vc_gnt_ovc;
   
   // input port that each output VC was granted to
   input [0:num_vcs*num_ports-1] vc_gnt_ovc_ip;
   
   // input VC that each output VC was granted to
   input [0:num_vcs*num_vcs-1] vc_gnt_ovc_ivc;
   
   // incoming allocator control signals
   input [0:num_ports*sw_alloc_in_width-1] sw_alloc_in_ip;
   
   // outgoing allocator control signals
   output [0:num_ports*sw_alloc_out_width-1] sw_alloc_out_ip;
   wire [0:num_ports*sw_alloc_out_width-1] sw_alloc_out_ip;
   
   // incoming flit control signals
   input [0:int_flit_ctrl_width-1] int_flit_ctrl_in;
   
   // incoming flit data
   input [0:flit_data_width-1] flit_data_in;
   
   // outgoing flit control signals
   output [0:flit_ctrl_width-1] flit_ctrl_out;
   wire [0:flit_ctrl_width-1] flit_ctrl_out;
   
   // outgoing flit data
   output [0:flit_data_width-1] flit_data_out;
   wire [0:flit_data_width-1] flit_data_out;
   
   // output VC is eligible for allocation (i.e., not currently allocated)
   output [0:num_vcs-1] elig_ovc;
   wire [0:num_vcs-1] 	      elig_ovc;
   
   // internal error condition detected
   output error;
   wire 		      error;
   
   
   //---------------------------------------------------------------------------
   // pack / unpack switch allocator control signals
   //---------------------------------------------------------------------------
   
   // non-speculative switch allocation requests to output-side arbitration 
   // stage / wavefront block
   wire [0:num_ports-1]       sw_oreq_nonspec_ip;
   
   // speculative switch allocation requests to output-side arbitration stage or
   // wavefront block
   wire [0:num_ports-1]       sw_oreq_spec_ip;
   
   // non-speculative switch allocation grants from output-side arbitration 
   // stage / wavefront block
   wire [0:num_ports-1]       sw_ognt_nonspec_ip;
   
   // speculative switch allocation grants from output-side arbitration stage or
   // wavefront block
   wire [0:num_ports-1]       sw_ognt_spec_ip;
   
   // gobal non-speculative switch allocation grants
   wire [0:num_ports-1]       sw_gnt_nonspec_ip;
   
   // global speculative switch allocation grants
   wire [0:num_ports-1]       sw_gnt_spec_ip;
   
   // internal flow control signalling from output controller to input 
   // controllers
   wire [0:num_vcs*int_flow_ctrl_width-1] int_flow_ctrl_ovc;
   
   // global grant signal
   wire [0:num_ports-1] 		  sw_gnt_ip;
   
   genvar 				  ip;
   
   generate
      
      for(ip = 0; ip < num_ports; ip = ip + 1)
	begin:ips
	   
	   wire [0:sw_alloc_in_width-1] sw_alloc_in;
	   assign sw_alloc_in = sw_alloc_in_ip[ip*sw_alloc_in_width:
					       (ip+1)*sw_alloc_in_width-1];
	   
	   wire [0:sw_alloc_out_width-1] sw_alloc_out;
	   
	   if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF)
	     begin
		
		assign sw_oreq_nonspec_ip[ip] = sw_alloc_in[0];
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  assign sw_oreq_spec_ip[ip] = sw_alloc_in[1];
		else
		  assign sw_oreq_spec_ip[ip] = 1'b0;
		
		assign sw_gnt_nonspec_ip[ip] = sw_ognt_nonspec_ip[ip];
		assign sw_gnt_spec_ip[ip] = sw_ognt_spec_ip[ip];
		
		assign sw_alloc_out[0] = sw_ognt_nonspec_ip[ip];
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  assign sw_alloc_out[1] = sw_ognt_spec_ip[ip];
		
		assign sw_alloc_out[sw_alloc_gnt_width:
				    sw_alloc_gnt_width+
				    num_vcs*int_flow_ctrl_width-1]
			 = int_flow_ctrl_ovc;
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  assign sw_alloc_out[sw_alloc_gnt_width+
				      num_vcs*int_flow_ctrl_width]
			   = sw_gnt_ip[ip];
		
	     end
	   else if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF)
	     begin
		
		assign sw_oreq_nonspec_ip[ip] = sw_alloc_in[0];
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  assign sw_oreq_spec_ip[ip] = sw_alloc_in[1];
		else
		  assign sw_oreq_spec_ip[ip] = 1'b0;
		
		assign sw_gnt_nonspec_ip[ip] = sw_alloc_in[sw_alloc_req_width];
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  assign sw_gnt_spec_ip[ip] = sw_alloc_in[sw_alloc_req_width+1];
		else
		  assign sw_gnt_spec_ip[ip] = 1'b0;
		
		assign sw_alloc_out[0] = sw_ognt_nonspec_ip[ip];
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  assign sw_alloc_out[1] = sw_ognt_spec_ip[ip];
		
		assign sw_alloc_out[sw_alloc_gnt_width:
				    sw_alloc_gnt_width+
				    num_vcs*int_flow_ctrl_width-1]
			 = int_flow_ctrl_ovc;
		
	     end
	   else if((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) &&
		   (sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT))
	     begin
		
		assign sw_oreq_nonspec_ip[ip] = 1'b0;
		assign sw_oreq_spec_ip[ip] = 1'b0;
		
		assign sw_gnt_nonspec_ip[ip] = sw_alloc_in[0];
		
		if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  assign sw_gnt_spec_ip[ip] = sw_alloc_in[1];
		else
		  assign sw_gnt_spec_ip[ip] = 1'b0;
		
		assign sw_alloc_out[0:num_vcs*int_flow_ctrl_width-1]
			 = int_flow_ctrl_ovc;
		
	     end
	   
	   assign sw_alloc_out_ip[ip*sw_alloc_out_width:
				  (ip+1)*sw_alloc_out_width-1]
		    = sw_alloc_out;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // input staging
   //---------------------------------------------------------------------------
   
   wire [0:flow_ctrl_width-1] 		  flow_ctrl_s, flow_ctrl_q;
   assign flow_ctrl_s = flow_ctrl_in;
   
   wire 				  cred_valid_s, cred_valid_q;
   assign cred_valid_s = flow_ctrl_s[0];
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cred_validq
     (.clk(clk),
      .reset(reset),
      .d(cred_valid_s),
      .q(cred_valid_q));
   
   assign flow_ctrl_q[0] = cred_valid_q;

   generate
      
      if(flow_ctrl_width > 1)
	begin
	   
	   c_dff
	     #(.width(flow_ctrl_width-1),
	       .reset_type(reset_type))
	   flow_ctrlq
	     (.clk(clk),
	      .reset(1'b0),
	      .d(flow_ctrl_s[1:flow_ctrl_width-1]),
	      .q(flow_ctrl_q[1:flow_ctrl_width-1]));
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // switch allocation
   //---------------------------------------------------------------------------
   
   generate
      
      if((sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF) ||
	 (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF))
	begin
	   
	   // any non-speculative requests for this port?
	   wire sw_oreq_nonspec;
	   assign sw_oreq_nonspec = |sw_oreq_nonspec_ip;
	   
	   // any non-speculative grants for this port?
	   wire sw_gnt_nonspec;
	   assign sw_gnt_nonspec = |sw_gnt_nonspec_ip;
	   
	   // update arbiter priorities
	   wire update_arb_nonspec;
	   
	   case(sw_alloc_type)
	     
	     `SW_ALLOC_TYPE_SEP_IF:
	       begin
		  
		  //------------------------------------------------------------
		  // For the input-first separable allocator, if any requests 
		  // reach the output stage, they must already have won input-
		  // side arbitration; consequently, one of them will result in 
		  // an global grant, and we can thus update arbiter priorities
		  // whenever we see a request.
		  //------------------------------------------------------------
		  
		  assign update_arb_nonspec = sw_oreq_nonspec;
		  
	       end
	     
	     `SW_ALLOC_TYPE_SEP_OF:
	       begin
		  
		  //------------------------------------------------------------
		  // For separable output-first allocation, any grants generated
		  // by the output stage may subsequently be dropped at the 
		  // input stage; consequently, we must only update priorities 
		  // once we know that our grant also won input-side 
		  // arbitration.
		  //------------------------------------------------------------
		  
		  assign update_arb_nonspec = sw_gnt_nonspec;
		  
	       end
	     
	   endcase
	   
	   
	   //-------------------------------------------------------------------
	   // The actual arbitration is identical for the input- and output-
	   // first cases.
	   //-------------------------------------------------------------------
	   
	   wire [0:num_ports-1] req_nonspec_ip;
	   assign req_nonspec_ip = sw_oreq_nonspec_ip;
	   
	   wire [0:num_ports-1] gnt_nonspec_ip;
	   c_arbiter
	     #(.num_ports(num_ports),
	       .arbiter_type(sw_alloc_arbiter_type),
	       .reset_type(reset_type))
	   gnt_nonspec_ip_arb
	     (.clk(clk),
	      .reset(reset),
	      .update(update_arb_nonspec),
	      .req(req_nonspec_ip),
	      .gnt(gnt_nonspec_ip));
	   
	   assign sw_ognt_nonspec_ip = gnt_nonspec_ip;
	   
	   
	   //-------------------------------------------------------------------
	   // speculative switch allocation support
	   //-------------------------------------------------------------------
	   
	   if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
	     begin
		
		// arbitrate between speculative requests
		wire [0:num_ports-1] req_spec_ip;
		wire [0:num_ports-1] gnt_spec_ip;
		wire 		     update_arb_spec;

		case(sw_alloc_type)
		  
		  `SW_ALLOC_TYPE_SEP_IF:
		    begin
		       
		       //-------------------------------------------------------
		       // For input-first allocation, any non-speculative 
		       // requests arriving at the output stage must already 
		       // have won input-side arbitration, and thus one of them 
		       // will always be granted; therefore, we can suppress 
		       // speculative grants and the arbiter state update in 
		       // this case whenever any non-speculative requests are 
		       // present. 
		       // Likewise, the global grants can be formed by 
		       // combining the non-speculative and speculative output-
		       // side grants.
		       //-------------------------------------------------------
		       
		       assign update_arb_spec
			 = ~sw_oreq_nonspec & |sw_oreq_spec_ip;
		       
		       assign sw_ognt_spec_ip
			 = {num_ports{~sw_oreq_nonspec}} & gnt_spec_ip;
		       
		       assign sw_gnt_ip = sw_ognt_nonspec_ip | sw_ognt_spec_ip;
		       
		    end
		  
		  `SW_ALLOC_TYPE_SEP_OF:
		    begin
		       case(sw_alloc_spec_type)
			 
			 `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS,
			 `SW_ALLOC_SPEC_TYPE_REQS_MASK_REQS:
			   begin
			      
			      //------------------------------------------------
			      // For output-first allocation, on the other hand,
			      // any given output-side non-speculative grant may
			      // still fail to win input-side arbitration; 
			      // however, if we suppress speculative requests 
			      // based on non-speculative grants, we can still 
			      // use the same approach as in the input-first 
			      // case. 
			      //------------------------------------------------
			      
			      assign update_arb_spec
				= ~sw_oreq_nonspec & |sw_oreq_spec_ip;
			      
			      assign sw_ognt_spec_ip
				= {num_ports{~sw_oreq_nonspec}} & gnt_spec_ip;
			      
			   end
			 
			 `SW_ALLOC_SPEC_TYPE_GNTS_MASK_GNTS,
			 `SW_ALLOC_SPEC_TYPE_GNTS_MASK_REQS:
			   begin
			      
			      //------------------------------------------------
			      // For output-first allocation with conflict 
			      // detection based on non-speculative grants, 
			      // however, we must check if non-speculative 
			      // grants also won input-side arbitration before 
			      // suppressing speculative grants and inhibiting 
			      // arbiter state updates.
			      //------------------------------------------------
			      
			      assign update_arb_spec
				= ~sw_gnt_nonspec & |sw_gnt_spec_ip;
			      
			      assign sw_ognt_spec_ip
				= {num_ports{~sw_gnt_nonspec}} & gnt_spec_ip;
			      
			   end
			 
		       endcase
		       
		       //-------------------------------------------------------
		       // The global grants in this case come from the input-
		       // side arbitration stage; speculation-related masking is
		       // performed at the input side.
		       //-------------------------------------------------------
		       
		       assign sw_gnt_ip = sw_gnt_nonspec_ip | sw_gnt_spec_ip;
		       
		    end
		  
		endcase
		
		assign req_spec_ip = sw_oreq_spec_ip;
		       
		c_arbiter
		  #(.num_ports(num_ports),
		    .arbiter_type(sw_alloc_arbiter_type),
		    .reset_type(reset_type))
		gnt_spec_ip_arb
		  (.clk(clk),
		   .reset(reset),
		   .update(update_arb_spec),
		   .req(req_spec_ip),
		   .gnt(gnt_spec_ip));
		
	     end
	   else
	     begin
		
		//--------------------------------------------------------------
		// If speculation is disabled, all speculation-related control
		// signals are just tied to zero.
		//--------------------------------------------------------------
		
		assign sw_ognt_spec_ip = {num_ports{1'b0}};
		
		case(sw_alloc_type)
		  
		  `SW_ALLOC_TYPE_SEP_IF:
		    begin
		       
		       //-------------------------------------------------------
		       // In the separable input-first case, every output-side
		       // request already succeeded in input-side arbitration, 
		       // and thus the global grants are just the output-side 
		       // grants; i.e., we do not need to consider the incoming 
		       // sw_gnt_*_ip signals.
		       //-------------------------------------------------------
		       
		       assign sw_gnt_ip = sw_ognt_nonspec_ip;
		       
		    end
		  
		  `SW_ALLOC_TYPE_SEP_OF:
		    begin
		       
		       //-------------------------------------------------------
		       // In the output-first case, on the other hand, the 
		       // global grants are generated by the input-side
		       // arbitration stage.
		       //-------------------------------------------------------
		       
		       assign sw_gnt_ip = sw_gnt_nonspec_ip;
		       
		    end
		  
		endcase
		
	     end
	   
	end
      else if((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
	      (sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT))
	begin
	   
	   //-------------------------------------------------------------------
	   // The wavefront allocator does not require any output-side logic. 
	   // Global grants are generated externally in the wavefront block.
	   //-------------------------------------------------------------------
	   
	   assign sw_ognt_nonspec_ip = {num_ports{1'b0}};
	   assign sw_ognt_spec_ip = {num_ports{1'b0}};
	   
	   if(sw_alloc_spec_type == `SW_ALLOC_SPEC_TYPE_NONE)
	     assign sw_gnt_ip = sw_gnt_nonspec_ip;
	   else
	     assign sw_gnt_ip = sw_gnt_nonspec_ip | sw_gnt_spec_ip;
	   
	end
      
   endgenerate
   
   wire sw_gnt_s;
   assign sw_gnt_s = |sw_gnt_ip;
   
   wire sw_gnt_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   sw_gntq
     (.clk(clk),
      .reset(reset),
      .d(sw_gnt_s),
      .q(sw_gnt_q));
   
   wire [0:num_ports-1] 		  sw_gnt_ip_s;
   assign sw_gnt_ip_s = sw_gnt_ip;
   
   wire [0:num_ports-1] 		  sw_gnt_ip_q;
   c_dff
     #(.width(num_ports),
       .reset_type(reset_type))
   sw_gnt_ipq
     (.clk(clk),
      .reset(reset),
      .d(sw_gnt_ip_s),
      .q(sw_gnt_ip_q));
   
   
   //---------------------------------------------------------------------------
   // output VC control logic
   //---------------------------------------------------------------------------

   wire 				  flit_valid_in;
   assign flit_valid_in = int_flit_ctrl_in[0] & sw_gnt_q;
   
   wire 				  flit_tail_in;
   assign flit_tail_in = int_flit_ctrl_in[1+vc_idx_width+1];
   
   wire 				  cred_valid;
   assign cred_valid = flow_ctrl_q[0];
   
   wire [0:num_vcs-1] 			  match_ovc;
   
   wire 				  error_unmatched;
   assign error_unmatched = flit_valid_in & ~|match_ovc;
   
   wire [0:num_vcs-1] 			  next_cred_ovc;
   wire [0:num_vcs*4-1] 		  ovcc_errors_ovc;
   
   generate
      
      genvar 				  ovc;
      
      for(ovc = 0; ovc < num_vcs; ovc = ovc + 1)
	begin:ovcs
	   
	   wire vc_gnt;
	   assign vc_gnt = vc_gnt_ovc[ovc];
	   
	   wire [0:num_ports-1] vc_gnt_ip;
	   assign vc_gnt_ip = vc_gnt_ovc_ip[ovc*num_ports:(ovc+1)*num_ports-1];
	   
	   wire [0:num_vcs-1] 	vc_gnt_ivc;
	   assign vc_gnt_ivc = vc_gnt_ovc_ivc[ovc*num_vcs:(ovc+1)*num_vcs-1];

	   wire 		match;
	   wire 		elig;
	   wire 		empty;
	   wire [0:int_flow_ctrl_width-1] int_flow_ctrl;
	   wire [0:3] 			  ovcc_errors;
	   vcr_ovc_ctrl
	     #(.num_flit_buffers(num_flit_buffers),
	       .num_header_buffers(num_header_buffers),
	       .num_message_classes(num_message_classes),
	       .num_resource_classes(num_resource_classes),
	       .num_vcs_per_class(num_vcs_per_class),
	       .num_routers_per_dim(num_routers_per_dim),
	       .num_dimensions(num_dimensions),
	       .num_nodes_per_router(num_nodes_per_router),
	       .connectivity(connectivity),
	       .packet_format(packet_format),
	       .vc_id(ovc),
	       .vc_alloc_requires_credit(vc_alloc_requires_credit),
	       .track_vcs_at_output(track_vcs_at_output),
	       .int_flow_ctrl_type(int_flow_ctrl_type),
	       .cred_level_width(cred_level_width),
	       .reset_type(reset_type))
	   ovcc
	     (.clk(clk),
	      .reset(reset),
	      .flow_ctrl(flow_ctrl_q),
	      .vc_gnt(vc_gnt),
	      .vc_gnt_ip(vc_gnt_ip),
	      .vc_gnt_ivc(vc_gnt_ivc),
	      .sw_gnt_q(sw_gnt_q),
	      .sw_gnt_ip_q(sw_gnt_ip_q),
	      .int_flit_ctrl(int_flit_ctrl_in),
	      .match(match),
	      .elig(elig),
	      .empty(empty),
	      .int_flow_ctrl(int_flow_ctrl),
	      .errors(ovcc_errors));

	   assign match_ovc[ovc] = match;
	   assign elig_ovc[ovc] = elig;
	   assign int_flow_ctrl_ovc[ovc*int_flow_ctrl_width:
				    (ovc+1)*int_flow_ctrl_width-1]
		    = int_flow_ctrl;
	   assign ovcc_errors_ovc[ovc*4:(ovc+1)*4-1] = ovcc_errors;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // output staging
   //---------------------------------------------------------------------------
   
   wire [0:flit_ctrl_width-1] flit_ctrl_s, flit_ctrl_q;
   
   assign flit_ctrl_s[0] = flit_valid_in;
   
   generate
      
      if(num_vcs > 1)
	begin
	   
	   if(track_vcs_at_output)
	     begin
		
		wire [0:vc_idx_width-1] match_ovc_enc;
		c_encoder
		  #(.num_ports(num_vcs))
		match_ovc_enc_enc
		  (.data_in(match_ovc),
		   .data_out(match_ovc_enc));
		
		assign flit_ctrl_s[1:1+vc_idx_width-1]
			 = sw_gnt_q ? 
			   match_ovc_enc : 
			   flit_ctrl_q[1:1+vc_idx_width-1];
		
	     end
	   else
	     assign flit_ctrl_s[1:1+vc_idx_width-1]
		      = sw_gnt_q ? 
			int_flit_ctrl_in[1:1+vc_idx_width-1] :
			flit_ctrl_q[1:1+vc_idx_width-1];
	   
	end
      
      assign flit_ctrl_s[1+vc_idx_width:flit_ctrl_width-1]
	       = sw_gnt_q ?
		 int_flit_ctrl_in[1+vc_idx_width:flit_ctrl_width-1] :
		 flit_ctrl_q[1+vc_idx_width:flit_ctrl_width-1];
      
      case(packet_format)
	
	`PACKET_FORMAT_HEAD_TAIL,
	`PACKET_FORMAT_EXPLICIT_LENGTH:
	  begin
	     
	     wire flit_valid_s, flit_valid_q;
	     assign flit_valid_s = flit_ctrl_s[0];
	     c_dff
	       #(.width(1),
		 .reset_type(reset_type))
	     flit_validq
	       (.clk(clk),
		.reset(reset),
		.d(flit_valid_s),
		.q(flit_valid_q));
	     
	     assign flit_ctrl_q[0] = flit_valid_q;
	     
	     c_dff
	       #(.width(flit_ctrl_width-1),
		 .offset(1),
		 .reset_type(reset_type))
	     flit_ctrlq
	       (.clk(clk),
		.reset(1'b0),
		.d(flit_ctrl_s[1:flit_ctrl_width-1]),
		.q(flit_ctrl_q[1:flit_ctrl_width-1]));
	     
	  end
	
      endcase
      
   endgenerate
   
   assign flit_ctrl_out = flit_ctrl_q;
   
   wire [0:flit_data_width-1] 		  flit_data_s, flit_data_q;
   assign flit_data_s = sw_gnt_q ? flit_data_in : flit_data_q;
   c_dff
     #(.width(flit_data_width),
       .reset_type(reset_type))
   flit_dataq
     (.clk(clk),
      .reset(1'b0),
      .d(flit_data_s),
      .q(flit_data_q));
   
   assign flit_data_out = flit_data_q;
   
   
   //---------------------------------------------------------------------------
   // error checking
   //---------------------------------------------------------------------------
   
   generate
      
      if(error_capture_mode != `ERROR_CAPTURE_MODE_NONE)
	begin
	   
	   // synopsys translate_off
	   
	   integer i;
	   
	   always @(posedge clk)
	     begin
		
		if(error_unmatched)
		  $display("ERROR: Unmatched flit in module %m.");
		
		for(i = 0; i < num_vcs; i = i + 1)
		  begin
		     
		     if(ovcc_errors_ovc[i*4])
		       $display({"ERROR: Credit tracker underflow in module ",
				 "%m."});
		     
		     if(ovcc_errors_ovc[i*4+1])
		       $display({"ERROR: Credit tracker overflow in module ", 
				 "%m."});
		     
		     if(ovcc_errors_ovc[i*4+2])
		       $display({"ERROR: Head credit tracker underflow in ", 
				 "module %m."});
		     
		     if(ovcc_errors_ovc[i*4+3])
		       $display({"ERROR: Head credit tracker overflow in ", 
				 "module %m."});
		     
		  end
		
	     end
	   // synopsys translate_on
	   
	   wire [0:1+num_vcs*4-1] errors_s, errors_q;
	   assign errors_s = {error_unmatched, ovcc_errors_ovc};
	   c_err_rpt
	     #(.num_errors(1+num_vcs*4),
	       .capture_mode(error_capture_mode),
	       .reset_type(reset_type))
	   chk
	     (.clk(clk),
	      .reset(reset),
	      .errors_in(errors_s),
	      .errors_out(errors_q));
	   
	   assign error = |errors_q;
	   
	end
      else
	assign error = 1'b0;
      
   endgenerate
   
endmodule
