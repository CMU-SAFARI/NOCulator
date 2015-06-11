// $Id: c_fifo_ctrl.v 1922 2010-04-15 03:47:49Z dub $

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



// simple FIFO controller
module c_fifo_ctrl
  (clk, reset, push, pop, write_addr, read_addr, almost_empty, empty, 
   almost_full, full, errors);
   
`include "c_constants.v"
   
   // address width
   parameter addr_width = 3;
   
   // starting address (i.e., address of leftmost entry)
   parameter offset = 0;
   
   // number of entries in FIFO
   parameter depth = 8;
   
   // minimum (leftmost) address
   localparam [0:addr_width-1] min_value = offset;
   
   // maximum (rightmost) address
   localparam [0:addr_width-1] max_value = offset + depth - 1;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // write (add) an element
   input push;
   
   // read (remove) an element
   input pop;
   
   // address to write current input element to
   output [0:addr_width-1] write_addr;
   wire [0:addr_width-1] write_addr;
   
   // address to read next output element from
   output [0:addr_width-1] read_addr;
   wire [0:addr_width-1] read_addr;
   
   // buffer nearly empty (1 used slot remaining) indication
   output almost_empty;
   wire 		 almost_empty;
   
   // buffer empty indication
   output empty;
   wire 		 empty;
   
   // buffer almost full (1 unused slot remaining) indication
   output almost_full;
   wire 		 almost_full;
   
   // buffer full indication
   output full;
   wire 		 full;
   
   // internal error condition detected
   output [0:1] errors;
   wire [0:1] 		 errors;
   
   wire [0:addr_width-1] read_ptr_next, read_ptr_q;
   c_incr
     #(.width(addr_width),
       .min_value(min_value),
       .max_value(max_value))
   read_ptr_incr
     (.data_in(read_ptr_q),
      .data_out(read_ptr_next));
   
   wire [0:addr_width-1] read_ptr_s;
   assign read_ptr_s = pop ? read_ptr_next : read_ptr_q;
   c_dff
     #(.width(addr_width),
       .reset_value(min_value),
       .reset_type(reset_type))
   read_ptrq
     (.clk(clk),
      .reset(reset),
      .d(read_ptr_s),
      .q(read_ptr_q));
   
   assign read_addr[0:addr_width-1] = read_ptr_q;
   
   wire [0:addr_width-1] write_ptr_next, write_ptr_q;
   c_incr
     #(.width(addr_width),
       .min_value(min_value),
       .max_value(max_value))
   write_ptr_incr
     (.data_in(write_ptr_q),
      .data_out(write_ptr_next));
   
   wire [0:addr_width-1]  write_ptr_s;
   assign write_ptr_s = push ? write_ptr_next : write_ptr_q;
   c_dff
     #(.width(addr_width),
       .reset_value(min_value),
       .reset_type(reset_type))
   write_ptrq
     (.clk(clk),
      .reset(reset),
      .d(write_ptr_s),
      .q(write_ptr_q));
   
   assign write_addr[0:addr_width-1] = write_ptr_q;
   
   assign almost_empty = (read_ptr_next == write_ptr_q);
   
   wire 		 empty_s, empty_q;
   assign empty_s = (empty_q | (almost_empty & pop & ~push)) & ~(push & ~pop);
   c_dff
     #(.width(1),
       .reset_type(reset_type),
       .reset_value(1'b1))
   emptyq
     (.clk(clk),
      .reset(reset),
      .d(empty_s),
      .q(empty_q));
   
   assign empty = empty_q;
   
   assign almost_full = (write_ptr_next == read_ptr_q);
   
   wire 		 full_s, full_q;
   assign full_s = (full_q | (almost_full & push & ~pop)) & ~(pop & ~push);
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   fullq
     (.clk(clk),
      .reset(reset),
      .d(full_s),
      .q(full_q));
   
   assign full = full_q;
   
   wire 		 error_underflow;
   assign error_underflow = empty & pop & ~push;
   
   wire 		 error_overflow;
   assign error_overflow = full & push & ~pop;
   
   assign errors = {error_underflow, error_overflow};
   
   // synopsys translate_off
   generate
      
      if(depth > (1 << addr_width))
	begin
	   initial
	   begin
	      $display({"ERROR: FIFO controller %m requires that addr_width ", 
			"be at least wide enough to cover the entire depth."});
	      $stop;
	   end
	end
      
   endgenerate
   // synopsys translate_on
   
endmodule
