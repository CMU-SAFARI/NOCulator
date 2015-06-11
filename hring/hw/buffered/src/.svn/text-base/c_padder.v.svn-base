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



// add padding to given vector
module c_padder
  (data_in, data_out);
   
   // width of input data
   parameter width = 32;
   
   // number of bits to pad on the left (use negative value to trim bits
   parameter pad_left = 0;
   
   // bit value to pad with on the left
   parameter pad_left_value = 0;
   
   // number of bits to pad on the right (use negative value to trim bits)
   parameter pad_right = 0;
   
   // bit value to pad with on the right
   parameter pad_right_value = 0;
   
   // width of input vector
   localparam new_width = pad_left + width + pad_right;
   
   // input vector
   input [0:width-1] data_in;
   
   // result
   output [0:new_width-1] data_out;
   wire [0:new_width-1] data_out;
   
   genvar 		i;
   
   generate
      
      for(i = 0; i < new_width; i = i + 1)
	begin:bits
	   
	   if(i < pad_left)
	     assign data_out[i] = pad_left_value;
	   else if(i >= (new_width - pad_right))
	     assign data_out[i] = pad_right_value;
	   else
	     assign data_out[i] = data_in[i - pad_left];
	   
	end
      
   endgenerate
   
endmodule

	   
