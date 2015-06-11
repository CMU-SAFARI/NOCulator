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



// matrix multiplication in GF(2)
module c_mat_mult
  (input_a, input_b, result);
   
   // matrix dimensions
   parameter dim1_width = 1;
   parameter dim2_width = 1;
   parameter dim3_width = 1;
   
   // first input matrix
   input [0:dim1_width*dim2_width-1] input_a;
   
   // second input matrix
   input [0:dim2_width*dim3_width-1] input_b;
   
   output [0:dim1_width*dim3_width-1] result;
   wire [0:dim1_width*dim3_width-1] result;
   
   generate
      
      genvar 			    row;
      
      for(row = 0; row < dim1_width; row = row + 1)
	begin:rows
	   
	   genvar col;
	   
	   for(col = 0; col < dim3_width; col = col + 1)
	     begin:cols
		
		wire [0:dim2_width-1] products;
		
		genvar 		      idx;
		
		for(idx = 0; idx < dim2_width; idx = idx + 1)
		  begin:idxs
		     assign products[idx]
			      = input_a[row*dim2_width+idx] & 
				input_b[idx*dim3_width+col];
		  end
		
		assign result[row*dim3_width+col] = ^products;
		
	     end
	   
	end
      
   endgenerate
   
endmodule
