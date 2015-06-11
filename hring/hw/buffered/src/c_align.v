// $Id: c_align.v 1637 2009-10-30 23:07:49Z dub $

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



// align a vector inside another one
module c_align
  (data_in, dest_in, data_out);
   
   // width of input data
   parameter data_width = 32;
   
   // width of destination vector
   parameter dest_width = 32;
   
   // offset at which to place input data within destination vector
   // (portions of the input data that end up to the left or right of the
   // destination vector will be trimmed)
   parameter offset = 0;
   
   // input vector
   input [0:data_width-1] data_in;
   
   // destination vector
   input [0:dest_width-1] dest_in;
   
   // result
   output [0:dest_width-1] data_out;
   wire [0:dest_width-1] data_out;
   
   genvar 		i;
   
   generate
      
      for(i = 0; i < dest_width; i = i + 1)
	begin:bits
	   
	   if((i < offset) || (i >= (offset + data_width)))
	     assign data_out[i] = dest_in[i];
	   else
	     assign data_out[i] = data_in[i - offset];
	   
	end
      
   endgenerate
   
endmodule

	   
