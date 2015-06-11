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



// switch allocator variant usign separable input-first allocation
module vcr_comb_alloc_sep_if
  (clk, reset, route_port_ip_ivc, inc_rc_ip_ivc, elig_op_ovc, vc_req_ip_ivc,
   vc_gnt_ip_ivc, vc_gnt_ip_ivc_ovc, vc_gnt_op_ovc, vc_gnt_op_ovc_ip, 
   vc_gnt_op_ovc_ivc, sw_req_ip_ivc, sw_gnt_ip_ivc, sw_gnt_op_ip);
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
   
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
   
   // number of input and output ports on router
   parameter num_ports = 5;
   
   // width required to select an individual port
   localparam port_idx_width = clogb(num_ports);
   
   // select which arbiter type to use in allocator
   parameter arbiter_type = `ARBITER_TYPE_ROUND_ROBIN;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // destination port selects
   input [0:num_ports*num_vcs*port_idx_width-1] route_port_ip_ivc;
   
   // transition to next resource class
   input [0:num_ports*num_vcs-1] inc_rc_ip_ivc;
   
   // output VC is eligible for allocation (i.e., not currently allocated)
   input [0:num_ports*num_vcs-1] elig_op_ovc;
   
   // VC requests
   input [0:num_ports*num_vcs-1] vc_req_ip_ivc;
   
   // granted output VC (to input controller)
   output [0:num_ports*num_vcs*num_vcs-1] vc_gnt_ip_ivc_ovc;
   wire [0:num_ports*num_vcs*num_vcs-1] vc_gnt_ip_ivc_ovc;
   
   // VC grants (to input controller)
   output [0:num_ports*num_vcs-1] vc_gnt_ip_ivc;
   wire [0:num_ports*num_vcs-1] 	vc_gnt_ip_ivc;
   
   // output VC was granted (to output controller)
   output [0:num_ports*num_vcs-1] vc_gnt_op_ovc;
   wire [0:num_ports*num_vcs-1] 	vc_gnt_op_ovc;
   
   // input port that each output VC was granted to
   output [0:num_ports*num_vcs*num_ports-1] vc_gnt_op_ovc_ip;
   wire [0:num_ports*num_vcs*num_ports-1] vc_gnt_op_ovc_ip;
   
   // input VC that each output VC was granted to
   output [0:num_ports*num_vcs*num_vcs-1] vc_gnt_op_ovc_ivc;
   wire [0:num_ports*num_vcs*num_vcs-1]   vc_gnt_op_ovc_ivc;
   
   // switch requests
   input [0:num_ports*num_vcs-1] sw_req_ip_ivc;
   
   // switch grants (to input controller)
   output [0:num_ports*num_vcs-1] sw_gnt_ip_ivc;
   wire [0:num_ports*num_vcs-1] sw_gnt_ip_ivc;
   
   // crossbar control signals
   output [0:num_ports*num_ports-1] sw_gnt_op_ip;
   wire [0:num_ports*num_ports-1] sw_gnt_op_ip;
   
   
   //---------------------------------------------------------------------------
   // global wires
   //---------------------------------------------------------------------------
   
   wire [0:num_ports*num_ports-1] req_out_ip_op;
   wire [0:num_ports*num_ports-1] gnt_out_ip_op;
   
   wire [0:num_ports*num_vcs-1]   gnt_in_ip_ivc;
   wire [0:num_ports*num_packet_classes-1] req_out_ip_opc;
   wire [0:num_ports-1] 		   vc_req_ip;
   
   wire [0:num_ports*num_vcs-1] 	   next_vc_op_ovc;
   
   
   //---------------------------------------------------------------------------
   // input stage
   //---------------------------------------------------------------------------
   
   generate
      
      genvar 			  ip;
      
      for(ip = 0; ip < num_ports; ip = ip + 1)
	begin:ips
	   
	   wire [0:num_vcs*port_idx_width-1] route_port_ivc;
	   assign route_port_ivc
	     = route_port_ip_ivc[ip*num_vcs*port_idx_width:
				 (ip+1)*num_vcs*port_idx_width-1];
	   
	   wire [0:num_vcs-1] 		     inc_rc_ivc;
	   assign inc_rc_ivc = inc_rc_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1];
	   
	   wire [0:num_vcs-1] 		     gnt_in_ivc;
	   
	   wire 			     inc_rc;
	   assign inc_rc = |(inc_rc_ivc & gnt_in_ivc);
	   
	   wire [0:num_vcs*num_ports-1]      route_ivc_op;
	   
	   wire [0:num_packet_classes-1]     gnt_in_ipc;
	   wire [0:num_packet_classes-1]     req_out_opc;
	   
	   wire [0:num_vcs-1] 		     elig_ivc;
	   wire [0:num_vcs*num_vcs-1] 	     gnt_ivc_ovc;
	   
	   genvar 			     mc;
	   
	   for(mc = 0; mc < num_message_classes; mc = mc + 1)
	     begin:mcs
		
		genvar irc;
		
		for(irc = 0; irc < num_resource_classes; irc = irc + 1)
		  begin:ircs
		     
		     genvar icvc;
		     
		     for(icvc = 0; icvc < num_vcs_per_class; icvc = icvc + 1)
		       begin:icvcs
			  
			  wire [0:port_idx_width-1] route_port;
			  assign route_port
			    = route_port_ivc[((mc*num_resource_classes+irc)*
					      num_vcs_per_class+icvc)*
					     port_idx_width:
					     ((mc*num_resource_classes+irc)*
					      num_vcs_per_class+icvc+1)*
					     port_idx_width-1];
			  
			  wire [0:num_ports-1] 	    route_op;
			  c_decoder
			    #(.num_ports(num_ports))
			  route_op_dec
			    (.data_in(route_port),
			     .data_out(route_op));
			  
			  assign route_ivc_op[((mc*num_resource_classes+irc)*
					       num_vcs_per_class+icvc)*
					      num_ports:
					      ((mc*num_resource_classes+irc)*
					       num_vcs_per_class+icvc+1)*
					      num_ports-1]
				   = route_op;
			  
			  wire [0:num_vcs-1] 	    elig_ovc;
			  assign elig_ovc
			    = elig_op_ovc[route_port*num_vcs +: num_vcs];
			  
			  wire [0:num_vcs-1] 	    next_vc_ovc;
			  assign next_vc_ovc
			    = next_vc_op_ovc[route_port*num_vcs +: num_vcs];
			  
			  wire 			    elig;
			  wire [0:num_vcs-1] 	    gnt_ovc;
			  
			  if(irc == (num_resource_classes - 1))
			    begin
			       
			       wire [0:num_vcs_per_class-1] elig_ocvc;
			       assign elig_ocvc
				 = elig_ovc[(mc*num_resource_classes+irc)*
					       num_vcs_per_class:
					       (mc*num_resource_classes+irc+1)*
					       num_vcs_per_class-1];
			       
			       wire [0:num_vcs_per_class-1] next_vc_ocvc;
			       assign next_vc_ocvc
				 = next_vc_ovc[(mc*num_resource_classes+irc)*
					       num_vcs_per_class:
					       (mc*num_resource_classes+irc+1)*
					       num_vcs_per_class-1];
			       
			       c_align
				 #(.data_width(num_vcs_per_class),
				   .dest_width(num_vcs),
				   .offset((mc*num_resource_classes+irc)*
					   num_vcs_per_class))
			       gnt_ovc_alg
				 (.data_in(next_vc_ocvc),
				  .dest_in({num_vcs{1'b0}}),
				  .data_out(gnt_ovc));
			       
			       assign elig = |elig_ocvc;
			       
			    end
			  else
			    begin
			       
			       wire [0:num_vcs_per_class-1] elig_low_ocvc;
			       assign elig_low_ocvc
				 = elig_ovc[(mc*num_resource_classes+irc)*
					       num_vcs_per_class:
					       (mc*num_resource_classes+irc+1)*
					       num_vcs_per_class-1] & 
				   {num_vcs_per_class{~inc_rc}};
			       
			       wire 			    elig_low;
			       assign elig_low = |elig_low_ocvc;
			       
			       wire [0:num_vcs_per_class-1] elig_high_ocvc;
			       assign elig_high_ocvc
				 = elig_ovc[(mc*num_resource_classes+irc+1)*
					       num_vcs_per_class:
					       (mc*num_resource_classes+irc+2)*
					       num_vcs_per_class-1] &
				   {num_vcs_per_class{inc_rc}};
			       
			       wire 			    elig_high;
			       assign elig_high = |elig_high_ocvc;
			       
			       wire 			    inc_rc;
			       assign inc_rc
				 = inc_rc_ivc[(mc*num_resource_classes+irc)*
					      num_vcs_per_class+icvc];
			       
			       assign elig = inc_rc ? elig_high : elig_low;
			       
			       wire [0:num_vcs_per_class-1] next_vc_low_ocvc;
			       assign next_vc_low_ocvc
				 = next_vc_ovc[(mc*num_resource_classes+irc)*
					       num_vcs_per_class:
					       (mc*num_resource_classes+irc+1)*
					       num_vcs_per_class-1] & 
				   {num_vcs_per_class{~inc_rc}};
			       
			       wire [0:num_vcs_per_class-1] next_vc_high_ocvc;
			       assign next_vc_high_ocvc
				 = next_vc_ovc[(mc*num_resource_classes+irc+1)*
					       num_vcs_per_class:
					       (mc*num_resource_classes+irc+2)*
					       num_vcs_per_class-1] &
				   {num_vcs_per_class{inc_rc}};
			       
			       c_align
				 #(.data_width(2*num_vcs_per_class),
				   .dest_width(num_vcs),
				   .offset((mc*num_resource_classes+irc)*
					   num_vcs_per_class))
			       gnt_ovc_alg
				 (.data_in({next_vc_low_ocvc, 
					    next_vc_high_ocvc}),
				  .dest_in({num_vcs{1'b0}}),
				  .data_out(gnt_ovc));
			       
			    end
			  
			  assign elig_ivc[(mc*num_resource_classes+irc)*
					  num_vcs_per_class+icvc] = elig;
			  
			  assign gnt_ivc_ovc[((mc*num_resource_classes+irc)*
					      num_vcs_per_class+icvc)*
					     num_vcs:
					     ((mc*num_resource_classes+irc)*
					      num_vcs_per_class+icvc+1)*
					     num_vcs-1] = gnt_ovc;
			  
		       end
		     
		     wire [0:num_vcs_per_class-1] 	    gnt_in_icvc;
		     assign gnt_in_icvc
		       = gnt_in_ivc[(mc*num_resource_classes+irc)*
				    num_vcs_per_class:
				    (mc*num_resource_classes+irc+1)*
				    num_vcs_per_class-1];
		     
		     wire gnt_in;
		     assign gnt_in = |gnt_in_icvc;
		     
		     assign gnt_in_ipc[mc*num_resource_classes+irc] = gnt_in;

		     wire req_out;
		     if(irc > 0)
		       assign req_out
			 = inc_rc ? 
			   gnt_in_ipc[mc*num_resource_classes+irc-1] :
			   gnt_in_ipc[mc*num_resource_classes+irc];
		     else
		       assign req_out
			 = ~inc_rc & gnt_in_ipc[mc*num_resource_classes+irc];
		     
		     assign req_out_opc[mc*num_resource_classes+irc] = req_out;
		     
		  end
	     end
	   
	   
	   //-------------------------------------------------------------------
	   // perform input-side arbitration
	   //-------------------------------------------------------------------
	   
	   wire [0:num_ports-1] 			    gnt_out_op;
	   assign gnt_out_op = gnt_out_ip_op[ip*num_ports:(ip+1)*num_ports-1];
	   
	   wire 					    gnt_out;
	   assign gnt_out = |gnt_out_op;
	   
	   wire 		update_arb;
	   assign update_arb = gnt_out;
	   
	   wire [0:num_vcs-1] 	vc_req_ivc;
	   assign vc_req_ivc = vc_req_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1];
	   
	   wire [0:num_vcs-1] 	sw_req_ivc;
	   assign sw_req_ivc = sw_req_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1];
	   
	   wire [0:num_vcs-1] 	req_in_ivc;
	   assign req_in_ivc = (elig_ivc | ~vc_req_ivc) & sw_req_ivc;
	   
	   c_arbiter
	     #(.num_ports(num_vcs),
	       .reset_type(reset_type),
	       .arbiter_type(arbiter_type))
	   gnt_in_ivc_arb
	     (.clk(clk),
	      .reset(reset),
	      .update(update_arb),
	      .req(req_in_ivc),
	      .gnt(gnt_in_ivc));
	   
	   
	   //-------------------------------------------------------------------
	   // generate control signals for output stage
	   //-------------------------------------------------------------------
	   
	   wire [0:num_ports-1] req_out_op;
	   c_select_1ofn
	     #(.width(num_ports),
	       .num_ports(num_vcs))
	   req_out_op_sel
	     (.select(gnt_in_ivc),
	      .data_in(route_ivc_op),
	      .data_out(req_out_op));
	   
	   assign req_out_ip_op[ip*num_ports:(ip+1)*num_ports-1] = req_out_op;

	   assign req_out_ip_opc[ip*num_packet_classes:
				 (ip+1)*num_packet_classes-1] = req_out_opc;
	   
	   assign gnt_in_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1] = gnt_in_ivc;
	   
	   wire 		vc_req;
	   assign vc_req = |(gnt_in_ivc & vc_req_ivc);
	   
	   assign vc_req_ip[ip] = vc_req;
	   
	   
	   //-------------------------------------------------------------------
	   // generate global grants
	   //-------------------------------------------------------------------
	   
	   wire [0:num_vcs-1] 	gnt_ivc;
	   assign gnt_ivc = {num_vcs{gnt_out}} & gnt_in_ivc;
	   
	   
	   //-------------------------------------------------------------------
	   // generate global grants
	   //-------------------------------------------------------------------
	   
	   assign sw_gnt_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1] = gnt_ivc;
	   assign vc_gnt_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1]
		    = gnt_ivc & vc_req_ivc;
	   assign vc_gnt_ip_ivc_ovc[ip*num_vcs*num_vcs:(ip+1)*num_vcs*num_vcs-1]
		    = gnt_ivc_ovc;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // bit shuffling for changing sort order
   //---------------------------------------------------------------------------
   
   wire [0:num_ports*num_ports-1] req_out_op_ip;
   c_interleaver
     #(.width(num_ports*num_ports),
       .num_blocks(num_ports))
   req_out_op_ip_intl
     (.data_in(req_out_ip_op),
      .data_out(req_out_op_ip));
   
   wire [0:num_ports*num_ports-1] gnt_out_op_ip;
   c_interleaver
     #(.width(num_ports*num_ports),
       .num_blocks(num_ports))
   gnt_out_op_ip_intl
     (.data_in(gnt_out_op_ip),
      .data_out(gnt_out_ip_op));
   
   
   //---------------------------------------------------------------------------
   // output stage
   //---------------------------------------------------------------------------
   
   generate
      
      genvar 				    op;
      
      for (op = 0; op < num_ports; op = op + 1)
	begin:ops
	   
	   //-------------------------------------------------------------------
	   // perform output-side arbitration (select input port)
	   //-------------------------------------------------------------------
	   
	   wire [0:num_ports-1] req_out_ip;
	   assign req_out_ip = req_out_op_ip[op*num_ports:(op+1)*num_ports-1];
	   
	   // if any VC requesting this output port was granted at any input
	   // port in the first stage, one of these input ports will be granted
	   // here as well, and we can thus update priorities
	   wire 		update_arb;
	   assign update_arb = |req_out_ip;
	   
	   wire [0:num_ports-1] gnt_out_ip;
	   c_arbiter
	     #(.num_ports(num_ports),
	       .arbiter_type(arbiter_type),
	       .reset_type(reset_type))
	   gnt_out_ip_arb
	     (.clk(clk),
	      .reset(reset),
	      .update(update_arb),
	      .req(req_out_ip),
	      .gnt(gnt_out_ip));
	   
	   
	   //-------------------------------------------------------------------
	   // feed grants back to input stage
	   //-------------------------------------------------------------------
	   
	   assign gnt_out_op_ip[op*num_ports:(op+1)*num_ports-1] = gnt_out_ip;
	   
	   
	   //-------------------------------------------------------------------
	   // determine next eligible VC
	   //-------------------------------------------------------------------
	   
	   wire 				       vc_req;
	   assign vc_req = |(vc_req_ip & gnt_out_ip);
	   
	   wire [0:num_vcs-1] 			       gnt_in_ivc;
	   c_select_mofn
	     #(.num_ports(num_ports),
	       .width(num_vcs))
	   gnt_in_ivc_sel
	     (.select(gnt_out_ip),
	      .data_in(gnt_in_ip_ivc),
	      .data_out(gnt_in_ivc));
	   
	   wire [0:num_packet_classes-1] 	       req_out_opc;
	   c_select_mofn
	     #(.num_ports(num_ports),
	       .width(num_packet_classes))
	   req_out_opc_sel
	     (.select(gnt_out_ip),
	      .data_in(req_out_ip_opc),
	      .data_out(req_out_opc));
	   
	   wire [0:num_vcs-1] 			       elig_ovc;
	   assign elig_ovc = elig_op_ovc[op*num_vcs:(op+1)*num_vcs-1];
	   
	   wire [0:num_vcs-1] 	next_vc_ovc;
	   wire [0:num_vcs-1] 	gnt_ovc;
	   
	   genvar 		mc;
	   
	   for(mc = 0; mc < num_message_classes; mc = mc + 1)
	     begin:mcs
		
		wire [0:num_resource_classes-1] gnt_in_orc;
		
		genvar orc;
		
		for(orc = 0; orc < num_resource_classes; orc = orc + 1)
		  begin:orcs
		     
		     wire [0:num_vcs_per_class-1] elig_ocvc;
		     assign elig_ocvc
		       = elig_ovc[(mc*num_resource_classes+orc)*
				  num_vcs_per_class:
				  (mc*num_resource_classes+orc+1)*
				  num_vcs_per_class-1];
		     
		     wire [0:num_vcs_per_class-1] next_vc_ocvc;
		     
		     if(num_vcs_per_class > 1)
		       begin
			  
			  c_fp_arbiter
			    #(.num_ports(num_vcs_per_class))
			  next_vc_arb
			    (.req(elig_ocvc),
			     .gnt(next_vc_ocvc));
			  
		       end
		     else
		       assign next_vc_ocvc = elig_ocvc;
		     
		     assign next_vc_ovc[(mc*num_resource_classes+orc)*
					num_vcs_per_class:
					(mc*num_resource_classes+orc+1)*
					num_vcs_per_class-1]
			      = next_vc_ocvc;
		     
		     wire req_out;
		     assign req_out = req_out_opc[mc*num_resource_classes+orc];
		     
		     wire [0:num_vcs_per_class-1] gnt_ocvc;
		     assign gnt_ocvc
		       = {num_vcs_per_class{req_out}} & next_vc_ocvc;
		     
		     assign gnt_ovc[(mc*num_resource_classes+orc)*
				    num_vcs_per_class:
				    (mc*num_resource_classes+orc+1)*
				    num_vcs_per_class-1] = gnt_ocvc;
		     
		  end
	     end
	   
	   assign next_vc_op_ovc[op*num_vcs:(op+1)*num_vcs-1] = next_vc_ovc;
	   
	   
	   //-------------------------------------------------------------------
	   // generate control signals to output controller
	   //-------------------------------------------------------------------

	   assign vc_gnt_op_ovc[op*num_vcs:(op+1)*num_vcs-1]
		    = gnt_ovc & {num_vcs{vc_req}};
	   
	   wire [0:num_vcs*num_ports-1] 	  vc_gnt_ovc_ip;
	   c_mat_mult
	     #(.dim1_width(num_vcs),
	       .dim2_width(1),
	       .dim3_width(num_ports))
	   vc_gnt_ovc_ip_mmult
	     (.input_a(gnt_ovc),
	      .input_b(gnt_out_ip),
	      .result(vc_gnt_ovc_ip));

	   assign vc_gnt_op_ovc_ip[op*num_vcs*num_ports:
				   (op+1)*num_vcs*num_ports-1] = vc_gnt_ovc_ip;
	   
	   wire [0:num_vcs*num_vcs-1] 		  vc_gnt_ovc_ivc;
	   c_mat_mult
	     #(.dim1_width(num_vcs),
	       .dim2_width(1),
	       .dim3_width(num_vcs))
	   vc_gnt_ovc_ivc_mmult
	     (.input_a(gnt_ovc),
	      .input_b(gnt_in_ivc),
	      .result(vc_gnt_ovc_ivc));
	   
	   assign vc_gnt_op_ovc_ivc[op*num_vcs*num_vcs:
				    (op+1)*num_vcs*num_vcs-1] = vc_gnt_ovc_ivc;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // generate global outputs
   //---------------------------------------------------------------------------

   assign sw_gnt_op_ip = gnt_out_op_ip;
   
endmodule
