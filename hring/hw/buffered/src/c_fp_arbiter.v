// $Id: c_fp_arbiter.v 1625 2009-10-29 00:15:03Z dub $

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



// generic fixed-priority arbiter (port 0 has highest priority)
module c_fp_arbiter
  (req, gnt);
   
`include "c_constants.v"
   
   // number of input ports
   parameter num_ports = 32;
   
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
	   
	   // This finds the first non-zero bit from the right (which is why we 
	   // reverse the request vector in the first place).
	   wire [0:num_ports-1] gnt_rev;
	   assign gnt_rev = req_rev & -req_rev;
	   
	   c_reversor
	     #(.width(num_ports))
	   gnt_revr
	     (.data_in(gnt_rev),
	      .data_out(gnt));
	   
	end
      else
	assign gnt = req;
      
   endgenerate
   
endmodule
