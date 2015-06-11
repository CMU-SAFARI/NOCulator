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



// VC allocator
module vcr_vc_alloc_mac
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
   
   // select implementation variant for VC allocator
   parameter allocator_type = `VC_ALLOC_TYPE_SEP_IF;
   
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
   
   // input port that each output VC was granted to (to output controller)
   output [0:num_ports*num_vcs*num_ports-1] gnt_op_ovc_ip;
   wire [0:num_ports*num_vcs*num_ports-1] gnt_op_ovc_ip;
   
   // input VC that each output VC was granted to (to output controller)
   output [0:num_ports*num_vcs*num_vcs-1] gnt_op_ovc_ivc;
   wire [0:num_ports*num_vcs*num_vcs-1]   gnt_op_ovc_ivc;
   
   generate
      
      if(allocator_type == `VC_ALLOC_TYPE_SEP_IF)
	begin
	   vcr_vc_alloc_sep_if
	     #(.num_message_classes(num_message_classes),
	       .num_resource_classes(num_resource_classes),
	       .num_vcs_per_class(num_vcs_per_class),
	       .num_ports(num_ports),
	       .arbiter_type(arbiter_type),
	       .reset_type(reset_type))
	   core_sep_if
	     (.clk(clk),
	      .reset(reset),
	      .route_port_ip_ivc(route_port_ip_ivc),
	      .inc_rc_ip_ivc(inc_rc_ip_ivc),
	      .elig_op_ovc(elig_op_ovc),
	      .req_ip_ivc(req_ip_ivc),
	      .gnt_ip_ivc(gnt_ip_ivc),
	      .gnt_ip_ivc_ovc(gnt_ip_ivc_ovc),
	      .gnt_op_ovc(gnt_op_ovc),
	      .gnt_op_ovc_ip(gnt_op_ovc_ip),
	      .gnt_op_ovc_ivc(gnt_op_ovc_ivc));
	end
      else if(allocator_type == `VC_ALLOC_TYPE_SEP_OF)
	begin
	   vcr_vc_alloc_sep_of
	     #(.num_message_classes(num_message_classes),
	       .num_resource_classes(num_resource_classes),
	       .num_vcs_per_class(num_vcs_per_class),
	       .num_ports(num_ports),
	       .arbiter_type(arbiter_type),
	       .reset_type(reset_type))
	   core_sep_of
	     (.clk(clk),
	      .reset(reset),
	      .route_port_ip_ivc(route_port_ip_ivc),
	      .inc_rc_ip_ivc(inc_rc_ip_ivc),
	      .elig_op_ovc(elig_op_ovc),
	      .req_ip_ivc(req_ip_ivc),
		.gnt_ip_ivc(gnt_ip_ivc),
	      .gnt_ip_ivc_ovc(gnt_ip_ivc_ovc),
	      .gnt_op_ovc(gnt_op_ovc),
	      .gnt_op_ovc_ip(gnt_op_ovc_ip),
	      .gnt_op_ovc_ivc(gnt_op_ovc_ivc));
	end
      else if((allocator_type >= `VC_ALLOC_TYPE_WF_BASE) &&
	      (allocator_type <= `VC_ALLOC_TYPE_WF_LIMIT))
	begin
	   vcr_vc_alloc_wf
	     #(.num_message_classes(num_message_classes),
	       .num_resource_classes(num_resource_classes),
	       .num_vcs_per_class(num_vcs_per_class),
	       .num_ports(num_ports),
	       .wf_alloc_type(allocator_type - `VC_ALLOC_TYPE_WF_BASE),
	       .reset_type(reset_type))
	   core_wf
	     (.clk(clk),
	      .reset(reset),
	      .route_port_ip_ivc(route_port_ip_ivc),
	      .inc_rc_ip_ivc(inc_rc_ip_ivc),
	      .elig_op_ovc(elig_op_ovc),
	      .req_ip_ivc(req_ip_ivc),
	      .gnt_ip_ivc(gnt_ip_ivc),
	      .gnt_ip_ivc_ovc(gnt_ip_ivc_ovc),
	      .gnt_op_ovc(gnt_op_ovc),
	      .gnt_op_ovc_ip(gnt_op_ovc_ip),
	      .gnt_op_ovc_ivc(gnt_op_ovc_ivc));
	end
      
   endgenerate
   
endmodule
