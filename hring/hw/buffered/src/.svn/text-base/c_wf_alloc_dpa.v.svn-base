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



// wavefron allocator variant implementing a Diagonal Propagation Arbiter as
// described in Hurt et al, "Design and Implementation of High-Speed Symmetric 
// Crossbar Schedulers"
module c_wf_alloc_dpa
  (clk, reset, update, req, gnt);
   
`include "c_constants.v"
   
   // number of input/output ports
   // each input can bid for any combination of outputs
   parameter num_ports = 8;
   
   // try to recover from errors
   parameter error_recovery = 0;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   // reset value for priority pointer
   localparam [0:(2*num_ports-1)-1] prio_init
     = {{num_ports{1'b1}}, {(num_ports-1){1'b0}}};
   
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
   wire [0:(2*num_ports-1)-1] 	  prio_s, prio_q;
   wire 			  prio_bad;
   assign prio_bad = ^prio_q ^ ^prio_init;
   
   wire 			  reset_prio;
   assign reset_prio = ~|prio_q[0:(num_ports-1)-1] ||
		       (error_recovery && prio_bad);
   
   assign prio_s = update ?
		   (reset_prio ?
		    prio_init :
		    {1'b0, prio_q[0:(2*num_ports-1)-2]}) :
		   prio_q;
   c_dff
     #(.width(2*num_ports-1),
       .reset_type(reset_type),
       .reset_value(prio_init))
   prioq
     (.clk(clk),
      .reset(reset),
      .d(prio_s),
      .q(prio_q));
   
   wire [0:num_ports*(2*num_ports-2)-1] x;
   
   generate
      
      genvar 				col;
      for(col = 0; col < num_ports; col = col + 1)
	begin:cols
	   
	   wire [0:(2*num_ports-1)-1] x_in, y_in;
	   wire [0:(2*num_ports-1)-1] x_out, y_out;
	   wire [0:(2*num_ports-1)-1] req_in;
	   wire [0:(2*num_ports-1)-1] gnt_out;
	   
	   assign x_in = {1'b1, x[col*(2*num_ports-2):
				  (col+1)*(2*num_ports-2)-1]};
	   assign y_in = {1'b1, y_out[0:(2*num_ports-2)-1]};
	   assign x_out = (x_in & ~(y_in & req_in)) | ~prio_q;
	   assign y_out = (y_in & ~(x_in & req_in)) | ~prio_q;
	   assign gnt_out = req_in & x_in & y_in & prio_q;
	   assign x[((col+1)%num_ports)*(2*num_ports-2):
		    ((col+1)%num_ports+1)*(2*num_ports-2)-1]
		    = x_out[0:(2*num_ports-2)-1];
	   
	   genvar 		      row;
	   for(row = 0; row < num_ports-1; row = row + 1)
	     begin:rows
		
		assign req_in[row]
			 = req[((num_ports-col+row)%num_ports)*num_ports + col];
		assign req_in[row+num_ports]
			 = req[((num_ports-col+row)%num_ports)*num_ports + col];
		assign gnt[((num_ports-col+row)%num_ports)*num_ports+col]
			 = gnt_out[row] | gnt_out[row+num_ports];
	     end
	   
	   assign req_in[num_ports-1]
		    = req[((2*num_ports-1-col)%num_ports)*num_ports+col];
	   assign gnt[((2*num_ports-1-col)%num_ports)*num_ports+col]
		    = gnt_out[num_ports-1];
	   
	end	     
      
   endgenerate
   
endmodule
