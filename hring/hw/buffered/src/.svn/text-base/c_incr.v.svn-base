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



// generic modulo incrementer (i.e., incrementer with wraparound)
module c_incr
  (data_in, data_out);
   
`include "c_functions.v"
   
   parameter width = 3;
   
   parameter [0:width-1] min_value = 0;
   parameter [0:width-1] max_value = (1 << width) - 1;
   
   localparam num_values = max_value - min_value + 1;
   
   localparam cwidth = clogb(num_values);
   
   // operand inputs
   input [0:width-1] data_in;
   
   // sum output
   output [0:width-1] data_out;
   wire [0:width-1] data_out;
   
   wire 	    carry;
   assign carry = &data_in[(width-cwidth):width-1];
   
   wire 	    wrap;
   assign wrap = (data_in[(width-cwidth):width-1] ==
		  max_value[(width-cwidth):width-1]);
   
   generate
      
      if((1 << cwidth) == num_values)
	begin
	   
	   // if the range is a power of two, we can take advantage of natural
	   // wraparound for the LSBs
	   assign data_out[(width-cwidth):width-1]
		    = data_in[(width-cwidth):width-1] + 1'b1;
	   
	end
      else
	begin
	   
	   // if the range is not a power of two, we need to implement 
	   // explicit wraparound
	   assign data_out[(width-cwidth):width-1]
		    = wrap ?
		      min_value[(width-cwidth):width-1] :
		      (data_in[(width-cwidth):width-1] + 1'b1);
	   
	end
      
      if(width > cwidth)
	begin
	   
	   if(min_value[0:(width-cwidth)-1] == max_value[0:(width-cwidth)-1])
	     begin
		
		// if the MSBs are identical for the first and last value, we 
		// never need to change them
		assign data_out[0:(width-cwidth)-1]
			 = data_in[0:(width-cwidth)-1];
		
	     end
	   else
	     begin
		
		// if the first and last value have differing MSBs, we need to
		// adjust them whenever either the LSBs overflow or wraparound
		// occurs
		assign data_out[0:(width-cwidth)-1]
			 = data_in[0:(width-cwidth)-1] + carry - wrap;
		
	     end
	   
	end
      
   endgenerate
   
endmodule
