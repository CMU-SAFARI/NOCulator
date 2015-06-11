// $Id: c_credit_tracker.v 2061 2010-05-31 05:10:37Z dub $

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



// module for tracking available credit in a buffer
module c_credit_tracker
  (clk, reset, debit, credit, free, errors);
   
`include "c_functions.v"
`include "c_constants.v"
   
   // total number of credits available
   parameter num_credits = 8;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   // width required to select an individual credit
   localparam credit_idx_width = clogb(num_credits);
   
   // width required to represent full range of credit count (0..num_credits)
   localparam true_idx_width = clogb(num_credits+1);
   
   
   input clk;
   input reset;
   
   // inputs for subtracting and adding credits
   input debit;
   input credit;
   
   // at least one credit available?
   output free;
   wire free;
   
   // internal error condition encountered
   output [0:1] errors;
   wire [0:1] errors;
   
   wire set_cred, reset_cred;
   wire cred_s, cred_q;
   assign cred_s = set_cred | (cred_q & ~reset_cred);
   c_dff
     #(.width(1),
       .reset_type(reset_type),
       .reset_value(1'b1))
   credq
     (.clk(clk),
      .reset(reset),
      .d(cred_s),
      .q(cred_q));
   
   assign free = cred_q;
   
   wire incr;
   assign incr = credit & ~debit;

   wire decr;
   assign decr = debit & ~credit;

   wire error_underflow;
   wire error_overflow;
   wire [0:true_idx_width-1] true_count;
   
   generate
      
      // if we only have a single credit, we don't need explicit credit counter
      if(num_credits == 1)
	begin
	   
	   assign set_cred = incr;
	   assign reset_cred = decr;

	   assign error_underflow = ~cred_q & debit;
	   assign error_overflow = cred_q & credit;
	   assign true_count = cred_q;
	   
	end

      // if we have more than one credit, we need an extra counter to keep track
      else
	begin
	   
	   wire [0:credit_idx_width-1] cred_count_q;
	   wire 		       cred_count_zero;
	   assign cred_count_zero = ~|cred_count_q;
	   
	   wire 		       incr_count;
	   assign incr_count = incr & ~(~cred_q & cred_count_zero);
	   
	   wire 		       decr_count;
	   assign decr_count = decr & ~(cred_q & cred_count_zero);
	   
	   wire [0:credit_idx_width-1] cred_count_s;
	   assign cred_count_s
	     = (incr | decr) ?
	       ((cred_count_q - decr_count) + incr_count) :
	       cred_count_q;
	   c_dff
	     #(.width(credit_idx_width),
	       .reset_type(reset_type),
	       .reset_value(num_credits-1))
	   cred_countq
	     (.clk(clk),
	      .reset(reset),
	      .d(cred_count_s),
	      .q(cred_count_q));
	   
	   assign set_cred = incr & ~cred_q & cred_count_zero;
	   assign reset_cred = decr & cred_q & cred_count_zero;
	   
	   assign error_underflow
	     = !cred_q && (cred_count_q == 0) && debit;
	   assign error_overflow
	     = cred_q && (cred_count_q == (num_credits - 1)) && credit;
	   assign true_count = cred_count_q + cred_q;
	   
	end
      
      // synopsys translate_off
      if(num_credits == 0)
	begin
	   initial
	   begin
	      $display({"ERROR: Credit tracker module %m requires at least ", 
			"one credit."});
	      $stop;
	   end
	end
      // synopsys translate_on
      
   endgenerate
   
   assign errors = {error_underflow, error_overflow};
   
endmodule
