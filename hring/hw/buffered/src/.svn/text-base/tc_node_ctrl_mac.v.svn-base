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

// network pseudo-node controller 
module tc_node_ctrl_mac
  (clk, reset, io_write, io_read, io_addr, io_write_data, io_read_data, io_done,
   cfg_node_addrs, cfg_req, cfg_write, cfg_addr, cfg_write_data, cfg_read_data, 
   cfg_done, node_ctrl, node_status);
   
// register address declarations
`define CFG_ADDR_NODE_CTRL   0
`define CFG_ADDR_NODE_STATUS 1

`include "c_constants.v"
   
   // number of bits in address that are considered base address
   parameter cfg_node_addr_width = 10;
   
   // width of register selector part of control register address
   parameter cfg_reg_addr_width = 6;
   
   // width of configuration bus addresses
   localparam cfg_addr_width = cfg_node_addr_width + cfg_reg_addr_width;
   
   // number of distinct base addresses to which this node replies
   parameter num_cfg_node_addrs = 2;
   
   // width of configuration bus datapath
   parameter cfg_data_width = 32;
   
   // width of interface bus addresses
   localparam io_addr_width = cfg_addr_width;
   
   // width of interface bus datapath
   localparam io_data_width = cfg_data_width;
   
   // number of bits in delay counter for acknowledgement (i.e., log2 of 
   // interval before acknowledgement is sent)
   parameter done_delay_width = 6;
   
   // number of node control signals to generate
   parameter node_ctrl_width = 2;
   
   // number of node status signals to accept
   parameter node_status_width = 1;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
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
   
   // node addresses assigned to this node
   input [0:num_cfg_node_addrs*cfg_node_addr_width-1] cfg_node_addrs;
   
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
   
   
   //---------------------------------------------------------------------------
   // global interface bus uses a generic four-phase handshake
   //---------------------------------------------------------------------------
   
   wire 		      io_req;
   assign io_req = io_write | io_read;
   
   // double-register incoming async signal for metastability avoidance
   wire 		      io_req_stable;
   c_shift_reg
     #(.width(1),
       .depth(2),
       .reset_type(reset_type))
   io_req_stable_sr
     (.clk(clk),
      .reset(reset),
      .enable(1'b1),
      .data_in(io_req),
      .data_out(io_req_stable));
   
   wire  		      io_req_stable_s, io_req_stable_q;
   assign io_req_stable_s = io_req_stable;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   io_req_stableq
     (.clk(clk),
      .reset(reset),
      .d(io_req_stable_s),
      .q(io_req_stable_q));
   
   wire 		      io_req_stable_rise;
   assign io_req_stable_rise = io_req_stable & ~io_req_stable_q;
   
   wire 		      io_req_stable_fall;
   assign io_req_stable_fall = ~io_req_stable & io_req_stable_q;
   
   wire 		      io_active;
   assign io_active = io_req_stable | io_req_stable_q;
   

   //---------------------------------------------------------------------------
   // config bus protocol requires that write, addr and write_data be stable at 
   // least for the cycle following the one in which req is high; this is in 
   // order to facilitate clock gating
   //---------------------------------------------------------------------------
   
   wire 		     cfg_req_s, cfg_req_q;
   assign cfg_req_s = io_active ? io_req_stable_rise : cfg_req_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cfg_reqq
     (.clk(clk),
      .reset(reset),
      .d(cfg_req_s),
      .q(cfg_req_q));
   
   assign cfg_req = cfg_req_q;
   
   wire 		     cfg_write_s, cfg_write_q;
   assign cfg_write_s = io_active ? 
			(io_req_stable_rise ? io_write : cfg_write_q) : 
			cfg_write_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cfg_writeq
     (.clk(clk),
      .reset(1'b0),
      .d(cfg_write_s),
      .q(cfg_write_q));
   
   assign cfg_write = cfg_write_q;
   
   wire [0:cfg_addr_width-1] cfg_addr_s, cfg_addr_q;
   assign cfg_addr_s = io_active ? 
		       (io_req_stable_rise ? io_addr : cfg_addr_q) : 
		       cfg_addr_q;
   c_dff
     #(.width(cfg_addr_width),
       .reset_type(reset_type))
   cfg_addrq
     (.clk(clk),
      .reset(1'b0),
      .d(cfg_addr_s),
      .q(cfg_addr_q));
   
   assign cfg_addr = cfg_addr_q;
   
   wire 		     cfg_done_muxed;
   wire [0:cfg_data_width-1] cfg_read_data_muxed;
   
   wire [0:cfg_data_width-1] cfg_data_s, cfg_data_q;
   assign cfg_data_s = io_active ? 
		       (io_req_stable_rise ? 
			io_write_data : 
			(cfg_done_muxed ? cfg_read_data_muxed : cfg_data_q)) : 
		       cfg_data_q;
   c_dff
     #(.width(cfg_data_width),
       .reset_type(reset_type))
   cfg_dataq
     (.clk(clk),
      .reset(1'b0),
      .d(cfg_data_s),
      .q(cfg_data_q));
   
   assign cfg_write_data = cfg_data_q;   
   assign io_read_data = cfg_data_q;
   
   wire 		     done_pending_q;
   
   wire [0:done_delay_width-1] done_delay_s, done_delay_q;
   assign done_delay_s
     = io_active ? 
       ({done_delay_width{cfg_done_muxed}} | (done_delay_q - done_pending_q)) : 
       done_delay_q;
   c_dff
     #(.width(done_delay_width),
       .reset_type(reset_type))
   done_delayq
     (.clk(clk),
      .reset(1'b0),
      .d(done_delay_s),
      .q(done_delay_q));
   
   wire 		       done_delay_elapsed;
   assign done_delay_elapsed = ~|done_delay_q;
   
   wire 		       done_pending_s;
   assign done_pending_s
     = io_active ?
       ((done_pending_q & ~done_delay_elapsed) | cfg_done_muxed) :
       done_pending_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   done_pendingq
     (.clk(clk),
      .reset(reset),
      .d(done_pending_s),
      .q(done_pending_q));
   
   wire 		  io_done_s, io_done_q;
   assign io_done_s
     = io_active ? 
       ((io_done_q & ~io_req_stable_fall) | 
	(done_pending_q & done_delay_elapsed)) : 
       io_done_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   io_doneq
     (.clk(clk),
      .reset(reset),
      .d(io_done_s),
      .q(io_done_q));
   
   assign io_done = io_done_q;
   
   
   //---------------------------------------------------------------------------
   // in addition to child nodes on the config bus, the controller provides an
   // internal pseudo-node that generates a bunch of generic control signals
   //---------------------------------------------------------------------------
   
   wire [0:cfg_node_addr_width-1] cfg_node_addr_q;
   assign cfg_node_addr_q = cfg_addr_q[0:cfg_node_addr_width-1];
   
   wire [0:num_cfg_node_addrs-1] cfg_node_addr_match;
   
   genvar 			 naddr;
   
   generate
      
      for(naddr = 0; naddr < num_cfg_node_addrs; naddr = naddr + 1)
	begin:naddrs
	   
	   assign cfg_node_addr_match[naddr]
		    = (cfg_node_addr_q == 
		       cfg_node_addrs[naddr*cfg_node_addr_width:
				      (naddr+1)*cfg_node_addr_width-1]);
	   
	end
      
   endgenerate
   
   wire cfg_sel_node;
   assign cfg_sel_node = |cfg_node_addr_match;
   
   wire [0:cfg_reg_addr_width-1] cfg_reg_addr_q;
   assign cfg_reg_addr_q
     = cfg_addr_q[cfg_node_addr_width:cfg_addr_width-1];
   
   wire 			 cfg_sel_node_ctrl;
   assign cfg_sel_node_ctrl
     = cfg_sel_node && (cfg_reg_addr_q == `CFG_ADDR_NODE_CTRL);
   
   wire 			 cfg_sel_node_status;
   assign cfg_sel_node_status
     = cfg_sel_node && (cfg_reg_addr_q == `CFG_ADDR_NODE_STATUS);
   
   wire [0:cfg_data_width-1] 	 cfg_read_data_node_status;
   
   c_select_1ofn
     #(.num_ports(2),
       .width(cfg_data_width))
   cfg_read_data_muxed_sel
     (.select({~cfg_sel_node,
	       cfg_sel_node_status}),
      .data_in({cfg_read_data,
		cfg_read_data_node_status}),
      .data_out(cfg_read_data_muxed));
   
   assign cfg_done_muxed = cfg_sel_node ? cfg_req_q : cfg_done;
   
   
   //---------------------------------------------------------------------------
   // node control register
   // =====================
   // The bits in this register are exposed to the nodes as generic control 
   // signals.
   //---------------------------------------------------------------------------
   
   wire [0:node_ctrl_width-1] node_ctrl_loadval;
   c_align
     #(.data_width(cfg_data_width),
       .dest_width(node_ctrl_width))
   node_ctrl_loadval_alg
     (.data_in(cfg_data_q),
      .dest_in({node_ctrl_width{1'b0}}),
      .data_out(node_ctrl_loadval));
   
   wire 		      write_node_ctrl;
   assign write_node_ctrl = cfg_req_q & cfg_write_q & cfg_sel_node_ctrl;
   
   wire [0:node_ctrl_width-1] node_ctrl_s, node_ctrl_q;
   assign node_ctrl_s = io_active ? 
			(write_node_ctrl ? node_ctrl_loadval : node_ctrl_q) : 
			node_ctrl_q;
   c_dff
     #(.width(node_ctrl_width),
       .reset_type(reset_type))
   node_ctrlq
     (.clk(clk),
      .reset(reset),
      .d(node_ctrl_s),
      .q(node_ctrl_q));
   
   assign node_ctrl = node_ctrl_q;
   
   
   //---------------------------------------------------------------------------
   // node status register
   // ====================
   // This register exposes a bunch of generic status signals coming in from the
   // nodes.
   //---------------------------------------------------------------------------
   
   wire [0:node_status_width-1] node_status_s, node_status_q;
   assign node_status_s
     = io_active ? (cfg_req_q ? node_status : node_status_q) : node_status_q;
   c_dff
     #(.width(node_status_width),
       .reset_type(reset_type))
   node_statusq
     (.clk(clk),
      .reset(1'b0),
      .d(node_status_s),
      .q(node_status_q));
   
   c_align
     #(.data_width(node_status_width),
       .dest_width(cfg_data_width))
   cfg_read_data_node_status_alg
     (.data_in(node_status_q),
      .dest_in({cfg_data_width{1'b0}}),
      .data_out(cfg_read_data_node_status));
   
endmodule
