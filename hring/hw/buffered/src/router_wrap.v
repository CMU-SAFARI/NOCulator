// $Id: router_wrap.v 1730 2009-12-13 08:39:16Z dub $

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



// wrapper around router component (configures router parameters based on
// selected network topology, etc.)
module router_wrap
  (clk, reset, router_address, flit_ctrl_in_ip, flit_data_in_ip, 
   flow_ctrl_out_ip, flit_ctrl_out_op, flit_data_out_op, flow_ctrl_in_op, 
   error);
   
`include "c_functions.v"   
`include "c_constants.v"
`include "vcr_constants.v"
`include "parameters.v"
   
   
   // total number of packet classes
   localparam num_packet_classes = num_message_classes * num_resource_classes;
   
   // number of VCs
   localparam num_vcs = num_packet_classes * num_vcs_per_class;
   
   // width required to select individual VC
   localparam vc_idx_width = clogb(num_vcs);
   
   // total number of routers
   localparam num_routers
     = (num_nodes + num_nodes_per_router - 1) / num_nodes_per_router;
   
   // number of routers in each dimension
   localparam num_routers_per_dim = croot(num_routers, num_dimensions);
   
   // width required to select individual router in a dimension
   localparam dim_addr_width = clogb(num_routers_per_dim);
   
   // width required to select individual router in entire network
   localparam router_addr_width = num_dimensions * dim_addr_width;
   
   // connectivity within each dimension
   localparam connectivity
     = (topology == `TOPOLOGY_MESH) ?
       `CONNECTIVITY_LINE :
       (topology == `TOPOLOGY_TORUS) ?
       `CONNECTIVITY_RING :
       (topology == `TOPOLOGY_FBFLY) ?
       `CONNECTIVITY_FULL :
       -1;
   
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
   
   // width of flit control signals
   localparam flit_ctrl_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (1 + vc_idx_width + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + vc_idx_width + 1) : 
       -1;
   
   // width of flow control signals
   localparam flow_ctrl_width = 1 + vc_idx_width;
   
   
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
   
   generate
      
      case(router_type)
	
	`ROUTER_TYPE_WORMHOLE:
	  begin
	     
	     whr_top
	       #(.num_flit_buffers(num_flit_buffers),
		 .num_header_buffers(num_header_buffers),
		 .num_routers_per_dim(num_routers_per_dim),
		 .num_dimensions(num_dimensions),
		 .num_nodes_per_router(num_nodes_per_router),
		 .connectivity(connectivity),
		 .packet_format(packet_format),
		 .max_payload_length(max_payload_length),
		 .min_payload_length(min_payload_length),
		 .flit_data_width(flit_data_width),
		 .perf_ctr_enable(perf_ctr_enable),
		 .perf_ctr_width(perf_ctr_width),
		 .error_capture_mode(error_capture_mode),
		 .restrict_turns(restrict_turns),
		 .routing_type(routing_type),
		 .dim_order(dim_order),
		 .header_fifo_type(header_fifo_type),
		 .input_stage_can_hold(input_stage_can_hold),
		 .fbf_regfile_type(fbf_regfile_type),
		 .arbiter_type(sw_alloc_arbiter_type),
		 .crossbar_type(data_crossbar_type),
		 .reset_type(reset_type))
	     whr
	       (.clk(clk),
		.reset(reset),
		.router_address(router_address),
		.flit_ctrl_in_ip(flit_ctrl_in_ip),
		.flit_data_in_ip(flit_data_in_ip),
		.flow_ctrl_out_ip(flow_ctrl_out_ip),
		.flit_ctrl_out_op(flit_ctrl_out_op),
		.flit_data_out_op(flit_data_out_op),
		.flow_ctrl_in_op(flow_ctrl_in_op),
		.error(error));
	     
	  end
	
	`ROUTER_TYPE_VC:
	  begin
	     
	     vcr_top
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
		 .perf_ctr_enable(perf_ctr_enable),
		 .perf_ctr_width(perf_ctr_width),
		 .error_capture_mode(error_capture_mode),
		 .track_vcs_at_output(track_vcs_at_output),
		 .restrict_turns(restrict_turns),
		 .routing_type(routing_type),
		 .dim_order(dim_order),
		 .int_flow_ctrl_type(int_flow_ctrl_type),
		 .header_fifo_type(header_fifo_type),
		 .fbf_regfile_type(fbf_regfile_type),
		 .vc_alloc_type(vc_alloc_type),
		 .vc_alloc_arbiter_type(vc_alloc_arbiter_type),
		 .vc_alloc_requires_credit(vc_alloc_requires_credit),
		 .sw_alloc_type(sw_alloc_type),
		 .sw_alloc_arbiter_type(sw_alloc_arbiter_type),
		 .sw_alloc_spec_type(sw_alloc_spec_type),
		 .ctrl_crossbar_type(ctrl_crossbar_type),
		 .data_crossbar_type(data_crossbar_type),
		 .reset_type(reset_type))
	     vcr
	       (.clk(clk),
		.reset(reset),
		.router_address(router_address),
		.flit_ctrl_in_ip(flit_ctrl_in_ip),
		.flit_data_in_ip(flit_data_in_ip),
		.flow_ctrl_out_ip(flow_ctrl_out_ip),
		.flit_ctrl_out_op(flit_ctrl_out_op),
		.flit_data_out_op(flit_data_out_op),
		.flow_ctrl_in_op(flow_ctrl_in_op),
		.error(error));
	     
	  end
	
      endcase
      
   endgenerate
   
endmodule
