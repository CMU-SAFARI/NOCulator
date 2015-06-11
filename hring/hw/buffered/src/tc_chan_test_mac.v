// $Id: tc_chan_test_mac.v 1833 2010-03-22 23:18:22Z dub $

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

// network channel tester
module tc_chan_test_mac
  (clk, reset, cfg_node_addrs, cfg_req, cfg_write, cfg_addr, cfg_write_data, 
   cfg_read_data, cfg_done, xmit_cal, xmit_data, recv_cal, recv_data, error);
   
// register address declarations
`define CFG_ADDR_CTRL            0
`define CFG_ADDR_STATUS          1
`define CFG_ADDR_ERRORS          2
`define CFG_ADDR_TEST_DURATION   3
`define CFG_ADDR_WARMUP_DURATION 4
`define CFG_ADDR_CAL_INTERVAL    5
`define CFG_ADDR_CAL_DURATION    6
`define CFG_ADDR_PATTERN_ADDR    7
`define CFG_ADDR_PATTERN_DATA    8
   
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
   
   // select set of feedback polynomials used for LFSRs
   parameter lfsr_index = 0;
   
   // number of bits used for each LFSR (one per channel bit)
   parameter lfsr_width = 16;
   
   // width of the channel (in bits)
   parameter channel_width = 16;
   
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
   
   // calibration enable for transmit unit
   output xmit_cal;
   wire 		      xmit_cal;
   
   // data to transmit over the channel
   output [0:channel_width-1] xmit_data;
   wire [0:channel_width-1]   xmit_data;
   
   // calibration enable for receive unit
   output recv_cal;
   wire 		      recv_cal;
   
   // data received from the channel
   input [0:channel_width-1] recv_data;
   
   // internal error condition detected
   output error;
   wire 		      error;
   
   
   //---------------------------------------------------------------------------
   // configuration register interface
   //---------------------------------------------------------------------------
   
   wire 		      ifc_active;
   wire 		      ifc_req;
   wire 		      ifc_write;
   wire [0:num_cfg_node_addrs-1] ifc_node_addr_match;
   wire [0:cfg_reg_addr_width-1] ifc_reg_addr;
   wire [0:cfg_data_width-1] 	 ifc_write_data;
   wire [0:cfg_data_width-1] 	 ifc_read_data;
   wire 			 ifc_done;
   
   tc_cfg_bus_ifc
     #(.cfg_node_addr_width(cfg_node_addr_width),
       .cfg_reg_addr_width(cfg_reg_addr_width),
       .num_cfg_node_addrs(num_cfg_node_addrs),
       .cfg_data_width(cfg_data_width),
       .reset_type(reset_type))
   ifc
     (.clk(clk),
      .reset(reset),
      .cfg_node_addrs(cfg_node_addrs),
      .cfg_req(cfg_req),
      .cfg_write(cfg_write),
      .cfg_addr(cfg_addr),
      .cfg_write_data(cfg_write_data),
      .cfg_read_data(cfg_read_data),
      .cfg_done(cfg_done),
      .active(ifc_active),
      .req(ifc_req),
      .write(ifc_write),
      .node_addr_match(ifc_node_addr_match),
      .reg_addr(ifc_reg_addr),
      .write_data(ifc_write_data),
      .read_data(ifc_read_data),
      .done(ifc_done));
   
   wire 			 ifc_sel_node;
   assign ifc_sel_node = |ifc_node_addr_match;
   
   wire 			 do_write;
   assign do_write = ifc_req & ifc_write;
   
   // all registers in the pseudo-node are read immediately
   assign ifc_done = ifc_req & ifc_sel_node;
   
   wire 			 ifc_sel_ctrl;
   assign ifc_sel_ctrl
     =  ifc_sel_node && (ifc_reg_addr == `CFG_ADDR_CTRL);
   
   wire 			 ifc_sel_status;
   assign ifc_sel_status
     = ifc_sel_node && (ifc_reg_addr == `CFG_ADDR_STATUS);
   
   wire 			 ifc_sel_errors;
   assign ifc_sel_errors
     = ifc_sel_node && (ifc_reg_addr == `CFG_ADDR_ERRORS);
   
   wire 			 ifc_sel_test_duration;
   assign ifc_sel_test_duration
     = ifc_sel_node && (ifc_reg_addr == `CFG_ADDR_TEST_DURATION);
   
   wire 			 ifc_sel_warmup_duration;
   assign ifc_sel_warmup_duration
     = ifc_sel_node && (ifc_reg_addr == `CFG_ADDR_WARMUP_DURATION);
   
   wire 			 ifc_sel_cal_interval;
   assign ifc_sel_cal_interval
     = ifc_sel_node && (ifc_reg_addr == `CFG_ADDR_CAL_INTERVAL);
   
   wire 			 ifc_sel_cal_duration;
   assign ifc_sel_cal_duration
     = ifc_sel_node && (ifc_reg_addr == `CFG_ADDR_CAL_DURATION);
   
   wire 			 ifc_sel_pattern_addr;
   assign ifc_sel_pattern_addr
     = ifc_sel_node && (ifc_reg_addr == `CFG_ADDR_PATTERN_ADDR);
   
   wire 			 ifc_sel_pattern_data;
   assign ifc_sel_pattern_data
     = ifc_sel_node && (ifc_reg_addr == `CFG_ADDR_PATTERN_DATA);
   
   wire [0:cfg_data_width-1] 	 ifc_read_data_status;
   wire [0:cfg_data_width-1] 	 ifc_read_data_errors;
   
   c_select_mofn
     #(.num_ports(2),
       .width(cfg_data_width))
   ifc_read_data_sel
     (.select({ifc_sel_status, 
	       ifc_sel_errors}),
      .data_in({ifc_read_data_status, 
		ifc_read_data_errors}),
      .data_out(ifc_read_data));
   
   
   //---------------------------------------------------------------------------
   // control register
   // ================
   // 
   //---------------------------------------------------------------------------
   
   wire 			 write_ctrl;
   assign write_ctrl = do_write & ifc_sel_ctrl;
   
   wire 			 active_s, active_q;
   assign active_s
     = ifc_active ? (write_ctrl ? ifc_write_data[0] : active_q) : active_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   activeq
     (.clk(clk),
      .reset(reset),
      .d(active_s),
      .q(active_q));
   
   wire 			 preset_s, preset_q;
   assign preset_s
     = ifc_active ? (write_ctrl ? ifc_write_data[1] : preset_q) : preset_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   presetq
     (.clk(clk),
      .reset(reset),
      .d(preset_s),
      .q(preset_q));
   
   wire 			 cal_en_s, cal_en_q;
   assign cal_en_s
     = ifc_active ? (write_ctrl ? ifc_write_data[2] : cal_en_q) : cal_en_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cal_enq
     (.clk(clk),
      .reset(reset),
      .d(cal_en_s),
      .q(cal_en_q));
   
   wire 			 start_s, start_q;
   assign start_s = ifc_active ? (write_ctrl & ifc_write_data[0]) : start_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   startq
     (.clk(clk),
      .reset(reset),
      .d(start_s),
      .q(start_q));
   
   
   //---------------------------------------------------------------------------
   // status register
   // ===============
   // 
   //---------------------------------------------------------------------------
   
   wire 			 test_duration_zero;
   
   wire 			 running_s, running_q;
   assign running_s = active_q ? 
		      ((running_q & ~test_duration_zero) | start_q) : 
		      running_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   runningq
     (.clk(clk),
      .reset(1'b0),
      .d(running_s),
      .q(running_q));
   
   c_align
     #(.data_width(2),
       .dest_width(cfg_data_width))
   ifc_read_data_status_alg
     (.data_in({running_q, error}),
      .dest_in({cfg_data_width{1'b0}}),
      .data_out(ifc_read_data_status));
   
   
   //---------------------------------------------------------------------------
   // error register
   // ==============
   // 
   //---------------------------------------------------------------------------
   
   wire 			 ignore_errors;
   
   wire [0:channel_width-1] 	 errors;
   
   wire [0:channel_width-1] 	 errors_s, errors_q;
   assign errors_s = active_q ? 
		     ((errors_q | (errors & {channel_width{~ignore_errors}})) & 
		      {channel_width{~start_q}}) : 
		     errors_q;
   c_dff
     #(.width(channel_width),
       .reset_type(reset_type))
   errorsq
     (.clk(clk),
      .reset(1'b0),
      .d(errors_s),
      .q(errors_q));
   
   assign error = |errors_q;
   
   c_align
     #(.data_width(channel_width),
       .dest_width(cfg_data_width))
   ifc_read_data_errors_alg
     (.data_in(errors_q),
      .dest_in({cfg_data_width{1'b0}}),
      .data_out(ifc_read_data_errors));
   
   
   //---------------------------------------------------------------------------
   // test duration register
   // =====================
   // 
   //---------------------------------------------------------------------------
   
   wire [0:test_duration_width-1] test_duration_loadval;
   c_align
     #(.data_width(cfg_data_width),
       .dest_width(test_duration_width))
   test_duration_loadval_alg
     (.data_in(ifc_write_data),
      .dest_in({test_duration_width{1'b0}}),
      .data_out(test_duration_loadval));
   
   wire 			  test_duration_inf;
   wire [0:test_duration_width-1] test_duration_q;
   
   wire [0:test_duration_width-1] test_duration_next;
   assign test_duration_next
     = (~active_q | test_duration_inf | test_duration_zero) ? 
       test_duration_q : 
       (test_duration_q - 1'b1);
   
   wire 			  write_test_duration;
   assign write_test_duration = do_write & ifc_sel_test_duration;
   
   wire [0:test_duration_width-1] test_duration_s;
   assign test_duration_s = (ifc_active | active_q) ? 
			    (write_test_duration ? 
			     test_duration_loadval : 
			     test_duration_next) : 
			    test_duration_q;
   c_dff
     #(.width(test_duration_width),
       .reset_type(reset_type))
   test_durationq
     (.clk(clk),
      .reset(1'b0),
      .d(test_duration_s),
      .q(test_duration_q));
   
   assign test_duration_inf = &test_duration_q;
   assign test_duration_zero = ~|test_duration_q;
   
   
   //---------------------------------------------------------------------------
   // warmup duration register
   // ========================
   // 
   //---------------------------------------------------------------------------
   
   wire [0:warmup_duration_width-1] warmup_duration_loadval;
   c_align
     #(.data_width(cfg_data_width),
       .dest_width(warmup_duration_width))
   warmup_duration_loadval_alg
     (.data_in(ifc_write_data),
      .dest_in({warmup_duration_width{1'b0}}),
      .data_out(warmup_duration_loadval));
   
   wire 			    warmup_duration_zero;
   wire [0:warmup_duration_width-1] warmup_duration_q;
   
   wire [0:warmup_duration_width-1] warmup_duration_next;
   assign warmup_duration_next = (~active_q  | warmup_duration_zero) ? 
				 warmup_duration_q : 
				 (warmup_duration_q - 1'b1);
   
   wire 			    write_warmup_duration;
   assign write_warmup_duration = do_write & ifc_sel_warmup_duration;
   
   wire [0:warmup_duration_width-1] warmup_duration_s;
   assign warmup_duration_s = (ifc_active | active_q) ? 
			      (write_warmup_duration ? 
			       warmup_duration_loadval : 
			       warmup_duration_next) : 
			      warmup_duration_q;
   c_dff
     #(.width(warmup_duration_width),
       .reset_type(reset_type))
   warmup_durationq
     (.clk(clk),
      .reset(1'b0),
      .d(warmup_duration_s),
      .q(warmup_duration_q));
   
   assign warmup_duration_zero = ~|warmup_duration_q;
   
   
   //---------------------------------------------------------------------------
   // calibration interval register
   // =============================
   // 
   //---------------------------------------------------------------------------
   
   wire [0:cal_interval_width-1] cal_interval_loadval;
   c_align
     #(.data_width(cfg_data_width),
       .dest_width(cal_interval_width))
   cal_interval_loadval_alg
     (.data_in(ifc_write_data),
      .dest_in({cal_interval_width{1'b0}}),
      .data_out(cal_interval_loadval));
   
   wire 			 write_cal_interval;
   assign write_cal_interval = do_write & ifc_sel_cal_interval;
   
   wire [0:cal_interval_width-1] cal_interval_s, cal_interval_q;
   assign cal_interval_s
     = ifc_active ? 
       (write_cal_interval ? cal_interval_loadval : cal_interval_q) : 
       cal_interval_q;
   c_dff
     #(.width(cal_interval_width),
       .reset_type(reset_type))
   cal_intervalq
     (.clk(clk),
      .reset(1'b0),
      .d(cal_interval_s),
      .q(cal_interval_q));
   
   
   //---------------------------------------------------------------------------
   // calibration duration register
   // =============================
   // 
   //---------------------------------------------------------------------------
   
   wire [0:cal_duration_width-1] cal_duration_loadval;
   c_align
     #(.data_width(cfg_data_width),
       .dest_width(cal_duration_width))
   cal_duration_loadval_alg
     (.data_in(ifc_write_data),
      .dest_in({cal_duration_width{1'b0}}),
      .data_out(cal_duration_loadval));
   
   wire 			 write_cal_duration;
   assign write_cal_duration = do_write & ifc_sel_cal_duration;
   
   wire [0:cal_duration_width-1] cal_duration_s, cal_duration_q;
   assign cal_duration_s
     = ifc_active ? 
       (write_cal_duration ? cal_duration_loadval : cal_duration_q) : 
       cal_duration_q;
   c_dff
     #(.width(cal_duration_width),
       .reset_type(reset_type))
   cal_durationq
     (.clk(clk),
      .reset(1'b0),
      .d(cal_duration_s),
      .q(cal_duration_q));
   
   
   //---------------------------------------------------------------------------
   // pattern address register
   // ========================
   // 
   //---------------------------------------------------------------------------
   
   wire [0:pattern_addr_width-1] pattern_addr_loadval;
   c_align
     #(.data_width(cfg_data_width),
       .dest_width(pattern_addr_width))
   pattern_addr_loadval_alg
     (.data_in(ifc_write_data),
      .dest_in({pattern_addr_width{1'b0}}),
      .data_out(pattern_addr_loadval));
   
   wire 			 write_pattern_addr;
   assign write_pattern_addr = do_write & ifc_sel_pattern_addr;
   
   wire [0:pattern_addr_width-1] pattern_addr_s, pattern_addr_q;
   assign pattern_addr_s
     = ifc_active ? 
       (write_pattern_addr ? pattern_addr_loadval : pattern_addr_q) : 
       pattern_addr_q;
   c_dff
     #(.width(pattern_addr_width),
       .reset_type(reset_type))
   pattern_addrq
     (.clk(clk),
      .reset(1'b0),
      .d(pattern_addr_s),
      .q(pattern_addr_q));
   
   
   //---------------------------------------------------------------------------
   // calibration signal generation
   //---------------------------------------------------------------------------
   
   wire [0:cal_interval_width-1] cal_ctr_q;
   
   wire 			 cal_ctr_max;
   assign cal_ctr_max = (cal_ctr_q == cal_interval_q);
   
   wire [0:cal_interval_width-1] cal_ctr_next;
   assign cal_ctr_next
     = (cal_ctr_q + 1'b1) & {cal_interval_width{~cal_ctr_max & ~start_q}};
   
   wire [0:cal_interval_width-1] cal_ctr_s;
   assign cal_ctr_s = active_q ? cal_ctr_next : cal_ctr_q;
   c_dff
     #(.width(cal_interval_width),
       .reset_type(reset_type))
   cal_ctrq
     (.clk(clk),
      .reset(1'b0),
      .d(cal_ctr_s),
      .q(cal_ctr_q));
   
   wire 			 cal_ctr_zero;
   assign cal_ctr_zero = ~|cal_ctr_q;
   
   wire [0:cal_interval_width-1] cal_duration_ext;
   c_align
     #(.data_width(cal_duration_width),
       .dest_width(cal_interval_width),
       .offset(cal_interval_width-cal_duration_width))
   cal_duration_ext_alg
     (.data_in(cal_duration_q),
      .dest_in({cal_interval_width{1'b0}}),
      .data_out(cal_duration_ext));
   
   wire [0:cal_interval_width-1] cal_duration_minus_one;
   assign cal_duration_minus_one = cal_duration_ext - 1'b1;
   
   wire [0:cal_interval_width-1] cal_duration_minus_two;
   assign cal_duration_minus_two
     = {cal_duration_ext[0:cal_interval_width-2] - 1'b1, 
	cal_duration_ext[cal_interval_width-1]};
   
   wire 			 tx_cal_set;
   assign tx_cal_set = cal_ctr_max | start_q;
   
   wire 			 tx_cal_reset;
   assign tx_cal_reset
     = ((cal_ctr_q == cal_duration_minus_one) && !start_q) || 
       test_duration_zero || ~cal_en_q;
   
   wire 			 tx_cal_s, tx_cal_q;
   assign tx_cal_s
     = active_q ? ((tx_cal_q | tx_cal_set) & ~tx_cal_reset) : tx_cal_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   tx_calq
     (.clk(clk),
      .reset(reset),
      .d(tx_cal_s),
      .q(tx_cal_q));
   
   assign xmit_cal = tx_cal_q;
   
   wire 			 invalid_data_s, invalid_data_q;
   assign invalid_data_s = active_q ? tx_cal_q : invalid_data_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   invalid_dataq
     (.clk(clk),
      .reset(reset),
      .d(invalid_data_s),
      .q(invalid_data_q));
   
   wire 			 rx_cal_set;
   assign rx_cal_set = cal_ctr_zero & ~start_q;
   
   wire 			 rx_cal_reset;
   assign rx_cal_reset
     = ((cal_ctr_q == cal_duration_minus_two) && !start_q) || 
       test_duration_zero || ~cal_en_q;
   
   wire 			 rx_cal_s, rx_cal_q;
   assign rx_cal_s
     = active_q ? ((rx_cal_q | rx_cal_set) & ~rx_cal_reset) : rx_cal_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   rx_calq
     (.clk(clk),
      .reset(reset),
      .d(rx_cal_s),
      .q(rx_cal_q));
   
   assign recv_cal = rx_cal_q;
   
   
   //---------------------------------------------------------------------------
   // pattern address registers
   // =========================
   // 
   //---------------------------------------------------------------------------
   
   wire [0:lfsr_width-1] 	 pattern_data_loadval;
   c_align
     #(.data_width(cfg_data_width),
       .dest_width(lfsr_width))
   pattern_data_loadval_alg
     (.data_in(ifc_write_data),
      .dest_in({lfsr_width{1'b0}}),
      .data_out(pattern_data_loadval));
   
   wire 			 write_pattern_data;
   assign write_pattern_data = do_write & ifc_sel_pattern_data;
   
   genvar 			 idx;
   
   generate
      
      for(idx = 0; idx < channel_width; idx = idx + 1)
	begin:idxs
	   
	   wire [0:lfsr_width-1] pattern_data_rv_feedback;
	   c_fbgen
	     #(.width(lfsr_width),
	       .index(channel_width*lfsr_index+idx))
	   pattern_data_rv_feedback_fbgen
	     (.feedback(pattern_data_rv_feedback));
	   
	   wire [0:lfsr_width-1] pattern_data_rv_feedback_muxed;
	   assign pattern_data_rv_feedback_muxed[0]
		    = pattern_data_rv_feedback[0] | preset_q;
	   assign pattern_data_rv_feedback_muxed[1:lfsr_width-1]
		    = pattern_data_rv_feedback[1:lfsr_width-1] & 
		      {(lfsr_width-1){~preset_q}};
	   
	   wire 		 write_pattern_data_rv;
	   assign write_pattern_data_rv
	     = write_pattern_data && (pattern_addr_q == idx);
	   
	   wire [0:lfsr_width-1] pattern_data_rv_s, pattern_data_rv_q;
	   assign pattern_data_rv_s
	     = {lfsr_width{write_pattern_data_rv}} & pattern_data_loadval;
	   c_lfsr
	     #(.width(lfsr_width),
	       .iterations(1),
	       .reset_type(reset_type))
	   pattern_data_rv_lfsr
	     (.clk(clk),
	      .reset(1'b0),
	      .load(write_pattern_data_rv),
	      .run(active_q),
	      .feedback(pattern_data_rv_feedback_muxed),
	      .complete(1'b0),
	      .d(pattern_data_rv_s),
	      .q(pattern_data_rv_q));
	   
	   assign xmit_data[idx] = pattern_data_rv_q[lfsr_width-1];
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // generate reference data and compare
   //---------------------------------------------------------------------------
   
   wire [0:channel_width-1] 	 ref_data;
   c_shift_reg
     #(.width(channel_width),
       .depth(2),
       .reset_type(reset_type))
   ref_data_sr
     (.clk(clk),
      .reset(1'b0),
      .enable(active_q),
      .data_in(xmit_data),
      .data_out(ref_data));
   
   assign errors = recv_data ^ ref_data;
   
   assign ignore_errors = start_q | ~warmup_duration_zero | invalid_data_q;
   
endmodule
