// $Id: c_pseudo_sram_mac.v 1833 2010-03-22 23:18:22Z dub $

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



// register file wrapper that implements SRAM-like timing
module c_pseudo_sram_mac
  (clk, reset, write_enable, write_address, write_data, read_enable, 
   read_address, read_data);
   
`include "c_functions.v"
`include "c_constants.v"
   
   // number of entries
   parameter depth = 8;
   
   // width of each entry
   parameter width = 64;
   
   // select implementation variant
   parameter regfile_type = `REGFILE_TYPE_FF_2D;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   // width required to swelect an entry
   localparam addr_width = clogb(depth);
   
   input clk;
   input reset;
   
   // if high, write to entry selected by write_address
   input write_enable;
   
   // entry to be written to
   input [0:addr_width-1] write_address;
   
   // data to be written
   input [0:width-1] write_data;
   
   // if high, enable read for next cycle
   input read_enable;
   
   // entry to read out
   input [0:addr_width-1] read_address;
   
   // contents of entry selected by read_address
   output [0:width-1] read_data;
   wire [0:width-1] read_data;
   
   // make read timing behave like SRAM (read address selects entry to be read 
   // out in next cycle)
   wire [0:addr_width-1] read_address_s, read_address_q;
   assign read_address_s = read_enable ? read_address : read_address_q;
   c_dff
     #(.width(addr_width),
       .reset_type(reset_type))
   read_addrq
     (.clk(clk),
      .reset(reset),
      .d(read_address_s),
      .q(read_address_q));
   
   c_regfile
     #(.depth(depth),
       .width(width),
       .regfile_type(regfile_type))
   rf
     (.clk(clk),
      .write_enable(write_enable),
      .write_address(write_address),
      .write_data(write_data),
      .read_address(read_address_q),
      .read_data(read_data));
   
endmodule
