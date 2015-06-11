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



// generic round-robin arbiter
module c_rr_arbiter
  (clk, reset, update, req, gnt);
   
`include "c_constants.v"
   
   // number of input ports
   parameter num_ports = 32;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // update priority vector
   input update;
   
   // vector of requests
   input [0:num_ports-1] req;
   
   // vector of grants
   output [0:num_ports-1] gnt;
   wire [0:num_ports-1] gnt;
   
   generate
      
      if(num_ports > 1)
	begin
	   
	   // reversed request vector
	   wire [0:num_ports-1] req_rev;
	   c_reversor
	     #(.width(num_ports))
	   req_rev_revr
	     (.data_in(req),
	      .data_out(req_rev));
	   
	   // current priority vector
	   wire [0:num_ports-2] prev_q;
	   
	   // unqualified grant (ignorign priority)
	   // This finds the first non-zero bit from the right (which is why we 
	   // reverse the request vector in the first place).
	   wire [0:num_ports-1] gnt_rev_unqual;
	   assign gnt_rev_unqual = req_rev & -req_rev;
	   
	   // mask all inputs to the right of current priority pointer
	   // FIXME: Can we reverse the order of prev_q to avoid having to 
	   // invert the mask later?
	   wire [0:num_ports-2] mask;
	   assign mask = prev_q - 1'b1;
	   
	   // generate qualified requests (w/ priority)
	   wire [0:num_ports-2] req_rev_qual;
	   assign req_rev_qual = req_rev[0:num_ports-2] & ~mask;
	   
	   // generate qualified grants (w/ priority)
	   // This finds the first non-zero bit from the right, not including
	   wire [0:num_ports-2] gnt_rev_qual;
	   assign gnt_rev_qual = req_rev_qual & -req_rev_qual;
	   
	   // reversed grant vector
	   wire [0:num_ports-1] gnt_rev;
	   assign gnt_rev
	     = |req_rev_qual ? {gnt_rev_qual, 1'b0} : gnt_rev_unqual;
	   
	   c_reversor
	     #(.width(num_ports))
	   gnt_revr
	     (.data_in(gnt_rev),
	      .data_out(gnt));
	   
	   // The worst-case effect of bit errors in prev_q is screwing up 
	   // priorities until the next update; this only affects performance, 
	   // not functionality, so no need to add protection here.
	   wire [0:num_ports-2] prev_s;
	   assign prev_s = update ? gnt_rev[1:num_ports-1] : prev_q;
	   c_dff
	     #(.width(num_ports-1),
	       .reset_type(reset_type))
	   prevq
	     (.clk(clk),
	      .reset(reset),
	      .d(prev_s),
	      .q(prev_q));
	   
	end
      else
	assign gnt = req;
      
   endgenerate
   
endmodule
