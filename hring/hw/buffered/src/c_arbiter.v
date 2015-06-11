// $Id: c_arbiter.v 1534 2009-09-16 16:10:23Z dub $

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



// generic arbiter
module c_arbiter
  (clk, reset, update, req, gnt);
   
`include "c_functions.v"
`include "c_constants.v"
   
   // number of input ports
   parameter num_ports = 32;
   
   // number fo bits required to select a port
   localparam port_idx_width = clogb(num_ports);
   
   // select type of arbiter to use
   parameter arbiter_type = `ARBITER_TYPE_ROUND_ROBIN;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // update port priorities
   input update;
   
   // request vector
   input [0:num_ports-1] req;
   
   // grant vector
   output [0:num_ports-1] gnt;
   wire [0:num_ports-1] gnt;
   
   generate

      if(num_ports > 1)
	begin
	   
	   case(arbiter_type)
	     `ARBITER_TYPE_ROUND_ROBIN:
	       begin
		  c_rr_arbiter
		    #(.num_ports(num_ports),
		      .reset_type(reset_type))
		  rr_arb
		    (.clk(clk),
		     .reset(reset),
		     .update(update),
		     .req(req),
		     .gnt(gnt));
	       end
	     `ARBITER_TYPE_MATRIX:
	       begin
		  c_matrix_arbiter
		    #(.num_ports(num_ports),
		      .reset_type(reset_type))
		  m_arb
		    (.clk(clk),
		     .reset(reset),
		     .update(update),
		     .req(req),
		     .gnt(gnt));
	       end
	     `ARBITER_TYPE_DW_FCFS:
	       begin
		  wire rst_n;
		  if(reset_type == `RESET_TYPE_ASYNC)
		    assign rst_n = ~reset;
		  else
		    assign rst_n = 1'b1;
		  
		  wire init_n;
		  if(reset_type == `RESET_TYPE_SYNC)
		    assign init_n = ~reset;
		  else
		    assign init_n = 1'b1;
		  
		  wire parked;
		  wire locked;
		  wire [0:port_idx_width-1] grant_index;
		  DW_arb_fcfs
		    #(.n(num_ports),
		      .park_mode(0),
		      .output_mode(0))
		  dw_fcfs_arb
		    (.clk(clk),
		     .rst_n(rst_n),
		     .init_n(init_n),
		     .enable(update),
		     .request(req),
		     .lock({num_ports{1'b0}}),
		     .mask({num_ports{1'b0}}),
		     .parked(parked),
		     .locked(locked),
		     .grant(gnt),
		     .grant_index(grant_index));
	       end
	   endcase
	   
	end
      else
	assign gnt = req;
      
   endgenerate
   
endmodule
