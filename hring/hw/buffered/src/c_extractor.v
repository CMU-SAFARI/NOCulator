// $Id: c_extractor.v 1534 2009-09-16 16:10:23Z dub $

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



// module for extracting a set of bits from an input word (result is 
// concatenation of these bits)
module c_extractor
  (data_in, data_out);

   // width of input word
   parameter width = 32;
   
   function integer pop_count(input [0:width-1] argument);
      integer i;
      begin
	 pop_count = 0;
	 for(i = 0; i < width; i = i + 1)
	   pop_count = pop_count + argument[i];
      end
   endfunction
   
   // mask specifying which bits to extract
   parameter [0:width-1] mask = {width{1'b1}};
   
   // width of result
   localparam new_width = pop_count(mask);
   
   // input word
   input [0:width-1] data_in;
   
   // result
   output [0:new_width-1] data_out;
   reg [0:new_width-1] data_out;
   
   reg 		       unused;
   
   integer 	       idx1, idx2;
   
   always @(data_in)
     begin
	unused = 1'b0;
	idx2 = 0;
	for(idx1 = 0; idx1 < width; idx1 = idx1 + 1)
	  if(mask[idx1] == 1'b1)
	    begin
	       data_out[idx2] = data_in[idx1];
	       idx2 = idx2 + 1;
	    end
	  else
	    begin
	       unused = unused | data_in[idx1];
	    end
     end

endmodule
