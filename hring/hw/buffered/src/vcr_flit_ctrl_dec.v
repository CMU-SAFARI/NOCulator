// $Id: vcr_flit_ctrl_dec.v 2048 2010-05-24 19:25:23Z dub $

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



// flit control signal decoding logic
module vcr_flit_ctrl_dec
  (clk, reset, flit_ctrl_in, header_info_in, flit_valid_out_ivc, 
   flit_head_out_ivc, flit_tail_out_ivc);
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
   
   // number of message classes (e.g. request, reply)
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
   
   // width required to select an individual port
   localparam port_idx_width = clogb(num_ports);
   
   // width required for lookahead routing information
   localparam la_route_info_width
     = port_idx_width + ((num_resource_classes > 1) ? 1 : 0);
   
   // select packet format
   parameter packet_format = `PACKET_FORMAT_EXPLICIT_LENGTH;
   
   // maximum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter max_payload_length = 4;
   
   // minimum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter min_payload_length = 1;
   
   // number of bits required to represent all possible payload sizes
   localparam payload_length_width
     = clogb(max_payload_length-min_payload_length+1);

   // width of counter for remaining flits
   localparam flit_ctr_width = clogb(max_payload_length);
   
   // width required to select individual router in a dimension
   localparam dim_addr_width = clogb(num_routers_per_dim);
   
   // width required to select individual router in network
   localparam router_addr_width = num_dimensions * dim_addr_width;
   
   // width required to select individual node at current router
   localparam node_addr_width = clogb(num_nodes_per_router);
   
   // width of global addresses
   localparam addr_width = router_addr_width + node_addr_width;
   
   // total number of bits required for storing routing information
   localparam route_info_width
     = num_resource_classes * router_addr_width + node_addr_width;
   
   // total number of bits of header information encoded in header flit payload
   localparam header_info_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (la_route_info_width + route_info_width) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (la_route_info_width + route_info_width + payload_length_width) : 
       -1;
   
   // width of flit control signals
   localparam flit_ctrl_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (1 + vc_idx_width + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + vc_idx_width + 1) : 
       -1;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // incoming flit control signals
   input [0:flit_ctrl_width-1] flit_ctrl_in;
   
   // header information associated with current flit
   input [0:header_info_width-1] header_info_in;
   
   // flit valid indicators for each VC
   output [0:num_vcs-1] flit_valid_out_ivc;
   wire [0:num_vcs-1] flit_valid_out_ivc;
   
   // flit is head flit
   output [0:num_vcs-1] flit_head_out_ivc;
   wire [0:num_vcs-1] flit_head_out_ivc;

   // flit is tail flit
   output [0:num_vcs-1] flit_tail_out_ivc;
   wire [0:num_vcs-1] flit_tail_out_ivc;
   
   
   generate
      
      case(packet_format)
	
	`PACKET_FORMAT_HEAD_TAIL,
	`PACKET_FORMAT_EXPLICIT_LENGTH:
	  begin
	     
	     wire flit_valid_in;
	     assign flit_valid_in = flit_ctrl_in[0];
	     
	     if(num_vcs > 1)
	       begin
		  
		  wire [0:vc_idx_width-1] flit_vc_in;
		  assign flit_vc_in = flit_ctrl_in[1:1+vc_idx_width-1];
		  
		  wire [0:num_vcs-1] 	  flit_valid_unqual_ivc;
		  c_decoder
		    #(.num_ports(num_vcs))
		  flit_valid_unqual_ivc_dec
		    (.data_in(flit_vc_in),
		     .data_out(flit_valid_unqual_ivc));
		  
		  assign flit_valid_out_ivc
		    = {num_vcs{flit_valid_in}} & flit_valid_unqual_ivc;
		  
	       end
	     else
	       assign flit_valid_out_ivc = flit_valid_in;
	     
	     wire 			  flit_head_out;
	     assign flit_head_out = flit_ctrl_in[1+vc_idx_width+0];
	     
	     assign flit_head_out_ivc = {num_vcs{flit_head_out}};
	     
	     case(packet_format)
	       
	       `PACKET_FORMAT_HEAD_TAIL:
		 begin
		    
		    wire flit_tail_out;
		    assign flit_tail_out = flit_ctrl_in[1+vc_idx_width+1];
		    
		    assign flit_tail_out_ivc = {num_vcs{flit_tail_out}};
		    
		 end
	       
	       `PACKET_FORMAT_EXPLICIT_LENGTH:
		 begin
		    
		    if(max_payload_length == 0)
		      begin
			 
			 if(max_payload_length == min_payload_length)
			   assign flit_tail_out_ivc = flit_head_out_ivc;
			 
			 // synopsys translate_off
			 else
			   begin
			      initial
			      begin
				 $display({"ERROR: The value of the ", 
					   "max_payload_length parameter ", 
					   "(%d) in module %m cannot be ",
					   "smaller than that of the ",
					   "min_payload_length parameter ",
					   "(%d)."},
					  max_payload_length, 
					  min_payload_length);
				 $stop;
			      end
			   end
			 // synopsys translate_on
			 
		      end
		    else if(max_payload_length == 1)
		      begin
			 
			 wire has_payload;
			 
			 if(max_payload_length > min_payload_length)
			   assign has_payload
			     = header_info_in[la_route_info_width+
					      route_info_width];
			 else if(max_payload_length == min_payload_length)
			   assign has_payload = 1'b1;
			 
			 // synopsys translate_off
			 else
			   begin
			      initial
			      begin
				 $display({"ERROR: The value of the ", 
					   "max_payload_length parameter ",
					   "(%d) in module %m cannot be ",
					   "smaller than that of the ",
					   "min_payload_length parameter ",
					   "(%d)."},
					  max_payload_length,
					  min_payload_length);
				 $stop;
			      end
			   end
			 // synopsys translate_on
			 
			 assign flit_tail_out_ivc
			   = ~flit_head_out_ivc | {num_vcs{~has_payload}};
			 
		      end
		    else
		      begin
			 
			 genvar ivc;
			 
			 for(ivc = 0; ivc < num_vcs; ivc = ivc + 1)
			   begin:ivcs
			      
			      wire flit_valid_out;
			      assign flit_valid_out = flit_valid_out_ivc[ivc];
			      
			      wire flit_head_out;
			      assign flit_head_out = flit_head_out_ivc[ivc];
			      
			      wire [0:flit_ctr_width-1] flit_ctr_next;
			      wire [0:flit_ctr_width-1] flit_ctr_s, flit_ctr_q;
			      assign flit_ctr_s
				= flit_valid_out ? 
				  (flit_head_out ? 
				   flit_ctr_next : 
				   (flit_ctr_q - 1'b1)) : 
				  flit_ctr_q;
			      c_dff
				#(.width(flit_ctr_width),
				  .reset_type(reset_type))
			      flit_ctrq
				(.clk(clk),
				 .reset(reset),
				 .d(flit_ctr_s),
				 .q(flit_ctr_q));
			      
			      wire 			flit_tail_out;
			      
			      if(max_payload_length > min_payload_length)
				begin
				   
				   wire [0:payload_length_width-1] 
				     payload_length;
				   assign payload_length
				     = header_info_in[la_route_info_width+
						      route_info_width:
						      la_route_info_width+
						      route_info_width+
						      payload_length_width-1];
				   
				   assign flit_ctr_next
				     = (min_payload_length - 1) + 
				       payload_length;
				   
				   if(min_payload_length == 0)
				     assign flit_tail_out = flit_head_out ? 
							    ~|payload_length : 
							    ~|flit_ctr_q;
				   else
				     assign flit_tail_out = ~flit_head_out & 
							    ~|flit_ctr_q;
				   
				end
			      else if(max_payload_length == min_payload_length)
				begin
				   assign flit_ctr_next
				     = max_payload_length - 1;
				   assign flit_tail_out
				     = ~flit_head_out & ~|flit_ctr_q;
				end
			      
			      // synopsys translate_off
			      else
				begin
				   initial
				   begin
				      $display({"ERROR: The value of the ", 
						"max_payload_length parameter ",
						"(%d) in module %m cannot be ",
						"smaller than that of the ",
						"min_payload_length ", 
						"parameter (%d)."},
					       max_payload_length,
					       min_payload_length);
				      $stop;
				   end
				end
			      // synopsys translate_on
			      
			      assign flit_tail_out_ivc[ivc] = flit_tail_out;
			      
			   end
			 
		      end
		    
		 end
	       
	     endcase
	     
	  end
	
      endcase
      
   endgenerate
   
endmodule
