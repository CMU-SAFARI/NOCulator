// $Id: vcr_vc_alloc_sep_of.v 2083 2010-06-01 10:49:54Z dub $

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



// VC allocator variant using separable output-first allocation
module vcr_vc_alloc_sep_of
  (clk, reset, route_port_ip_ivc, inc_rc_ip_ivc, elig_op_ovc, req_ip_ivc, 
   gnt_ip_ivc, gnt_ip_ivc_ovc, gnt_op_ovc, gnt_op_ovc_ip, gnt_op_ovc_ivc);
   
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
   
   // number of input and output ports on switch
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
   
   // request VC allocation
   input [0:num_ports*num_vcs-1] req_ip_ivc;
   
   // VC allocation successful (to input controller)
   output [0:num_ports*num_vcs-1] gnt_ip_ivc;
   wire [0:num_ports*num_vcs-1] gnt_ip_ivc;
   
   // granted output VC (to input controller)
   output [0:num_ports*num_vcs*num_vcs-1] gnt_ip_ivc_ovc;
   wire [0:num_ports*num_vcs*num_vcs-1] gnt_ip_ivc_ovc;
   
   // output VC was granted (to output controller)
   output [0:num_ports*num_vcs-1] gnt_op_ovc;
   wire [0:num_ports*num_vcs-1] 	gnt_op_ovc;
   
   // input port that each output VC was granted to
   output [0:num_ports*num_vcs*num_ports-1] gnt_op_ovc_ip;
   wire [0:num_ports*num_vcs*num_ports-1] gnt_op_ovc_ip;
   
   // input VC that each output VC was granted to
   output [0:num_ports*num_vcs*num_vcs-1] gnt_op_ovc_ivc;
   wire [0:num_ports*num_vcs*num_vcs-1]   gnt_op_ovc_ivc;
   
   
   generate
      
      genvar 				  mc;
      
      for(mc = 0; mc < num_message_classes; mc = mc + 1)
	begin:mcs
	   
	   //-------------------------------------------------------------------
	   // global wires
	   //-------------------------------------------------------------------
	   
	   wire [0:num_ports*num_resource_classes*num_vcs_per_class*
		 num_ports*num_resource_classes*num_vcs_per_class-1] 
             req_out_ip_irc_icvc_op_orc_ocvc;
	   wire [0:num_ports*num_resource_classes*num_vcs_per_class*
		 num_ports*num_resource_classes*num_vcs_per_class-1] 
             gnt_out_ip_irc_icvc_op_orc_ocvc;
	   wire [0:num_ports*num_resource_classes*num_vcs_per_class*
		 num_ports*num_resource_classes*num_vcs_per_class-1] 
             gnt_in_ip_irc_icvc_op_orc_ocvc;
	   
	   
	   //-------------------------------------------------------------------
	   // input stage
	   //-------------------------------------------------------------------
	   
	   genvar ip;
	   
	   for(ip = 0; ip < num_ports; ip = ip + 1)
	     begin:ips
		
		genvar 			   irc;
		
		for(irc = 0; irc < num_resource_classes; irc = irc + 1)
		  begin:ircs
		     
		     genvar icvc;
		     
		     for(icvc = 0; icvc < num_vcs_per_class; icvc = icvc + 1)
		       begin:icvcs
			  
			  //----------------------------------------------------
			  // generate requests for output stage
			  //----------------------------------------------------
			  
			  wire req;
			  assign req
			    = req_ip_ivc[((ip*num_message_classes+mc)*
					  num_resource_classes+irc)*
					 num_vcs_per_class+icvc];
			  
			  wire [0:port_idx_width-1] route_port;
			  assign route_port
			    = route_port_ip_ivc[(((ip*num_message_classes+mc)*
						  num_resource_classes+irc)*
						 num_vcs_per_class+icvc)*
						port_idx_width:
						(((ip*num_message_classes+mc)*
						  num_resource_classes+irc)*
						 num_vcs_per_class+icvc+1)*
						port_idx_width-1];
			  
			  wire 			    inc_rc;
			  assign inc_rc
			    = inc_rc_ip_ivc[((ip*num_message_classes+mc)*
					     num_resource_classes+irc)*
					    num_vcs_per_class+icvc];
			  
			  wire [0:num_ports-1] 	    route_op;
			  
			  wire [0:num_ports*num_resource_classes*
				num_vcs_per_class-1] req_out_op_orc_ocvc;
			  
			  genvar 		    op;
			  
			  for(op = 0; op < num_ports; op = op + 1)
			    begin:ops
			       
			       wire route;
			       assign route = (route_port == op);
			       
			       assign route_op[op] = route;
			       
			       genvar orc;
			       
			       for(orc = 0; orc < num_resource_classes; 
				   orc = orc + 1)
				 begin:orcs
				    
				    wire req_out;
				    
				    if((orc == irc) &&
				       (irc == (num_resource_classes - 1)))
				      assign req_out = req & route;
				    else if(orc == irc)
				      assign req_out = req & route & ~inc_rc;
				    else if(orc == (irc + 1))
				      assign req_out = req & route & inc_rc;
				    else
                                      assign req_out = 1'b0;
				    
				    assign req_out_op_orc_ocvc
				      [(op*num_resource_classes+orc)*
				       num_vcs_per_class:
				       (op*num_resource_classes+orc+1)*
				       num_vcs_per_class-1]
					     = {num_vcs_per_class{req_out}};
				    
				 end
			    end
			       
			  assign req_out_ip_irc_icvc_op_orc_ocvc
			    [((ip*num_resource_classes+irc)*
			      num_vcs_per_class+icvc)*
			     num_ports*num_resource_classes*
			     num_vcs_per_class:
			     ((ip*num_resource_classes+irc)*
			      num_vcs_per_class+icvc+1)*
			     num_ports*num_resource_classes*
			     num_vcs_per_class-1]
				   = req_out_op_orc_ocvc;
			  
			  
			  //----------------------------------------------------
			  // input arbitration stage (select output VC)
			  //----------------------------------------------------
			  
			  wire [0:num_ports*num_resource_classes*
				num_vcs_per_class-1] gnt_out_op_orc_ocvc;
			  assign gnt_out_op_orc_ocvc
			    = gnt_out_ip_irc_icvc_op_orc_ocvc
			      [((ip*num_resource_classes+irc)*
				num_vcs_per_class+icvc)*
			       num_ports*num_resource_classes*
			       num_vcs_per_class:
			       ((ip*num_resource_classes+irc)*
				num_vcs_per_class+icvc+1)*
			       num_ports*num_resource_classes*
			       num_vcs_per_class-1];
			  
			  // NOTE: Logically, what we want to do here is select 
			  // the subvector that corresponds to the current input
			  // VC's selected output port; however, because the 
			  // subvectors for all other ports will never have any 
			  // grants anyway, we can just OR all the subvectors
			  // instead of using a proper MUX.
			  wire [0:num_resource_classes*num_vcs_per_class-1] 
                            gnt_out_orc_ocvc;
			  c_or_nto1
			    #(.width(num_resource_classes*num_vcs_per_class),
			      .num_ports(num_ports))
			  gnt_out_orc_ocvc_or
			    (.data_in(gnt_out_op_orc_ocvc),
			     .data_out(gnt_out_orc_ocvc));
			  
			  wire gnt_out;
			  if(irc == (num_resource_classes - 1))
			    assign gnt_out
			      = |gnt_out_orc_ocvc[irc*num_vcs_per_class:
						  (irc+1)*num_vcs_per_class-1];
			  else
			    assign gnt_out
			      = |gnt_out_orc_ocvc[irc*num_vcs_per_class:
						  (irc+2)*num_vcs_per_class-1];
			  
			  wire [0:num_resource_classes*num_vcs_per_class-1] 
			    req_in_orc_ocvc;
			  assign req_in_orc_ocvc = gnt_out_orc_ocvc;
			  
			  wire [0:num_resource_classes*num_vcs_per_class-1] 
                            gnt_in_orc_ocvc;
			  
			  genvar orc;
			  
			  for(orc = 0; orc < num_resource_classes;
			      orc = orc + 1)
			    begin:orcs
			       
			       wire [0:num_vcs_per_class-1] gnt_in_ocvc;
			       
			       if((orc == irc) || (orc == (irc + 1)))
				 begin
				    
				    wire [0:num_vcs_per_class-1] req_in_ocvc;
				    assign req_in_ocvc
				      = req_in_orc_ocvc[orc*
							num_vcs_per_class:
							(orc+1)*
							num_vcs_per_class-1];
				    
				    if(num_vcs_per_class > 1)
				      begin
					 
					 wire [0:num_vcs_per_class-1]
					   gnt_out_ocvc;
					 assign gnt_out_ocvc
						= gnt_out_orc_ocvc
						  [orc*num_vcs_per_class:
						   (orc+1)*num_vcs_per_class-1];
					 
					 wire update_arb;
					 assign update_arb = |gnt_out_ocvc;
					      
					 c_arbiter
					   #(.num_ports(num_vcs_per_class),
					     .arbiter_type(arbiter_type),
					     .reset_type(reset_type))
					 gnt_in_ocvc_arb
					   (.clk(clk),
					    .reset(reset),
					    .update(update_arb),
					    .req(req_in_ocvc),
					    .gnt(gnt_in_ocvc));
					 
				      end
				    else
				      assign gnt_in_ocvc = req_in_ocvc;
				    
				 end
			       else
				 assign gnt_in_ocvc = {num_vcs_per_class{1'b0}};
			       
			       assign gnt_in_orc_ocvc[orc*
						      num_vcs_per_class:
						      (orc+1)*
						      num_vcs_per_class-1]
					= gnt_in_ocvc;
			       
			    end
			  
			  wire [0:num_ports*num_resource_classes*
				num_vcs_per_class-1] gnt_in_op_orc_ocvc;
			  c_mat_mult
			    #(.dim1_width(num_ports),
			      .dim2_width(1),
			      .dim3_width(num_resource_classes*
					  num_vcs_per_class))
			  gnt_in_op_orc_ocvc_mmult
			    (.input_a(route_op),
			     .input_b(gnt_in_orc_ocvc),
			     .result(gnt_in_op_orc_ocvc));
			  
			  assign gnt_in_ip_irc_icvc_op_orc_ocvc
			    [((ip*num_resource_classes+irc)*
			      num_vcs_per_class+icvc)*
			     num_ports*num_resource_classes*
			     num_vcs_per_class:
			     ((ip*num_resource_classes+irc)*
			      num_vcs_per_class+icvc+1)*
			     num_ports*num_resource_classes*
			     num_vcs_per_class-1]
				   = gnt_in_op_orc_ocvc;
			  
			  
			  //----------------------------------------------------
			  // generate global grants
			  //----------------------------------------------------
			  
			  wire [0:num_vcs-1] 	     gnt_in_ovc;
			  c_align
			    #(.data_width(num_resource_classes*
					  num_vcs_per_class),
			      .dest_width(num_vcs),
			      .offset(mc*num_resource_classes*
				      num_vcs_per_class))
			  gnt_in_ovc_alg
			    (.data_in(gnt_in_orc_ocvc),
			     .dest_in({num_vcs{1'b0}}),
			     .data_out(gnt_in_ovc));
			  
			  assign gnt_ip_ivc_ovc[(((ip*num_message_classes+mc)*
						  num_resource_classes+irc)*
						 num_vcs_per_class+icvc)*
						num_vcs:
						(((ip*num_message_classes+mc)*
						  num_resource_classes+irc)*
						 num_vcs_per_class+icvc+1)*
						num_vcs-1]
				   = gnt_in_ovc;
			  
			  assign gnt_ip_ivc[((ip*num_message_classes+mc)*
					     num_resource_classes+irc)*
					    num_vcs_per_class+icvc]
				   = gnt_out;
			  
		       end
		     
		  end
		
	     end
	   
	   
	   //-------------------------------------------------------------------
	   // bit shuffling for changing sort order
	   //-------------------------------------------------------------------
	   
	   wire [0:num_ports*num_resource_classes*num_vcs_per_class*
		 num_ports*num_resource_classes*num_vcs_per_class-1] 
             gnt_in_op_orc_ocvc_ip_irc_icvc;
	   c_interleaver
	     #(.width(num_ports*num_resource_classes*num_vcs_per_class*
		      num_ports*num_resource_classes*num_vcs_per_class),
	       .num_blocks(num_ports*num_resource_classes*num_vcs_per_class))
	   gnt_in_ip_irc_icvc_op_orc_ocvc_intl
	     (.data_in(gnt_in_ip_irc_icvc_op_orc_ocvc),
	      .data_out(gnt_in_op_orc_ocvc_ip_irc_icvc));
	   
	   wire [0:num_ports*num_resource_classes*num_vcs_per_class*
		 num_ports*num_resource_classes*num_vcs_per_class-1] 
             req_out_op_orc_ocvc_ip_irc_icvc;
	   c_interleaver
	     #(.width(num_ports*num_resource_classes*num_vcs_per_class*
		      num_ports*num_resource_classes*num_vcs_per_class),
	       .num_blocks(num_ports*num_resource_classes*num_vcs_per_class))
	   req_out_op_orc_ocvc_ip_irc_icvc_intl
	     (.data_in(req_out_ip_irc_icvc_op_orc_ocvc),
	      .data_out(req_out_op_orc_ocvc_ip_irc_icvc));
	   
	   wire [0:num_ports*num_resource_classes*num_vcs_per_class*
		 num_ports*num_resource_classes*num_vcs_per_class-1] 
             gnt_out_op_orc_ocvc_ip_irc_icvc;
	   c_interleaver
	     #(.width(num_ports*num_resource_classes*num_vcs_per_class*
		      num_ports*num_resource_classes*num_vcs_per_class),
	       .num_blocks(num_ports*num_resource_classes*num_vcs_per_class))
	   gnt_out_ip_irc_icvc_op_orc_ocvc_intl
	     (.data_in(gnt_out_op_orc_ocvc_ip_irc_icvc),
	      .data_out(gnt_out_ip_irc_icvc_op_orc_ocvc));
	   
	   
	   //-------------------------------------------------------------------
	   // output stage
	   //-------------------------------------------------------------------
	   
	   genvar op;
	   
	   for (op = 0; op < num_ports; op = op + 1)
	     begin:ops
		
		genvar orc;
		
		for(orc = 0; orc < num_resource_classes; orc = orc + 1)
		  begin:orcs
		     
		     genvar ocvc;
		     
		     for(ocvc = 0; ocvc < num_vcs_per_class; ocvc = ocvc + 1)
		       begin:ocvcs
			  
			  //----------------------------------------------------
			  // second stage arbitration (select input port and VC)
			  //----------------------------------------------------
			  
			  wire [0:num_ports*num_resource_classes*
				num_vcs_per_class-1] gnt_in_ip_irc_icvc;
			  assign gnt_in_ip_irc_icvc
			    = gnt_in_op_orc_ocvc_ip_irc_icvc
			      [((op*num_resource_classes+orc)*
				num_vcs_per_class+ocvc)*
			       num_ports*num_resource_classes*
			       num_vcs_per_class:
			       ((op*num_resource_classes+orc)*
				num_vcs_per_class+ocvc+1)*
			       num_ports*num_resource_classes*
			       num_vcs_per_class-1];
			  
			  wire gnt_in;
			  assign gnt_in = |gnt_in_ip_irc_icvc;
			  
			  wire [0:num_ports*num_resource_classes*
				num_vcs_per_class-1] req_out_ip_irc_icvc;
			  assign req_out_ip_irc_icvc
			    = req_out_op_orc_ocvc_ip_irc_icvc
			      [((op*num_resource_classes+orc)*
				num_vcs_per_class+ocvc)*
			       num_ports*num_resource_classes*
			       num_vcs_per_class:
			       ((op*num_resource_classes+orc)*
				num_vcs_per_class+ocvc+1)*
			       num_ports*num_resource_classes*
			       num_vcs_per_class-1];
			  
			  wire elig;
			  assign elig
			    = elig_op_ovc[((op*num_message_classes+mc)*
					   num_resource_classes+orc)*
					  num_vcs_per_class+ocvc];
			  
			  wire 		     update_arb;
			  assign update_arb = gnt_in;
			  
			  wire [0:num_ports-1] req_out_ip;
			  
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
			  
			  
			  wire [0:num_ports*num_resource_classes*
				num_vcs_per_class-1] gnt_raw_ip_irc_icvc;
			  wire [0:num_ports*num_resource_classes*
				num_vcs_per_class-1] gnt_out_ip_irc_icvc;
			  
			  wire [0:num_ports*num_resource_classes*
				num_vcs_per_class-1] gnt_ip_irc_icvc;
			  
			  genvar ip;
			  
			  for(ip = 0; ip < num_ports; ip = ip + 1)
			    begin:ips
			       
			       wire [0:num_resource_classes*num_vcs_per_class-1]
                                 req_out_irc_icvc;
			       assign req_out_irc_icvc
				 = req_out_ip_irc_icvc[ip*
						       num_resource_classes*
						       num_vcs_per_class:
						       (ip+1)*
						       num_resource_classes*
						       num_vcs_per_class-1];
			       
			       assign req_out_ip[ip] = |req_out_irc_icvc;
			       
			       wire [0:num_resource_classes*num_vcs_per_class-1]
				 gnt_raw_irc_icvc;
			       
			       if(orc == 0)
				 begin
				    
				    wire [0:num_vcs_per_class-1] req_out_icvc;
				    assign req_out_icvc
				      = req_out_irc_icvc[0:num_vcs_per_class-1];
				    
				    wire [0:num_vcs_per_class-1] gnt_raw_icvc;
				    
				    if(num_vcs_per_class > 1)
				      begin
					 
					 wire update_arb;
					 assign update_arb
					   = gnt_in & gnt_out_ip[ip];
					 
					 c_arbiter
					   #(.num_ports(num_vcs_per_class),
					     .arbiter_type(arbiter_type),
					     .reset_type(reset_type))
					 gnt_raw_icvc_arb
					   (.clk(clk),
					    .reset(reset),
					    .update(update_arb),
					    .req(req_out_icvc),
					    .gnt(gnt_raw_icvc));
					 
				      end
				    else
				      assign gnt_raw_icvc = req_out_icvc;

				    c_align
				      #(.data_width(num_vcs_per_class),
					.dest_width(num_resource_classes*
						    num_vcs_per_class),
					.offset(0))
				    gnt_raw_irc_icvc_alg
				      (.data_in(gnt_raw_icvc),
				       .dest_in({(num_resource_classes*
						  num_vcs_per_class){1'b0}}),
				       .data_out(gnt_raw_irc_icvc));
				    
				 end
			       else
				 begin
				    
				    wire [0:num_vcs_per_class-1]
				      req_out_low_icvc;
				    assign req_out_low_icvc
				      = req_out_irc_icvc[(orc-1)*
							 num_vcs_per_class:
							 orc*
							 num_vcs_per_class-1];
				    
				    wire [0:num_vcs_per_class-1] 
				      req_out_high_icvc;
				    assign req_out_high_icvc
				      = req_out_irc_icvc[orc*
							 num_vcs_per_class:
							 (orc+1)*
							 num_vcs_per_class-1];
				    
				    wire [0:num_vcs_per_class-1] 
				      gnt_raw_low_icvc;
				    wire [0:num_vcs_per_class-1] 
				      gnt_raw_high_icvc;
				    
				    wire update_arb;
				    assign update_arb = gnt_in & gnt_out_ip[ip];
				    
				    c_arbiter
				      #(.num_ports(2*num_vcs_per_class),
					.arbiter_type(arbiter_type),
					.reset_type(reset_type))
				    gnt_raw_icvc_arb
				      (.clk(clk),
				       .reset(reset),
				       .update(update_arb),
				       .req({req_out_low_icvc,
					     req_out_high_icvc}),
				       .gnt({gnt_raw_low_icvc,
					     gnt_raw_high_icvc}));
				    
				    c_align
				      #(.data_width(2*num_vcs_per_class),
					.dest_width(num_resource_classes*
						    num_vcs_per_class),
					.offset((orc-1)*num_vcs_per_class))
				    gnt_raw_irc_icvc_alg
				      (.data_in({gnt_raw_low_icvc,
						 gnt_raw_high_icvc}),
				       .dest_in({(num_resource_classes*
						  num_vcs_per_class){1'b0}}),
				       .data_out(gnt_raw_irc_icvc));
				    
				 end

			       
			       assign gnt_raw_ip_irc_icvc[ip*
							  num_resource_classes*
							  num_vcs_per_class:
							  (ip+1)*
							  num_resource_classes*
							  num_vcs_per_class-1]
					= gnt_raw_irc_icvc;
			       
			       wire [0:num_resource_classes*num_vcs_per_class-1]
				 gnt_out_irc_icvc;
			       assign gnt_out_irc_icvc
				 = gnt_raw_irc_icvc &
				   {(num_resource_classes*
				     num_vcs_per_class){gnt_out_ip[ip] & elig}};
			       
			       assign gnt_out_ip_irc_icvc[ip*
							  num_resource_classes*
							  num_vcs_per_class:
							  (ip+1)*
							  num_resource_classes*
							  num_vcs_per_class-1]
					= gnt_out_irc_icvc;
			       
			    end
			  
			  wire [0:num_resource_classes*num_vcs_per_class-1] 
                            gnt_raw_irc_icvc;
			  c_select_1ofn
			    #(.num_ports(num_ports),
			      .width(num_resource_classes*num_vcs_per_class))
			  gnt_raw_irc_icvc_sel
			    (.select(gnt_out_ip),
			     .data_in(gnt_raw_ip_irc_icvc),
			     .data_out(gnt_raw_irc_icvc));
			  
			  wire [0:num_vcs-1] gnt_raw_ivc;
			  c_align
			    #(.data_width(num_resource_classes*
					  num_vcs_per_class),
			      .dest_width(num_vcs),
			      .offset(mc*num_resource_classes*
				      num_vcs_per_class))
			  gnt_raw_ivc_alg
			    (.data_in(gnt_raw_irc_icvc),
			     .dest_in({num_vcs{1'b0}}),
			     .data_out(gnt_raw_ivc));

			  assign gnt_out_op_orc_ocvc_ip_irc_icvc
			    [((op*num_resource_classes+orc)*
			      num_vcs_per_class+ocvc)*
			     num_ports*num_resource_classes*
			     num_vcs_per_class:
			     ((op*num_resource_classes+orc)*
			      num_vcs_per_class+ocvc+1)*
			     num_ports*num_resource_classes*
			     num_vcs_per_class-1]
				   = gnt_out_ip_irc_icvc;
			  
			  
			  //----------------------------------------------------
			  // generate control signals to output controller
			  //----------------------------------------------------
			  
			  assign gnt_op_ovc[((op*num_message_classes+mc)*
					     num_resource_classes+orc)*
					    num_vcs_per_class+ocvc]
				   = gnt_in;
			  
			  assign gnt_op_ovc_ip[(((op*num_message_classes+mc)*
						 num_resource_classes+orc)*
						num_vcs_per_class+ocvc)*
					       num_ports:
					       (((op*num_message_classes+mc)*
						 num_resource_classes+orc)*
						num_vcs_per_class+ocvc+1)*
					       num_ports-1]
				   = gnt_out_ip;

			  assign gnt_op_ovc_ivc[(((op*num_message_classes+mc)
						  *num_resource_classes+orc)*
						 num_vcs_per_class+ocvc)*
						num_vcs:
						(((op*num_message_classes+mc)*
						  num_resource_classes+orc)*
						 num_vcs_per_class+ocvc+1)*
						num_vcs-1]
				   = gnt_raw_ivc;
			  
		       end
		     
		  end
		
	     end
	   
	end
      
   endgenerate
   
endmodule
