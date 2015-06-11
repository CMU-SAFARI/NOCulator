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



// wavefron allocator variant which uses wraparound (forming a false 
// combinational loop) as described in Dally et al, "Principles and Practices of
// Interconnection Networks"
module c_wf_alloc_loop
  (clk, reset, update, req, gnt);
   
`include "c_constants.v"
   
   // number of input/output ports
   // each input can bid for any combination of outputs
   parameter num_ports = 8;
   
   // try to recover from errors
   parameter error_recovery = 0;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   // reset value for priority pointer
   localparam [0:num_ports-1] prio_init = {1'b1, {(num_ports-1){1'b0}}};
   
   input clk;
   input reset;
   
   // update arbitration priorities
   input update;
   
   // request matrix
   input [0:num_ports*num_ports-1] req;
   
   // grant matrix
   output [0:num_ports*num_ports-1] gnt;
   wire [0:num_ports*num_ports-1] gnt;
   
   // Need to make sure priority is one-hot, otherwise conflicting grants can be
   // generated!
   wire [0:num_ports-1] 	  prio_s, prio_q;
   wire 			  prio_bad;
   assign prio_bad = ~^prio_q;
   wire [0:num_ports-1] 	  prio_fixed;
   assign prio_fixed = (error_recovery && prio_bad) ? prio_init : prio_q;
   assign prio_s = update ?
		   {prio_fixed[num_ports-1], prio_fixed[0:num_ports-2]} :
		   prio_fixed;
   c_dff
     #(.width(num_ports),
       .reset_type(reset_type),
       .reset_value(prio_init))
   prioq
     (.clk(clk),
      .reset(reset),
      .d(prio_s),
      .q(prio_q));
   
   wire [0:num_ports*num_ports-1] x_pri;
   wire [0:num_ports*num_ports-1] y_pri;
   
   generate
      
      genvar 			  row;
      for(row = 0; row < num_ports; row = row + 1)
	begin:rows
	   genvar col;
	   for(col = 0; col < num_ports; col = col + 1)
	     begin:cols
		
		wire req_in;
		assign req_in = req[row*num_ports+col];
		
		wire x_pri_in;
		assign x_pri_in
		  = x_pri[row*num_ports + ((col+num_ports-1)%num_ports)] |
		    prio_q[(row+col)%num_ports];
		
		wire y_pri_in;
		assign y_pri_in
		  = y_pri[((row+num_ports-1)%num_ports)*num_ports + col] |
		    prio_q[(row+col)%num_ports];
		
		wire gnt_out;
		assign gnt_out = req_in & x_pri_in & y_pri_in;
		assign gnt[row*num_ports+col] = gnt_out;
		
		wire x_pri_out;
		assign x_pri_out = x_pri_in & ~gnt_out;
		assign x_pri[row*num_ports+col] = x_pri_out;
		
		wire y_pri_out;
		assign y_pri_out = y_pri_in & ~gnt_out;
		assign y_pri[row*num_ports+col] = y_pri_out;
		
	     end
	end
      
   endgenerate
   
endmodule
