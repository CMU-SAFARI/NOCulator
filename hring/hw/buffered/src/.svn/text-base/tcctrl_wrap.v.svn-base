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



// wrapper around pseudo-node controller module
module tcctrl_wrap
  (clk, reset, io_write, io_read, io_addr, io_write_data, io_read_data, io_done,
   cfg_addr_prefixes, cfg_req, cfg_write, cfg_addr, cfg_write_data, 
   cfg_read_data, cfg_done, node_ctrl, node_status);
   
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
   
   // base address width for interface bus
   localparam io_addr_prefix_width = cfg_addr_prefix_width;
   
   // register index width for interface bus
   localparam io_addr_suffix_width = cfg_addr_suffix_width;
   
   // width of interface bus addresses
   localparam io_addr_width = cfg_addr_width;
   
   // width of interface bus datapath
   localparam io_data_width = cfg_data_width;
   
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
   
   // number of bits in delay counter for acknowledgement (i.e., log2 of 
   // interval before acknowledgement is sent)
   parameter done_delay_width = 6;
   
   // number of node control signals to generate
   localparam node_ctrl_width = 2 + 2;
   
   // number of node status signals to accept
   localparam node_status_width = 1 + num_ports;
   
   input clk;
   input reset;
   
   // write request indicator from chip controller
   input io_write;
   
   // read request indicator from chip controller
   input io_read;
   
   // register address
   input [0:io_addr_width-1] io_addr;
   
   // input data
   input [0:io_data_width-1] io_write_data;
   
   // result data to chip controller
   output [0:io_data_width-1] io_read_data;
   wire [0:io_data_width-1] io_read_data;
   
   // completion indicator to chip controller
   output io_done;
   wire 		    io_done;
   
   // address prefixes assigned to this node
   input [0:num_cfg_addr_prefixes*cfg_addr_prefix_width-1] cfg_addr_prefixes;
   
   // config register access pending
   output cfg_req;
   wire 		    cfg_req;
   
   // config register access is write access
   output cfg_write;
   wire 		    cfg_write;
   
   // select config register to access
   output [0:cfg_addr_width-1] cfg_addr;
   wire [0:cfg_addr_width-1] cfg_addr;
   
   // data to be written to selected config register for write accesses
   output [0:cfg_data_width-1] cfg_write_data;
   wire [0:cfg_data_width-1] cfg_write_data;
   
   // contents of selected config register for read accesses
   input [0:cfg_data_width-1] cfg_read_data;
   
   // config register access complete
   input cfg_done;
   
   // node control signals
   output [0:node_ctrl_width-1] node_ctrl;
   wire [0:node_ctrl_width-1] node_ctrl;
   
   // node status signals
   input [0:node_status_width-1] node_status;
   
   tc_node_ctrl_mac
     #(.cfg_addr_prefix_width(cfg_addr_prefix_width),
       .cfg_addr_suffix_width(cfg_addr_suffix_width),
       .num_cfg_addr_prefixes(num_cfg_addr_prefixes),
       .cfg_data_width(cfg_data_width),
       .done_delay_width(done_delay_width),
       .node_ctrl_width(node_ctrl_width),
       .node_status_width(node_status_width),
       .reset_type(reset_type))
   nctl
     (.clk(clk),
      .reset(reset),
      .io_write(io_write),
      .io_read(io_read),
      .io_addr(io_addr),
      .io_write_data(io_write_data),
      .io_read_data(io_read_data),
      .io_done(io_done),
      .cfg_addr_prefixes(cfg_addr_prefixes),
      .cfg_req(cfg_req),
      .cfg_write(cfg_write),
      .cfg_addr(cfg_addr),
      .cfg_write_data(cfg_write_data),
      .cfg_read_data(cfg_read_data),
      .cfg_done(cfg_done),
      .node_ctrl(node_ctrl),
      .node_status(node_status));
   
endmodule
