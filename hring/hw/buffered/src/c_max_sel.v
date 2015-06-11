// $Id: c_max_sel.v 1734 2009-12-15 03:27:19Z dub $

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



// generic round-robin arbiter
module c_prio_sel
  (port_prios, max_ports);
   
   // number of input ports
   parameter num_ports = 32;
   
   // width of priority fields
   parameter prio_width = 4;
   
   // priority values
   input [0:num_ports*prio_width-1] port_prios;
   
   // vector representing maximum priority ports
   output [0:num_ports-1] max_ports;
   wire [0:num_ports-1] max_ports;
   
   // for each priority bit, list all ports that have this bit set
   wire [0:prio_width*num_ports-1] prio_ports
   c_interleaver
     #(.width(num_ports*prio_width),
       .num_blocks(num_ports))
   prio_ports_intl
     (.data_in(port_prios),
      .data_out(prio_ports));
   
   wire [0:prio_width-1] 	   prio_some_set;
   c_or_nto1
     #(.num_ports(num_ports),
       .width(prio_width))
   prio_some_set_or
     (.data_in(prio_ports),
      .data_out(prio_some_set));
   
   wire [0:prio_width-1] 	   prio_some_unset;
   c_nand_nto1
     #(.num_ports(num_ports),
       .width(prio_width))
   prio_some_unset_nand
     (.data_in(prio_ports),
      .data_out(prio_some_unset));
   
   // find bit positions that are not identical for all ports
   wire [0:prio_width-1] 	   prio_diffs;
   assign prio_diffs = prio_some_set & prio_some_unset;
   
   wire [0:prio_width-1] 	   first_prio_diff;
   c_fp_arbiter
     #(.num_ports(prio_width))
   first_prio_diff_arb
     (.req(prio_diffs),
      .gnt(first_prio_diff));
   
   c_select_1ofn
     #(.num_ports(prio_width+1),
       .width(num_ports))
   max_ports_sel
     (.select({first_prio_diff, ~|prio_diffs}),
      .data_in({prio_ports, {num_ports{1'b1}}}),
      .data_out(max_ports));
   
endmodule
