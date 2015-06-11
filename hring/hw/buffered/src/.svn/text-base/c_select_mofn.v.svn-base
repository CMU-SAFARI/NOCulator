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



// generic multi-hot select gate
module c_select_mofn
  (select, data_in, data_out);
   
   // number of input ports
   parameter num_ports = 4;
   
   // width of each port
   parameter width = 32;
   
   // control signal to select active port
   input [0:num_ports-1] select;
   
   // vector of inputs
   input [0:num_ports*width-1] data_in;
   
   // result
   output [0:width-1] data_out;
   wire [0:width-1] data_out;
   
   generate
      
      genvar 	    i;
      for(i = 0; i < width; i = i + 1)
	begin:width_loop
	   
	   wire [0:num_ports-1] port_bits;
	   
	   genvar 		j;
	   for(j = 0; j < num_ports; j = j + 1)
	     begin:ports_loop
		assign port_bits[j] = data_in[i+j*width] & select[j];
	     end
	   
	   assign data_out[i] = |port_bits;
	   
	end
      
   endgenerate
   
endmodule
