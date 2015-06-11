// $Id$

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



// top-level module for virtual channel router
module vcr_top
  (clk, reset, router_address, flit_ctrl_in_ip, flit_data_in_ip, 
   flow_ctrl_out_ip, flit_ctrl_out_op, flit_data_out_op, flow_ctrl_in_op, 
   error);
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
   
   // flit buffer entries per VC
   parameter num_flit_buffers = 8;
   
   // maximum number of packets that can be in a given VC buffer simultaneously
   parameter num_header_buffers = 4;
   
   // number of message classes (e.g. request, reply)
   parameter num_message_classes = 2;
   
   // number of resource classes (e.g. minimal, adaptive)
   parameter num_resource_classes = 2;
   
   // total number of packet classes
   localparam num_packet_classes = num_message_classes * num_resource_classes;
   
   // number of VCs per class
   parameter num_vcs_per_class = 1;
   
   // number of VCs
   localparam num_vcs = num_packet_classes * num_vcs_per_class;
   
   // width required to select individual VC
   localparam vc_idx_width = clogb(num_vcs);
   
   // number of routers in each dimension
   parameter num_routers_per_dim = 4;
   
   // width required to select individual router in a dimension
   localparam dim_addr_width = clogb(num_routers_per_dim);
   
   // number of dimensions in network
   parameter num_dimensions = 2;
   
   // width required to select individual router in entire network
   localparam router_addr_width = num_dimensions * dim_addr_width;
   
   // number of nodes per router (a.k.a. concentration factor)
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
   
   // width required to select an individual port
   localparam port_idx_width = clogb(num_ports);
   
   // select packet format
   parameter packet_format = `PACKET_FORMAT_EXPLICIT_LENGTH;
   
   // maximum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter max_payload_length = 4;
   
   // minimum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter min_payload_length = 1;
   
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
   
   // enable performance counter
   parameter perf_ctr_enable = 1;
   
   // width of each counter
   parameter perf_ctr_width = 32;
   
   // configure error checking logic
   parameter error_capture_mode = `ERROR_CAPTURE_MODE_NO_HOLD;
   
   // select whether to set a packet's outgoing VC ID at the input or output 
   // controller
   parameter track_vcs_at_output = 0;
   
   // filter out illegal destination ports
   // (the intent is to allow synthesis to optimize away the logic associated 
   // with such turns)
   parameter restrict_turns = 1;
   
   // select routing function type
   parameter routing_type = `ROUTING_TYPE_DOR;
   
   // select order of dimension traversal
   parameter dim_order = `DIM_ORDER_ASCENDING;
   
   // select method for credit signaling from output to input controller
   parameter int_flow_ctrl_type = `INT_FLOW_CTRL_TYPE_PUSH;
   
   // number of bits to be used for credit level reporting
   // (note: must be less than or equal to cred_count_width as given below)
   // (note: this parameter is only used for INT_FLOW_CTRL_TYPE_LEVEL)
   localparam cred_level_width = 2;
   
   // width required for internal flit control signalling
   localparam int_flit_ctrl_width = 1 + vc_idx_width + 1 + 1;
   
   // width required for internal flow control signalling
   localparam int_flow_ctrl_width
     = (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_LEVEL) ?
       cred_level_width :
       (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_PUSH) ?
       1 :
       -1;
   
   // select implementation variant for header FIFO
   parameter header_fifo_type = `FIFO_TYPE_INDEXED;
   
   // select implementation variant for flit buffer register file
   parameter fbf_regfile_type = `REGFILE_TYPE_FF_2D;
   
   // number of entries in flit buffer
   localparam fbf_depth = num_vcs*num_flit_buffers;
   
   // required address size for flit buffer
   localparam fbf_addr_width = clogb(fbf_depth);
   
   // select implementation variant for VC allocator
   parameter vc_alloc_type = `VC_ALLOC_TYPE_SEP_IF;
   
   // select which arbiter type to use for VC allocator
   parameter vc_alloc_arbiter_type = `ARBITER_TYPE_ROUND_ROBIN;
   
   // select whether VCs must have credits available in order to be considered 
   // for VC allocation
   parameter vc_alloc_requires_credit = 0;
   
   // select implementation variant for switch allocator
   parameter sw_alloc_type = `SW_ALLOC_TYPE_SEP_IF;
   
   // select which arbiter type to use for switch allocator
   parameter sw_alloc_arbiter_type = `ARBITER_TYPE_ROUND_ROBIN;
   
   // select speculation type for switch allocator
   parameter sw_alloc_spec_type = `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS;
   
   // number of bits required for request signals
   localparam sw_alloc_req_width
     = (sw_alloc_spec_type == `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : (1 + 1);
   
   // number of bits required for grant signals
   localparam sw_alloc_gnt_width = sw_alloc_req_width;
   
   // width of outgoing allocator control signals for input controller
   localparam sw_alloc_ipc_out_width
     = (sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF) ? 
       sw_alloc_req_width : 
       (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF) ? 
       (sw_alloc_req_width + sw_alloc_gnt_width + 
	((sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : 0)) : 
       ((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
	(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT)) ? 
       sw_alloc_req_width : 
       -1;
   
   // width of incoming allocator control signals for input controller
   localparam sw_alloc_ipc_in_width
     = (sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF) ? 
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width) : 
       (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF) ? 
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width) :
       ((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
	(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT)) ? 
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width) : 
       -1;
   
   // width of incoming allocator control signals for wavefront allocator block
   localparam sw_alloc_wf_in_width = sw_alloc_req_width;
   
   // width of outgoing allocator control signals for wavefront allocator block
   localparam sw_alloc_wf_out_width
     = sw_alloc_gnt_width + 
       ((sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : 0);
   
   // width of incoming allocator control signals for output controller
   localparam sw_alloc_opc_in_width
     = (sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF) ?
       sw_alloc_req_width :
       (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF) ? 
       (sw_alloc_req_width + sw_alloc_gnt_width) : 
       ((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
	(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT)) ? 
       sw_alloc_gnt_width : 
       -1;
   
   // width of outgoing allocator control signals for output controller
   localparam sw_alloc_opc_out_width
     = (sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF) ?
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width + 
	((sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : 0)) : 
       (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF) ? 
       (sw_alloc_gnt_width + num_vcs*int_flow_ctrl_width) : 
       ((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) && 
	(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT)) ? 
       num_vcs*int_flow_ctrl_width : 
       -1;
   
   // select implementation variant for control crossbar
   parameter ctrl_crossbar_type = `CROSSBAR_TYPE_MUX;
   
   // select implementation variant for data crossbar
   parameter data_crossbar_type = `CROSSBAR_TYPE_MUX;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // current router's address
   input [0:router_addr_width-1] router_address;
   
   // control signals for each incoming flit
   input [0:num_ports*flit_ctrl_width-1] flit_ctrl_in_ip;
   
   // flit data for each incoming flit
   input [0:num_ports*flit_data_width-1] flit_data_in_ip;
   
   // outgoing flow control signals
   output [0:num_ports*flow_ctrl_width-1] flow_ctrl_out_ip;
   wire [0:num_ports*flow_ctrl_width-1] flow_ctrl_out_ip;
   
   // control signals for each outgoing flit
   output [0:num_ports*flit_ctrl_width-1] flit_ctrl_out_op;
   wire [0:num_ports*flit_ctrl_width-1] flit_ctrl_out_op;
   
   // flit data for each outgoing flit
   output [0:num_ports*flit_data_width-1] flit_data_out_op;
   wire [0:num_ports*flit_data_width-1] flit_data_out_op;
   
   // incoming flow control signals
   input [0:num_ports*flow_ctrl_width-1] flow_ctrl_in_op;
   
   // internal error condition detected
   output error;
   wire 				error;
   
   
   //---------------------------------------------------------------------------
   // input ports
   //---------------------------------------------------------------------------
   
   wire [0:num_ports*num_vcs*port_idx_width-1] 	    route_port_ip_ivc;
   wire [0:num_ports*num_vcs-1] 		    inc_rc_ip_ivc;
   
   wire [0:num_ports*num_vcs-1] 		    vc_req_ip_ivc;
   wire [0:num_ports*num_vcs-1] 		    vc_gnt_ip_ivc;
   wire [0:num_ports*num_vcs*num_vcs-1] 	    vc_gnt_ip_ivc_ovc;
   
   wire [0:num_ports*num_ports*sw_alloc_opc_out_width-1] sw_alloc_opc_out_ip_op;
   wire [0:num_ports*num_ports*sw_alloc_ipc_out_width-1] sw_alloc_ipc_out_ip_op;
   wire [0:num_ports*num_ports*sw_alloc_wf_in_width-1] 	 sw_alloc_wf_in_ip_op;
   wire [0:num_ports*num_ports*sw_alloc_wf_out_width-1]  sw_alloc_wf_out_ip_op;
   
   wire [0:num_ports*num_ports-1] 			 xbc_ctrl_ip_op;
   wire [0:num_ports*num_ports-1] 			 xbd_ctrl_ip_op;
   
   wire [0:num_ports*int_flit_ctrl_width-1] 		 xbc_data_in_ip;
   wire [0:num_ports*flit_data_width-1] 		 xbd_data_in_ip;
   
   wire [0:num_ports-1] 				 ipc_error_ip;
   
   generate
      
      genvar 						 ip;
      
      for (ip = 0; ip < num_ports; ip = ip + 1)
	begin:ips
	   
	   //-------------------------------------------------------------------
	   // connect switch allocator control signals
	   //-------------------------------------------------------------------
	   
	   wire [0:num_ports*sw_alloc_ipc_out_width-1] sw_alloc_ipc_out_op;
	   
	   wire [0:num_ports*sw_alloc_ipc_in_width-1] sw_alloc_ipc_in_op;
	   
	   genvar op;
	   
	   for(op = 0; op < num_ports; op = op + 1)
	     begin:ops
		
		wire [0:sw_alloc_ipc_out_width-1] sw_alloc_ipc_out;
		assign sw_alloc_ipc_out
		  = sw_alloc_ipc_out_op[op*sw_alloc_ipc_out_width:
					(op+1)*sw_alloc_ipc_out_width-1];
		
		wire [0:sw_alloc_opc_out_width-1] sw_alloc_opc_out;
		assign sw_alloc_opc_out
		  = sw_alloc_opc_out_ip_op[(ip*num_ports+op)*
					   sw_alloc_opc_out_width:
					   (ip*num_ports+op+1)*
					   sw_alloc_opc_out_width-1];
		
		wire [0:sw_alloc_ipc_in_width-1]  sw_alloc_ipc_in;
		
		wire [0:sw_alloc_wf_in_width-1]   sw_alloc_wf_in;
		
		if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF)
		  begin
		     
		     assign sw_alloc_ipc_in[0:sw_alloc_gnt_width-1]
			      = sw_alloc_opc_out[0:sw_alloc_gnt_width-1];
		     assign sw_alloc_ipc_in[sw_alloc_gnt_width:
					    sw_alloc_gnt_width+
					    num_vcs*int_flow_ctrl_width-1]
			      = sw_alloc_opc_out[sw_alloc_gnt_width:
						 sw_alloc_gnt_width+
						 num_vcs*int_flow_ctrl_width-1];
		     
		     assign sw_alloc_wf_in = {sw_alloc_wf_in_width{1'b0}};
		     
		  end
		else if (sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF)
		  begin
		     
		     assign sw_alloc_ipc_in[0:sw_alloc_gnt_width-1]
			      = sw_alloc_opc_out[0:sw_alloc_gnt_width-1];
		     assign sw_alloc_ipc_in[sw_alloc_gnt_width:
					    sw_alloc_gnt_width+
					    num_vcs*int_flow_ctrl_width-1]
			      = sw_alloc_opc_out[sw_alloc_gnt_width:
						 sw_alloc_gnt_width+
						 num_vcs*int_flow_ctrl_width-1];
		     
		     assign sw_alloc_wf_in = {sw_alloc_wf_in_width{1'b0}};
		     
		     if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		       begin
			  
			  assign xbc_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_ipc_out[sw_alloc_req_width+
						      sw_alloc_gnt_width];
			  assign xbd_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_ipc_out[sw_alloc_req_width+
						      sw_alloc_gnt_width];
			  
		       end
		     else
		       begin
			  
			  assign xbc_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_ipc_out[sw_alloc_req_width];
			  assign xbd_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_ipc_out[sw_alloc_req_width];
			  
		       end
		     
		  end
		else if((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) &&
			(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT))
		  begin
		     
		     wire [0:sw_alloc_wf_out_width-1]  sw_alloc_wf_out;
		     assign sw_alloc_wf_out
		       = sw_alloc_wf_out_ip_op[(ip*num_ports+op)*
					       sw_alloc_wf_out_width:
					       (ip*num_ports+op+1)*
					       sw_alloc_wf_out_width-1];
		     
		     assign sw_alloc_ipc_in[0:sw_alloc_gnt_width-1]
			      = sw_alloc_wf_out[0:sw_alloc_gnt_width-1];
		     assign sw_alloc_ipc_in[sw_alloc_gnt_width:
					    sw_alloc_gnt_width+
					    num_vcs*int_flow_ctrl_width-1]
			      = sw_alloc_opc_out[0:
						 num_vcs*int_flow_ctrl_width-1];
		     
		     assign sw_alloc_wf_in[0:sw_alloc_req_width-1]
			      = sw_alloc_ipc_out[0:sw_alloc_req_width-1];
		     
		     if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		       begin
			  
			  assign xbc_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_wf_out[sw_alloc_gnt_width];
			  assign xbd_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_wf_out[sw_alloc_gnt_width];
			  
		       end
		     else
		       begin
			  
			  assign xbc_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_wf_out[0];
			  assign xbd_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_wf_out[0];
			  
		       end
		     
		  end
		
		assign sw_alloc_ipc_in_op[op*sw_alloc_ipc_in_width:
					  (op+1)*sw_alloc_ipc_in_width-1]
			 = sw_alloc_ipc_in;
		
		assign sw_alloc_wf_in_ip_op[(ip*num_ports+op)*
					    sw_alloc_wf_in_width:
					    (ip*num_ports+op+1)*
					    sw_alloc_wf_in_width-1]
			 = sw_alloc_wf_in;
		
	     end // block: ops
	   
	   
	   
	   //-------------------------------------------------------------------
	   // input controller
	   //-------------------------------------------------------------------
	   
	   wire [0:flit_ctrl_width-1] flit_ctrl_in;
	   assign flit_ctrl_in
	     = flit_ctrl_in_ip[ip*flit_ctrl_width:(ip+1)*flit_ctrl_width-1];
	   
	   wire [0:flit_data_width-1] flit_data_in;
	   assign flit_data_in
	     = flit_data_in_ip[ip*flit_data_width:(ip+1)*flit_data_width-1];
	   
	   wire [0:num_vcs-1] 	      vc_gnt_ivc;
	   assign vc_gnt_ivc = vc_gnt_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1];
	   
	   wire [0:num_vcs*num_vcs-1] vc_gnt_ivc_ovc;
	   assign vc_gnt_ivc_ovc
	     = vc_gnt_ip_ivc_ovc[ip*num_vcs*num_vcs:(ip+1)*num_vcs*num_vcs-1];
	   
	   wire [0:num_vcs*port_idx_width-1] 	      route_port_ivc;
	   wire [0:num_vcs-1] 			      inc_rc_ivc;
	   wire [0:num_vcs-1] 			      vc_req_ivc;
	   wire [0:int_flit_ctrl_width-1] 	      int_flit_ctrl_out;
	   wire [0:flit_data_width-1] 		       flit_data_out;
	   wire [0:flow_ctrl_width-1] 		       flow_ctrl_out;
	   wire [0:fbf_addr_width-1] 		       fbf_write_addr;
	   wire 				       fbf_write_enable;
	   wire [0:flit_data_width-1] 		       fbf_write_data;
	   wire [0:fbf_addr_width-1] 		       fbf_read_addr;
	   wire 				       fbf_read_enable;
	   wire [0:flit_data_width-1] 		       fbf_read_data;
	   wire 				       ipc_error;
	   vcr_ip_ctrl_mac
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
	       .max_payload_length(max_payload_length),
	       .min_payload_length(min_payload_length),
	       .flit_data_width(flit_data_width),
	       .track_vcs_at_output(track_vcs_at_output),
	       .restrict_turns(restrict_turns),
	       .routing_type(routing_type),
	       .dim_order(dim_order),
	       .int_flow_ctrl_type(int_flow_ctrl_type),
	       .cred_level_width(cred_level_width),
	       .header_fifo_type(header_fifo_type),
	       .vc_alloc_type(vc_alloc_type),
	       .vc_alloc_requires_credit(vc_alloc_requires_credit),
	       .sw_alloc_type(sw_alloc_type),
	       .sw_alloc_arbiter_type(sw_alloc_arbiter_type),
	       .sw_alloc_spec_type(sw_alloc_spec_type),
	       .perf_ctr_enable(perf_ctr_enable),
	       .perf_ctr_width(perf_ctr_width),
	       .error_capture_mode(error_capture_mode),
	       .port_id(ip),
	       .reset_type(reset_type))
	   ipc
	     (.clk(clk),
	      .reset(reset),
	      .router_address(router_address),
	      .flit_ctrl_in(flit_ctrl_in),
	      .flit_data_in(flit_data_in),
	      .route_port_ivc(route_port_ivc),
	      .inc_rc_ivc(inc_rc_ivc),
	      .vc_req_ivc(vc_req_ivc),
	      .vc_gnt_ivc(vc_gnt_ivc),
	      .vc_gnt_ivc_ovc(vc_gnt_ivc_ovc),
	      .sw_alloc_out_op(sw_alloc_ipc_out_op),
	      .sw_alloc_in_op(sw_alloc_ipc_in_op),
	      .int_flit_ctrl_out(int_flit_ctrl_out),
	      .flit_data_out(flit_data_out),
	      .flow_ctrl_out(flow_ctrl_out),
	      .fbf_write_addr(fbf_write_addr),
	      .fbf_write_enable(fbf_write_enable),
	      .fbf_write_data(fbf_write_data),
	      .fbf_read_addr(fbf_read_addr),
	      .fbf_read_enable(fbf_read_enable),
	      .fbf_read_data(fbf_read_data),
	      .error(ipc_error));
	   
	   c_pseudo_sram_mac
	     #(.width(flit_data_width),
	       .depth(fbf_depth),
	       .regfile_type(fbf_regfile_type),
	       .reset_type(reset_type))
	   fbf
	     (.clk(clk),
	      .reset(1'b0),
	      .write_enable(fbf_write_enable),
	      .write_address(fbf_write_addr),
	      .write_data(fbf_write_data),
	      .read_enable(fbf_read_enable),
	      .read_address(fbf_read_addr),
	      .read_data(fbf_read_data));
	   
	   assign route_port_ip_ivc[ip*num_vcs*port_idx_width:
				    (ip+1)*num_vcs*port_idx_width-1]
		    = route_port_ivc;
	   assign inc_rc_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1] = inc_rc_ivc;
	   
	   assign vc_req_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1] = vc_req_ivc;
	   
	   assign sw_alloc_ipc_out_ip_op[ip*num_ports*
					 sw_alloc_ipc_out_width:
					 (ip+1)*num_ports*
					 sw_alloc_ipc_out_width-1]
		    = sw_alloc_ipc_out_op;
	   
	   assign xbc_data_in_ip[ip*int_flit_ctrl_width:
				 (ip+1)*int_flit_ctrl_width-1]
		    = int_flit_ctrl_out;
	   assign xbd_data_in_ip[ip*flit_data_width:(ip+1)*flit_data_width-1]
		    = flit_data_out;
	   
	   assign flow_ctrl_out_ip[ip*flow_ctrl_width:
				   (ip+1)*flow_ctrl_width-1]
		    = flow_ctrl_out;
	   
	   assign ipc_error_ip[ip] = ipc_error;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // virtual channel allocator
   //---------------------------------------------------------------------------
   
   wire [0:num_ports*num_vcs-1] 		       elig_op_ovc;
   
   wire [0:num_ports*num_vcs-1] 		       vc_gnt_op_ovc;
   wire [0:num_ports*num_vcs*num_ports-1] 	       vc_gnt_op_ovc_ip;
   wire [0:num_ports*num_vcs*num_vcs-1] 	       vc_gnt_op_ovc_ivc;
   vcr_vc_alloc_mac
     #(.num_message_classes(num_message_classes),
       .num_resource_classes(num_resource_classes),
       .num_vcs_per_class(num_vcs_per_class),
       .num_ports(num_ports),
       .allocator_type(vc_alloc_type),
       .arbiter_type(vc_alloc_arbiter_type),
       .reset_type(reset_type))
   vca
     (.clk(clk),
      .reset(reset),
      .route_port_ip_ivc(route_port_ip_ivc),
      .inc_rc_ip_ivc(inc_rc_ip_ivc),
      .elig_op_ovc(elig_op_ovc),
      .req_ip_ivc(vc_req_ip_ivc),
      .gnt_ip_ivc(vc_gnt_ip_ivc),
      .gnt_ip_ivc_ovc(vc_gnt_ip_ivc_ovc),
      .gnt_op_ovc(vc_gnt_op_ovc),
      .gnt_op_ovc_ip(vc_gnt_op_ovc_ip),
      .gnt_op_ovc_ivc(vc_gnt_op_ovc_ivc));
   
   
   //---------------------------------------------------------------------------
   // wavefronmt switch allocator
   //---------------------------------------------------------------------------
   
   generate
      
      if((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) &&
	 (sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT))
	begin
	   
	   vcr_sw_alloc_wf_mac
	     #(.num_ports(num_ports),
	       .wf_alloc_type(sw_alloc_type - `SW_ALLOC_TYPE_WF_BASE),
	       .spec_type(sw_alloc_spec_type),
	       .reset_type(reset_type))
	   swa_wf
	     (.clk(clk),
	      .reset(reset),
	      .alloc_in_ip_op(sw_alloc_wf_in_ip_op),
	      .alloc_out_ip_op(sw_alloc_wf_out_ip_op));
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // crossbars
   //---------------------------------------------------------------------------
   
   wire [0:num_ports*int_flit_ctrl_width-1] xbc_data_out_op;
   vcr_crossbar_mac
     #(.num_in_ports(num_ports),
       .num_out_ports(num_ports),
       .width(int_flit_ctrl_width),
       .crossbar_type(ctrl_crossbar_type),
       .reset_type(reset_type))
   xbc
     (.clk(clk),
      .reset(reset),
      .ctrl_ip_op(xbc_ctrl_ip_op),
      .data_in_ip(xbc_data_in_ip),
      .data_out_op(xbc_data_out_op));
   
   wire [0:num_ports*flit_data_width-1]     xbd_data_out_op;
   vcr_crossbar_mac
     #(.num_in_ports(num_ports),
       .num_out_ports(num_ports),
       .width(flit_data_width),
       .crossbar_type(data_crossbar_type),
       .reset_type(reset_type))
   xbd
     (.clk(clk),
      .reset(reset),
      .ctrl_ip_op(xbd_ctrl_ip_op),
      .data_in_ip(xbd_data_in_ip),
      .data_out_op(xbd_data_out_op));
   
   
   //---------------------------------------------------------------------------
   // output ports
   //---------------------------------------------------------------------------
   
   wire [0:num_ports-1] 		    opc_error_op;
   
   generate
      
      genvar 				    op;
      
      for(op = 0; op < num_ports; op = op + 1)
	begin:ops
	   
	   //-------------------------------------------------------------------
	   // connect switch allocator control signals
	   //-------------------------------------------------------------------
	   
	   wire [0:num_ports*sw_alloc_opc_out_width-1] sw_alloc_opc_out_ip;
	   
	   wire [0:num_ports*sw_alloc_opc_in_width-1]  sw_alloc_opc_in_ip;
	   
	   genvar 				       ip;
	   
	   for(ip = 0; ip < num_ports; ip = ip + 1)
	     begin:ips
		
		wire [0:sw_alloc_opc_out_width-1] sw_alloc_opc_out;
		assign sw_alloc_opc_out
		  = sw_alloc_opc_out_ip[ip*sw_alloc_opc_out_width:
					(ip+1)*sw_alloc_opc_out_width-1];
		
		assign sw_alloc_opc_out_ip_op[(ip*num_ports+op)*
					      sw_alloc_opc_out_width:
					      (ip*num_ports+op+1)*
					      sw_alloc_opc_out_width-1]
			 = sw_alloc_opc_out;
		
		wire [0:sw_alloc_ipc_out_width-1] sw_alloc_ipc_out;
		assign sw_alloc_ipc_out
		  = sw_alloc_ipc_out_ip_op[(ip*num_ports+op)*
					   sw_alloc_ipc_out_width:
					   (ip*num_ports+op+1)*
					   sw_alloc_ipc_out_width-1];
		
		wire [0:sw_alloc_opc_in_width-1] sw_alloc_opc_in;
		
		if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_IF)
		  begin
		     
		     assign sw_alloc_opc_in[0:sw_alloc_req_width-1]
			      = sw_alloc_ipc_out[0:sw_alloc_req_width-1];
		     
		     if(sw_alloc_spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		       begin
			  
			  assign xbc_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_opc_out[sw_alloc_gnt_width+
						      num_vcs*
						      int_flow_ctrl_width];
			  assign xbd_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_opc_out[sw_alloc_gnt_width+
						      num_vcs*
						      int_flow_ctrl_width];
			  
		       end
		     else
		       begin
			  
			  assign xbc_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_opc_out[0];
			  assign xbd_ctrl_ip_op[ip*num_ports+op]
				   = sw_alloc_opc_out[0];
			  
		       end
		     
		  end
		else if(sw_alloc_type == `SW_ALLOC_TYPE_SEP_OF)
		  begin
		     
		     assign sw_alloc_opc_in[0:sw_alloc_req_width-1]
			      = sw_alloc_ipc_out[0:sw_alloc_req_width-1];
		     assign sw_alloc_opc_in[sw_alloc_req_width:
					    sw_alloc_req_width+
					    sw_alloc_gnt_width-1]
			      = sw_alloc_ipc_out[sw_alloc_req_width:
						 sw_alloc_req_width+
						 sw_alloc_gnt_width-1];
		     
		  end
		else if((sw_alloc_type >= `SW_ALLOC_TYPE_WF_BASE) &&
			(sw_alloc_type <= `SW_ALLOC_TYPE_WF_LIMIT))
		  begin
		     
		     wire [0:sw_alloc_gnt_width-1] sw_alloc_wf_out;
		     assign sw_alloc_wf_out
		       = sw_alloc_wf_out_ip_op[(ip*num_ports+op)*
					       sw_alloc_wf_out_width:
					       (ip*num_ports+op+1)*
					       sw_alloc_wf_out_width-1];
		     
		     assign sw_alloc_opc_in[0:sw_alloc_req_width-1]
			      = sw_alloc_wf_out[0:sw_alloc_gnt_width-1];
		     
		  end
		
		assign sw_alloc_opc_in_ip[ip*sw_alloc_opc_in_width:
					  (ip+1)*sw_alloc_opc_in_width-1]
			 = sw_alloc_opc_in;
		
	     end
	   
	   
	   //-------------------------------------------------------------------
	   // output controller
	   //-------------------------------------------------------------------
	   
	   wire [0:int_flit_ctrl_width-1] int_flit_ctrl_in;
	   assign int_flit_ctrl_in
	     = xbc_data_out_op[op*int_flit_ctrl_width:
			       (op+1)*int_flit_ctrl_width-1];
	   
	   wire [0:flit_data_width-1] flit_data_in;
	   assign flit_data_in
	     = xbd_data_out_op[op*flit_data_width:(op+1)*flit_data_width-1];
	   
	   wire [0:flow_ctrl_width-1] flow_ctrl_in;
	   assign flow_ctrl_in
	     = flow_ctrl_in_op[op*flow_ctrl_width:(op+1)*flow_ctrl_width-1];
	   
	   wire [0:num_vcs-1] 	      vc_gnt_ovc;
	   assign vc_gnt_ovc = vc_gnt_op_ovc[op*num_vcs:(op+1)*num_vcs-1];
	   
	   wire [0:num_vcs*num_ports-1] vc_gnt_ovc_ip;
	   assign vc_gnt_ovc_ip = vc_gnt_op_ovc_ip[op*num_vcs*num_ports:
						   (op+1)*num_vcs*num_ports-1];
	   
	   wire [0:num_vcs*num_vcs-1] 	vc_gnt_ovc_ivc;
	   assign vc_gnt_ovc_ivc
	     = vc_gnt_op_ovc_ivc[op*num_vcs*num_vcs:(op+1)*num_vcs*num_vcs-1];
	   
	   wire [0:flit_ctrl_width-1] 		      flit_ctrl_out;
	   wire [0:flit_data_width-1] 		      flit_data_out;
	   wire [0:num_vcs-1] 			      elig_ovc;
	   wire 				      opc_error;
	   vcr_op_ctrl_mac
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
	       .flit_data_width(flit_data_width),
	       .error_capture_mode(error_capture_mode),
	       .track_vcs_at_output(track_vcs_at_output),
	       .int_flow_ctrl_type(int_flow_ctrl_type),
	       .cred_level_width(cred_level_width),
	       .sw_alloc_type(sw_alloc_type),
	       .sw_alloc_arbiter_type(sw_alloc_arbiter_type),
	       .sw_alloc_spec_type(sw_alloc_spec_type),
	       .vc_alloc_requires_credit(vc_alloc_requires_credit),
	       .port_id(op),
	       .reset_type(reset_type))
	   opc
	     (.clk(clk),
	      .reset(reset),
	      .flow_ctrl_in(flow_ctrl_in),
	      .vc_gnt_ovc(vc_gnt_ovc),
	      .vc_gnt_ovc_ip(vc_gnt_ovc_ip),
	      .vc_gnt_ovc_ivc(vc_gnt_ovc_ivc),
	      .sw_alloc_in_ip(sw_alloc_opc_in_ip),
	      .sw_alloc_out_ip(sw_alloc_opc_out_ip),
	      .int_flit_ctrl_in(int_flit_ctrl_in),
	      .flit_data_in(flit_data_in),
	      .flit_ctrl_out(flit_ctrl_out),
	      .flit_data_out(flit_data_out),
	      .elig_ovc(elig_ovc),
	      .error(opc_error));
	   
	   assign flit_ctrl_out_op[op*flit_ctrl_width:(op+1)*flit_ctrl_width-1]
		    = flit_ctrl_out;
	   assign flit_data_out_op[op*flit_data_width:(op+1)*flit_data_width-1]
		    = flit_data_out;
	   
	   assign elig_op_ovc[op*num_vcs:(op+1)*num_vcs-1] = elig_ovc;
	   
	   assign opc_error_op[op] = opc_error;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // error reporting
   //---------------------------------------------------------------------------
   
   generate
      
      if(error_capture_mode != `ERROR_CAPTURE_MODE_NONE)
	begin
	   
	   // synopsys translate_off
	   
	   integer 			  i;
	   
	   always @(posedge clk)
	     begin

		for(i = 0; i < num_ports; i = i + 1)
		  begin
		     if(ipc_error_ip[i])
		       $display({"ERROR: Router error detected at input ", 
				 "port %d in module %m."}, i);
		  end
		
		for(i = 0; i < num_ports; i = i + 1)
		  begin
		     if(opc_error_op[i])
		       $display({"ERROR: Router error detected at output ", 
				 "port %d in module %m."}, i);
		  end
		
	     end
	   // synopsys translate_on
	   
	   wire [0:num_ports+num_ports-1] errors_s, errors_q;
	   assign errors_s = {ipc_error_ip, opc_error_op};
	   c_err_rpt
	     #(.num_errors(num_ports+num_ports),
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
