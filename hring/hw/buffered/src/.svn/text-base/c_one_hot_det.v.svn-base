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



// generic one-hot detect logic
module c_one_hot_det
  (data, one_hot);
   
`include "c_constants.v"
`include "c_functions.v"
   
   // width of input data for given level
   function integer lvl_width(input integer width, input integer level);
      begin
	 lvl_width = width / (1 << level);
      end
   endfunction
   
   // offset into connection vector for given level
   function integer lvl_offset(input integer width, input integer level);
      begin
	 lvl_offset = 2 * (width - lvl_width(width, level));
      end
   endfunction

   // width of input vector
   parameter width = 8;
   
   // number of reduction levels required
   localparam num_levels = clogb(width);
   
   // smallest power-of-two width that is greater than or equal to width
   localparam ext_width = (1 << num_levels);
   
   // input data
   input [0:width-1] data;
   
   // one-hot indicator
   output one_hot;
   wire one_hot;
   
   generate
      
      if(width == 1)
	begin
	   
	   // a single wire is always one-hot encoded!
	   assign one_hot = 1'b1;
	   
	end
      else
	begin
	   
	   // connections between levels
	   wire [0:(2*width-1)-1] connections;
	   
	   // for first stage input, zero-extend data to power-of-two width
	   assign connections[0:width-1] = data;
	   if(ext_width > width)
	     assign connections[width:ext_width-1] = {(ext_width-width){1'b0}};
	   
	   // bit for each level indicating whether multiple bits are set
	   wire [0:num_levels-1]  multi_hot;
	   
	   genvar 		  level;
	   
   	   for(level = 0; level < num_levels; level = level + 1)
	     begin:levels
		
		wire [0:lvl_width(width, level)-1] data;
		assign data = connections[lvl_offset(width, level):
					  lvl_offset(width, level)+
					  lvl_width(width, level)-1];
		
		wire [0:lvl_width(width, level+1)-1] both_set;
		wire [0:lvl_width(width, level+1)-1] any_set;
		
		genvar 				     pair;
		
		for(pair = 0; pair < (width / (2 << level)); pair = pair + 1)
		  begin:pairs
		     
		     assign both_set[pair] = &data[pair*2:pair*2+1];
		     assign any_set[pair] = |data[pair*2:pair*2+1];
		     
		  end
		
		assign multi_hot[level] = |both_set;
		
		assign connections[lvl_offset(width, level+1):
				   lvl_offset(width, level+1)+
				   lvl_width(width, level+1)-1]
		= any_set;
		
	     end
	   
	   assign one_hot = ~|multi_hot;
	   
	end
      
   endgenerate
   
endmodule
