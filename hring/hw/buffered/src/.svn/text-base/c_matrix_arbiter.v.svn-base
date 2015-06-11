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



// generic matrix arbiter
module c_matrix_arbiter
  (clk, reset, update, req, gnt);
   
`include "c_constants.v"
   
   // number of inputs ports
   parameter num_ports = 32;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // update port priorities
   input update;
   
   // request vector
   input [0:num_ports-1] req;
   
   // grant vector
   output [0:num_ports-1] gnt;
   wire [0:num_ports-1] gnt;
   
   generate
      
      if(num_ports > 1)
	begin
	   
	   // port priority matrix
	   wire [0:num_ports*num_ports-1] prio;
	   
	   genvar 			  row;
	   
	   for(row = 0; row < num_ports; row = row + 1)
	     begin:rows
		
		// lower triangle has inverted values of transposed upper 
		// triangle
		
		genvar col;
		
		for(col = 0; col < row; col = col + 1)
		  begin:cols
		     
		     assign prio[row*num_ports+col] = ~prio[col*num_ports+row];
		     
		  end
		
		// diagonal has all ones
		assign prio[row*num_ports+row] = 1'b1;
		
		// upper triangle has actual registers
		if(row < num_ports-1)
		  begin
		     
		     wire [(row+1):num_ports-1] prio_s, prio_q;
		     assign prio_s
		       = update ?
			 (prio_q | gnt[(row+1):num_ports-1]) &
			 ~{(num_ports-row-1){gnt[row]}} :
			 prio_q;
		     c_dff
		       #(.width(num_ports-row-1),
			 .offset(row+1),
			 .reset_type(reset_type),
			 .reset_value({(num_ports-row-1){1'b1}}))
		     prioq
		       (.clk(clk),
			.reset(reset),
			.d(prio_s),
			.q(prio_q));
		     
		     assign prio[row*num_ports+row+1:(row+1)*num_ports-1]
			      = prio_q;
		     
		  end
		
		// grant requests if we have priority over all other requestors
		assign gnt[row]
			 = req[row] &
			   (&(prio[row*num_ports:(row+1)*num_ports-1] | ~req));
		
	     end
	   
	end
      else
	assign gnt = req;
      
   endgenerate
   
endmodule
