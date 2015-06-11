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

`default_nettype none

module testbench
  ();
   
`include "c_functions.v"   
`include "c_constants.v"
   
   parameter Tclk = 4;
   
   // number of bits in address that are considered base address
   parameter cfg_node_addr_width = 10;
   
   // width of register selector part of control register address
   parameter cfg_reg_addr_width = 6;
   
   // width of configuration bus addresses
   localparam cfg_addr_width = cfg_node_addr_width + cfg_reg_addr_width;
   
   // width of control register data
   parameter cfg_data_width = 32;
   
   // base address width for interface bus
   localparam io_node_addr_width = cfg_node_addr_width;
   
   // register index width for interface bus
   localparam io_addr_suffix_width = cfg_reg_addr_width;
   
   // width of interface bus addresses
   localparam io_addr_width = cfg_addr_width;
   
   // width of interface bus datapath
   localparam io_data_width = cfg_data_width;
   
   // number of bits in delay counter for acknowledgement (i.e., log2 of 
   // interval before acknowledgement is sent)
   parameter done_delay_width = 6;
   
   // number of node control signals to generate
   localparam node_ctrl_width = 2 + 2;
   
   // number of node status signals to accept
   localparam node_status_width = 1;
   
   // select set of feedback polynomials used for LFSRs
   parameter lfsr_index = 0;
   
   // number of bits used for each LFSR (one per channel bit)
   parameter lfsr_width = 16;
   
   // width of the channel (in bits)
   parameter channel_width = 16;
   
   // number of cycles it takes data to traverse the channel
   parameter channel_latency = 2;
   
   // number of bits required for selecting one of the channel bit LFSRs
   localparam pattern_addr_width = clogb(channel_width);
   
   // number of bits used for specifying overall test duration
   parameter test_duration_width = 32;
   
   // number of bits used for specifying warmup duration
   parameter warmup_duration_width = 32;
   
   // number of bits used for specifying calibration interval
   parameter cal_interval_width = 16;
   
   // number of bits used for specifying calibration duration
   parameter cal_duration_width = 8;
   
   // bit error rate (errors per 10000 bits)
   parameter error_rate = 1;
   
   // RNG seed value
   parameter initial_seed = 0;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   reg clk;
   reg reset;
   
   // base address for this router
   reg [0:io_node_addr_width-1] io_node_addr_base;
   
   // write request indicator from chip controller
   reg 				io_write;
   
   // read request indicator from chip controller
   reg 				io_read;
   
   // register address
   reg [0:io_addr_width-1] 	io_addr;
   
   // input data
   reg [0:io_data_width-1] 	io_write_data;
   
   // result data to chip controller
   wire [0:io_data_width-1] 	io_read_data;
   
   // completion indicator to chip controller
   wire 			io_done;
   
   // summary error indicator
   wire 			error;
   
   wire [0:cfg_node_addr_width-1] nctl_cfg_node_addrs;
   assign nctl_cfg_node_addrs = io_node_addr_base;
   
   wire 			  cfg_req;
   wire 			  cfg_write;
   wire [0:cfg_addr_width-1] 	  cfg_addr;
   wire [0:cfg_data_width-1] 	  cfg_write_data;
   wire [0:cfg_data_width-1] 	  cfg_read_data;
   wire 			  cfg_done;
   
   wire [0:node_ctrl_width-1] 	  node_ctrl;
   wire [0:node_status_width-1]   node_status;
   
   tc_node_ctrl_mac
     #(.cfg_node_addr_width(cfg_node_addr_width),
       .cfg_reg_addr_width(cfg_reg_addr_width),
       .num_cfg_node_addrs(1),
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
      .cfg_node_addrs(nctl_cfg_node_addrs),
      .cfg_req(cfg_req),
      .cfg_write(cfg_write),
      .cfg_addr(cfg_addr),
      .cfg_write_data(cfg_write_data),
      .cfg_read_data(cfg_read_data),
      .cfg_done(cfg_done),
      .node_ctrl(node_ctrl),
      .node_status(node_status));
   
   wire 			  force_node_reset_b;
   assign force_node_reset_b = node_ctrl[0];
   
   wire 			  node_reset;
   assign node_reset = reset | ~force_node_reset_b;
   
   wire 			  node_clk_en;
   assign node_clk_en = node_ctrl[1];
   
   wire 			  node_clk;
   assign node_clk = clk & node_clk_en;
   
   wire 			  force_chan_reset_b;
   assign force_chan_reset_b = node_ctrl[2];
   
   wire 			  chan_reset;
   assign chan_reset = reset | ~force_chan_reset_b;
   
   wire 			  chan_clk_en;
   assign chan_clk_en = node_ctrl[3];
   
   wire 			  chan_clk;
   assign chan_clk = clk & chan_clk_en;
   
   wire [0:cfg_node_addr_width-1] ctest_cfg_node_addrs;
   assign ctest_cfg_node_addrs = 'd1;
   
   wire 			  xmit_cal;
   wire [0:channel_width-1] 	  xmit_data;
   
   wire 			  recv_cal;
   wire [0:channel_width-1] 	  recv_data;
   
   wire 			  ctest_error;
   
   tc_chan_test_mac
     #(.cfg_node_addr_width(cfg_node_addr_width),
       .cfg_reg_addr_width(cfg_reg_addr_width),
       .num_cfg_node_addrs(1),
       .cfg_data_width(cfg_data_width),
       .lfsr_index(lfsr_index),
       .lfsr_width(lfsr_width),
       .channel_width(channel_width),
       .test_duration_width(test_duration_width),
       .warmup_duration_width(warmup_duration_width),
       .cal_interval_width(cal_interval_width),
       .cal_duration_width(cal_duration_width),
       .reset_type(reset_type))
   ctest
     (.clk(node_clk),
      .reset(node_reset),
      .cfg_node_addrs(ctest_cfg_node_addrs),
      .cfg_req(cfg_req),
      .cfg_write(cfg_write),
      .cfg_addr(cfg_addr),
      .cfg_write_data(cfg_write_data),
      .cfg_read_data(cfg_read_data),
      .cfg_done(cfg_done),
      .xmit_cal(xmit_cal),
      .xmit_data(xmit_data),
      .recv_cal(recv_cal),
      .recv_data(recv_data),
      .error(ctest_error));
   
   assign node_status[0] = ctest_error;
   assign error = ctest_error;
   
   channel
     #(.channel_width(channel_width),
       .error_rate(error_rate),
       .initial_seed(initial_seed),
       .reset_type(reset_type))
   chan
     (.clk(clk),
      .reset(reset),
      .xmit_cal(xmit_cal),
      .xmit_data(xmit_data),
      .recv_cal(recv_cal),
      .recv_data(recv_data));
   
   reg 				clk_en;
   
   always
   begin
      clk <= clk_en;
      #(Tclk/2);
      clk <= 1'b0;
      #(Tclk/2);
   end
   
   reg done;
   integer i, j;
   integer seed = initial_seed;
   
   initial
   begin
      
      reset = 1'b0;
      clk_en = 1'b0;
      io_node_addr_base = 'd0;
      
      #(Tclk);
      
      #(Tclk/4);
      
      reset = 1'b1;
      io_write = 1'b0;
      io_read = 1'b0;
      io_addr = 'b0;
      io_write_data = 'b0;
      done = 1'b0;
      
      #(Tclk);
      
      reset = 1'b0;
      
      #(Tclk);
      
      clk_en = 1'b1;
      
      #(Tclk);
      
      
      // disable reset (i.e., enable reset_b)
      
      io_write = 1'b1;
      io_addr[0:cfg_node_addr_width-1] = 'd0;
      io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd0;
      io_write_data = 'd0;
      io_write_data[0] = 1'b1;
      io_write_data[2] = 1'b1;
      while(!io_done)
	#(Tclk);
      #(Tclk);
      io_write = 1'b0;
      while(io_done)
	#(Tclk);
      
      #(Tclk);
      
      
      // enable clocks
      
      io_write = 1'b1;
      io_addr[0:cfg_node_addr_width-1] = 'd0;
      io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd0;
      io_write_data = 'd0;
      io_write_data[0] = 1'b1;
      io_write_data[1] = 1'b1;
      io_write_data[2] = 1'b1;
      io_write_data[3] = 1'b1;
      while(!io_done)
	#(Tclk);
      #(Tclk);
      io_write = 1'b0;
      while(io_done)
	#(Tclk);
      
      #(Tclk);
      
      
      // set test duration
      
      io_write = 1'b1;
      io_addr[0:cfg_node_addr_width-1] = 'd1;
      io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd3;
      io_write_data = 'd0;
      io_write_data[0:test_duration_width-1] = 'd16384;
      while(!io_done)
	#(Tclk);
      #(Tclk);
      io_write = 1'b0;
      while(io_done)
	#(Tclk);
      
      #(Tclk);
      
      
      // set warmup duration
      
      io_write = 1'b1;
      io_addr[0:cfg_node_addr_width-1] = 'd1;
      io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd4;
      io_write_data = 'd0;
      io_write_data[0:warmup_duration_width-1] = 'd1024;
      while(!io_done)
	#(Tclk);
      #(Tclk);
      io_write = 1'b0;
      while(io_done)
	#(Tclk);
      
      #(Tclk);
      
      
      // set calibration interval
      
      io_write = 1'b1;
      io_addr[0:cfg_node_addr_width-1] = 'd1;
      io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd5;
      io_write_data = 'd0;
      io_write_data[0:cal_interval_width-1] = 'd128;
      while(!io_done)
	#(Tclk);
      #(Tclk);
      io_write = 1'b0;
      while(io_done)
	#(Tclk);
      
      #(Tclk);
      
      
      // set calibration duration
      
      io_write = 1'b1;
      io_addr[0:cfg_node_addr_width-1] = 'd1;
      io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd6;
      io_write_data = 'd0;
      io_write_data[0:cal_duration_width-1] = 'd16;
      while(!io_done)
	#(Tclk);
      #(Tclk);
      io_write = 1'b0;
      while(io_done)
	#(Tclk);
      
      #(Tclk);
      
      
      // set LFSR seeds
      
      for(j = 0; j < channel_width; j = j + 1)
	begin
	   
	   // select register
	   
	   io_write = 1'b1;
	   io_addr[0:cfg_node_addr_width-1] = 1;
	   io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd7;
	   io_write_data = 'd0;
	   io_write_data[0:pattern_addr_width-1] = j;
	   while(!io_done)
	     #(Tclk);
	   #(Tclk);
	   io_write = 1'b0;
	   while(io_done)
	     #(Tclk);
	   
	   #(Tclk);
	   
	   
	   // write seed to register
	   
	   io_write = 1'b1;
	   io_addr[0:cfg_node_addr_width-1] = 1;
	   io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd8;
	   io_write_data = 'd0;
	   for(i = 0; i < lfsr_width; i = i + 1)
	     io_write_data[i] = $dist_uniform(seed, 0, 1);
	   while(!io_done)
	     #(Tclk);
	   #(Tclk);
	   io_write = 1'b0;
	   while(io_done)
	     #(Tclk);
	   
	end
      
      #(10*Tclk);
      
      
      // start experiment
      
      io_write = 1'b1;
      io_addr[0:cfg_node_addr_width-1] = 1;
      io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd0;
      io_write_data = 'd0;
      io_write_data[0] = 'd1;
      io_write_data[1] = 'd0;
      io_write_data[2] = 'd1;
      while(!io_done)
	#(Tclk);
      #(Tclk);
      io_write = 1'b0;
      while(io_done)
	#(Tclk);
      
      
      // wait for experiment to finish
      
      while(!done)
	begin
	   
	   #(10*Tclk);
	   
	   io_read = 1'b1;
	   io_addr[0:cfg_node_addr_width-1] = 1;
	   io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd1;
	   io_write_data = 'd0;
	   while(!io_done)
	     #(Tclk);
	   done = ~io_read_data[0];
	   #(Tclk);
	   io_read = 1'b0;
	   while(io_done)
	     #(Tclk);
	   
	end
      done = 1'b0;
      
      #(Tclk);
      
      
      // disable nodes
      
      io_write = 1'b1;
      io_addr[0:cfg_node_addr_width-1] = 1;
      io_addr[cfg_node_addr_width:cfg_addr_width-1] = 'd0;
      io_write_data = 'd0;
      while(!io_done)
	#(Tclk);
      #(Tclk);
      io_write = 1'b0;
      while(io_done)
	#(Tclk);
      
      #(3*Tclk/4);
      
      #(Tclk);
      
      $finish;
      
   end
   
endmodule
