// $Id: c_prio_enc.v 1734 2009-12-15 03:27:19Z dub $

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



// priority encoder (port 0 has highest priority)
module c_prio_enc
  (data_in, data_out);
   
`include "c_functions.v"
   
   // number of input ports (i.e., decoded width)
   parameter num_ports = 8;
   
   localparam width = clogb(num_ports);
   
   // one-hot input data
   input [0:num_ports-1] data_in;
   
   // binary encoded output data
   output [0:width-1] data_out;
   wire [0:width-1] data_out;

   wire [(width+1)*num_ports-1] masks;
   assign masks[0:num_ports-1] = {num_ports{1'b1}};
   
   generate
      
      genvar 	    level;
      for(level = 0; level < width; level = level + 1)
	begin:levels
	   
	   wire 		sel;
	   
	   wire [0:num_ports-1] mask_in;
	   assign mask_in = masks[level*num_ports:(level+1)*num_ports-1];
	   
	   wire [0:num_ports-1] bits;
	   wire 		value;
	   wire [0:num_ports-1] mask_out;
	   
	   genvar 		position;
	   for(position = 0; position < num_ports; position = position + 1)
	     begin:positions
		if(position & (1 << level))
		  begin
		     assign bits[position]
			      = data_in[position] & mask_in[position];
		     assign mask_out[position]
			      = mask_in[position] & value;
		  end
		else
		  begin
		     assign bits[position] = 1'b0;
		     assign mask_out[position]
			      = mask_in[position] & ~value;
		  end
	     end
	   
	   assign value = |bits;

	   assign data_out[(width-1)-level] = value;
	   assign mask_out[(level+1)*num_ports:(level+2)*num_ports-1]
		    = mask_out;
	   
	end
      
   endgenerate
   
endmodule
