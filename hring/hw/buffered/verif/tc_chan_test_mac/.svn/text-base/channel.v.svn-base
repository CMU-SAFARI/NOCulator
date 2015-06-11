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

// network channel model
module channel
  (clk, reset, xmit_cal, xmit_data, recv_cal, recv_data);
   
   // width of the channel (in bits)
   parameter channel_width = 16;
   
   // bit error rate (errors per 10000 bits)
   parameter error_rate = 1;
   
   // initial seed value for random number generation
   parameter initial_seed = 0;

   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // calibration enable for transmit unit
   input xmit_cal;
   
   // data to transmit over the channel
   input [0:channel_width-1] xmit_data;
   
   // calibration enable for receive unit
   input recv_cal;
   
   // data received from the channel
   output [0:channel_width-1] recv_data;
   wire [0:channel_width-1] recv_data;
   
   wire [0:channel_width-1] xmit_data_s, xmit_data_q;
   assign xmit_data_s = xmit_data;
   c_dff
     #(.width(channel_width),
       .reset_type(reset_type))
   xmit_dataq
     (.clk(clk),
      .reset(reset),
      .d(xmit_data_s),
      .q(xmit_data_q));
   
   wire [0:channel_width-1] channel_data_in;
   assign channel_data_in = xmit_cal ? {channel_width{1'bx}} : xmit_data_q;
   
   integer 		 seed = initial_seed;
   integer 		 i;
   
   reg [0:channel_width-1] channel_errors;
   
   always @(posedge clk)
     for(i = 0; i < channel_width; i = i + 1)
       channel_errors[i]
	 <= ($dist_uniform(seed, 0, 9999) < error_rate) ? 1'b1 : 1'b0;
   
   wire [0:channel_width-1] channel_data_out;
   assign channel_data_out = channel_data_in ^ channel_errors;
   
   wire [0:channel_width-1] recv_data_s, recv_data_q;
   assign recv_data_s = channel_data_out;
   c_dff
     #(.width(channel_width),
       .reset_type(reset_type))
   recv_dataq
     (.clk(clk),
      .reset(reset),
      .d(recv_data_s),
      .q(recv_data_q));
   
   assign recv_data = recv_cal ? {channel_width{1'bx}} : recv_data_q;
   
endmodule
