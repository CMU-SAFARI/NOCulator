// $Id: c_and_nto1.v 1534 2009-09-16 16:10:23Z dub $

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



// n-input bitwise AND
module c_and_nto1
  (data_in, data_out);
   
   // number of inputs
   parameter num_ports = 2;
   
   // width of each input
   parameter width = 1;
   
   // vector of inputs
   input [0:width*num_ports-1] data_in;
   
   // result
   output [0:width-1] data_out;
   wire [0:width-1] data_out;
   
   generate
      
      genvar 	    i;
      for(i = 0; i < width; i = i + 1)
	begin:bit_positions
	   
	   wire [0:num_ports-1] data;
	   
	   genvar 		j;
	   for(j = 0; j < num_ports; j = j + 1)
	     begin:input_ports
		assign data[j] = data_in[j*width+i];
	     end
	   
	   assign data_out[i] = &data;
	   
	end
      
   endgenerate
   
endmodule
