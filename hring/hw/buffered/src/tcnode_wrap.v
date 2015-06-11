// $Id: tcnode_wrap.v 1661 2009-11-02 05:01:21Z dub $

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



// wrapper around pseudo-node
module tcnode_wrap
  (clk, reset, router_address, port_id, flit_ctrl_out, flit_data_out, 
   flow_ctrl_in, flit_ctrl_in, flit_data_in, flow_ctrl_out, cfg_addr_prefixes, 
   cfg_req, cfg_write, cfg_addr, cfg_write_data, cfg_read_data, cfg_done, 
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
   
   // width required to select an individual port
   localparam port_idx_width = clogb(num_ports);
   
   // width of flit control signals
   localparam flit_ctrl_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (1 + vc_idx_width + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + vc_idx_width + 1) : 
       -1;
   
   // width of flow control signals
   localparam flow_ctrl_width = 1 + vc_idx_width;
   
   // select set of feedback polynomials used for LFSRs
   parameter lfsr_index = 0;
   
   // number of bits in address that are considered base address
   parameter cfg_addr_prefix_width = 10;
   
   // width of register selector part of control register address
   parameter cfg_addr_suffix_width = 6;
   
   // width of configuration bus addresses
   localparam cfg_addr_width = cfg_addr_prefix_width + cfg_addr_suffix_width;
   
   // number of distinct base addresses to which this node replies
   parameter num_cfg_addr_prefixes = 2;
   
   // width of configuration bus datapath
   parameter cfg_data_width = 32;
   
   // width of run cycle counter
   parameter num_packets_width = 16;
   
   // width of arrival rate LFSR
   parameter arrival_rv_width = 16;
   
   // width of message class selection LFSR
   parameter mc_idx_rv_width = 4;
   
   // width of resource class selection LFSR
   parameter rc_idx_rv_width = 4;
   
   // width of payload length selection LFSR
   parameter plength_idx_rv_width = 4;
   
   // number of selectable payload lengths
   parameter num_plength_vals = 2;
   
   // width of register that holds the number of outstanding packets
   parameter packet_count_width = 8;
   
   input clk;
   input reset;
   
   // current node's address
   input [0:router_addr_width-1] router_address;
   
   // router port to which this node is attached
   input [0:port_idx_width-1] port_id;
   
   // control signals for outgoing flit
   output [0:flit_ctrl_width-1] flit_ctrl_out;
   wire [0:flit_ctrl_width-1] flit_ctrl_out;
   
   // flit data for outgoing flit
   output [0:flit_data_width-1] flit_data_out;
   wire [0:flit_data_width-1] flit_data_out;
   
   // incoming flow control signals
   input [0:flow_ctrl_width-1] flow_ctrl_in;
   
   // control signals for incoming flit
   input [0:flit_ctrl_width-1] flit_ctrl_in;
   
   // flit data for incoming flit
   input [0:flit_data_width-1] flit_data_in;
   
   // outgoing flow control signals
   output [0:flow_ctrl_width-1] flow_ctrl_out;
   wire [0:flow_ctrl_width-1] flow_ctrl_out;
   
   // address prefixes assigned to this node
   input [0:num_cfg_addr_prefixes*cfg_addr_prefix_width-1] cfg_addr_prefixes;
   
   // config register access pending
   input cfg_req;
   
   // config register access is write access
   input cfg_write;
   
   // select config register to access
   input [0:cfg_addr_width-1] cfg_addr;
   
   // data to be written to selected config register for write accesses
   input [0:cfg_data_width-1] cfg_write_data;
   
   // contents of selected config register for read accesses
   output [0:cfg_data_width-1] cfg_read_data;
   wire [0:cfg_data_width-1] cfg_read_data;
   
   // config register access complete
   output cfg_done;
   wire 		     cfg_done;
   
   // internal error condition detected
   output error;
   wire 		     error;
   
   tc_node_mac
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
       .error_capture_mode(error_capture_mode),
       .routing_type(routing_type),
       .dim_order(dim_order),
       .lfsr_index(lfsr_index),
       .cfg_addr_prefix_width(cfg_addr_prefix_width),
       .cfg_addr_suffix_width(cfg_addr_suffix_width),
       .num_cfg_addr_prefixes(num_cfg_addr_prefixes),
       .cfg_data_width(cfg_data_width),
       .num_packets_width(num_packets_width),
       .arrival_rv_width(arrival_rv_width),
       .mc_idx_rv_width(mc_idx_rv_width),
       .rc_idx_rv_width(rc_idx_rv_width),
       .plength_idx_rv_width(plength_idx_rv_width),
       .num_plength_vals(num_plength_vals),
       .packet_count_width(packet_count_width),
       .reset_type(reset_type))
   node
     (.clk(clk),
      .reset(reset),
      .router_address(router_address),
      .port_id(port_id),
      .flit_ctrl_out(flit_ctrl_out),
      .flit_data_out(flit_data_out),
      .flow_ctrl_in(flow_ctrl_in),
      .flit_ctrl_in(flit_ctrl_in),
      .flit_data_in(flit_data_in),
      .flow_ctrl_out(flow_ctrl_out),
      .cfg_addr_prefixes(cfg_addr_prefixes),
      .cfg_req(cfg_req),
      .cfg_write(cfg_write),
      .cfg_addr(cfg_addr),
      .cfg_write_data(cfg_write_data),
      .cfg_read_data(cfg_read_data),
      .cfg_done(cfg_done),
      .error(error));
   
endmodule
