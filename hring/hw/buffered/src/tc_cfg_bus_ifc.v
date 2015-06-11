// $Id: tc_cfg_bus_ifc.v 1833 2010-03-22 23:18:22Z dub $

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

// configuration bus interface module
module tc_cfg_bus_ifc
  (clk, reset, cfg_node_addrs, cfg_req, cfg_write, cfg_addr, cfg_write_data, 
   cfg_read_data, cfg_done, active, req, write, node_addr_match, reg_addr, 
   write_data, read_data, done);
   
`include "c_functions.v"
`include "c_constants.v"
   
   // number of bits in address that are considered base address
   parameter cfg_node_addr_width = 10;
   
   // width of register selector part of control register address
   parameter cfg_reg_addr_width = 6;
   
   // width of configuration bus addresses
   localparam cfg_addr_width = cfg_node_addr_width + cfg_reg_addr_width;
   
   // number of distinct base addresses to which this node replies
   parameter num_cfg_node_addrs = 2;
   
   // width of control register data
   parameter cfg_data_width = 32;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // node addresses assigned to this node
   input [0:num_cfg_node_addrs*cfg_node_addr_width-1] cfg_node_addrs;
   
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
   wire [0:cfg_data_width-1]  cfg_read_data;
   
   // config register access complete
   output cfg_done;
   wire 		      cfg_done;
   
   // interface logic activity indicator (e.g. for clock gating)
   output active;
   wire 		      active;
   
   // request to client logic
   output req;
   wire 		      req;
   
   // write indicator for current request
   output write;
   wire 		      write;
   
   // which of the configured node addresses matched?
   output [0:num_cfg_node_addrs-1] node_addr_match;
   wire [0:num_cfg_node_addrs-1] node_addr_match;
   
   // register address
   output [0:cfg_reg_addr_width-1] reg_addr;
   wire [0:cfg_reg_addr_width-1] reg_addr;
   
   // data to be written to register (if write request)
   output [0:cfg_data_width-1] write_data;
   wire [0:cfg_data_width-1] 	 write_data;
   
   // contents of selected register (if read request)
   input [0:cfg_data_width-1] read_data;
   
   // completion indicator for current request
   input done;
   
   
   wire 			 cfg_stop;
   
   wire 			 cfg_active_s, cfg_active_q;
   assign cfg_active_s = (cfg_active_q | cfg_req) & ~cfg_stop;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cfg_activeq
     (.clk(clk),
      .reset(reset),
      .d(cfg_active_s),
      .q(cfg_active_q));
   
   assign active = cfg_active_q;
   
   wire 			 cfg_active_dly_s, cfg_active_dly_q;
   assign cfg_active_dly_s
     = cfg_active_q ? (cfg_active_q & ~cfg_stop) : cfg_active_dly_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cfg_active_dlyq
     (.clk(clk),
      .reset(reset),
      .d(cfg_active_dly_s),
      .q(cfg_active_dly_q));
   
   wire 			 cfg_active_rise;
   assign cfg_active_rise = cfg_active_q & ~cfg_active_dly_q;
   
   wire 			 cfg_req_s, cfg_req_q;
   assign cfg_req_s = cfg_active_q ? cfg_active_rise : cfg_req_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cfg_reqq
     (.clk(clk),
      .reset(reset),
      .d(cfg_req_s),
      .q(cfg_req_q));
   
   assign req = cfg_req_q;
   
   wire 			 cfg_write_s, cfg_write_q;
   assign cfg_write_s
     = cfg_active_q ? (cfg_active_rise ? cfg_write : cfg_write_q) : cfg_write_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cfg_writeq
     (.clk(clk),
      .reset(1'b0),
      .d(cfg_write_s),
      .q(cfg_write_q));
   
   assign write = cfg_write_q;
   
   wire 			 cfg_do_write;
   assign cfg_do_write = cfg_req_q & cfg_write_q;
   
   wire [0:cfg_addr_width-1] 	 cfg_addr_s, cfg_addr_q;
   assign cfg_addr_s
     = cfg_active_q ? (cfg_active_rise ? cfg_addr : cfg_addr_q) : cfg_addr_q;
   c_dff
     #(.width(cfg_addr_width),
       .reset_type(reset_type))
   cfg_addrq
     (.clk(clk),
      .reset(1'b0),
      .d(cfg_addr_s),
      .q(cfg_addr_q));
   
   assign reg_addr = cfg_addr_q[cfg_node_addr_width:cfg_addr_width-1];
   
   wire [0:cfg_node_addr_width-1] cfg_node_addr_q;
   assign cfg_node_addr_q = cfg_addr_q[0:cfg_node_addr_width-1];
   
   wire [0:num_cfg_node_addrs-1]  cfg_node_addr_match;
   
   genvar 			  naddr;
   
   generate
      
      for(naddr = 0; naddr < num_cfg_node_addrs; naddr = naddr + 1)
	begin:naddrs
	   
	   assign cfg_node_addr_match[naddr]
		    = (cfg_node_addr_q == 
		       cfg_node_addrs[naddr*cfg_node_addr_width:
				      (naddr+1)*cfg_node_addr_width-1]);
	   
	end
      
   endgenerate
   
   assign node_addr_match = cfg_node_addr_match;
   
   wire [0:cfg_data_width-1] cfg_data_s, cfg_data_q;
   assign cfg_data_s = cfg_active_q ? 
		       (cfg_active_rise ? 
			cfg_write_data : 
			(done ? read_data : cfg_data_q)) : 
		       cfg_data_q;
   c_dff
     #(.width(cfg_data_width),
       .reset_type(reset_type))
   cfg_dataq
     (.clk(clk),
      .reset(1'b0),
      .d(cfg_data_s),
      .q(cfg_data_q));
   
   assign write_data = cfg_data_q;
   assign cfg_read_data = cfg_data_q;
   
   wire 		     cfg_done_s, cfg_done_q;
   assign cfg_done_s = cfg_active_q ? done : cfg_done_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cfg_doneq
     (.clk(clk),
      .reset(reset),
      .d(cfg_done_s),
      .q(cfg_done_q));
   
   assign cfg_done = cfg_done_q;
   
   assign cfg_stop = cfg_done_q | (cfg_req_q & ~|cfg_node_addr_match);
   
endmodule
