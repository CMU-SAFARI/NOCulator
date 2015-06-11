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



// input-side switch allocator logic
module vcr_sw_alloc_ip
  (clk, reset, route_ivc_op, req_in_ivc, req_out_op, gnt_in_op, gnt_out_op, 
   gnt_out_ivc, sel_ivc, allow_update);
   
`include "c_constants.v"
`include "vcr_constants.v"
   
   // number of VCs
   parameter num_vcs = 2;
   
   // number of input and output ports on router
   parameter num_ports = 5;
   
   // select implementation variant for switch allocator
   parameter allocator_type = `SW_ALLOC_TYPE_SEP_IF;
   
   // select which arbiter type to use in allocator
   parameter arbiter_type = `ARBITER_TYPE_ROUND_ROBIN;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // destination port selects
   input [0:num_vcs*num_ports-1] route_ivc_op;
   
   // requests from VC controllers
   input [0:num_vcs-1] req_in_ivc;
   
   // requests to output-side arbitration stage / wavefront block
   output [0:num_ports-1] req_out_op;
   wire [0:num_ports-1] req_out_op;
   
   // grants from output-side arbitration stage / wavefront block
   input [0:num_ports-1] gnt_in_op;

   // grants to output-side arbitration stage / crossbar
   output [0:num_ports-1] gnt_out_op;
   wire [0:num_ports-1] gnt_out_op;
   
   // grants to VC controllers
   output [0:num_vcs-1] gnt_out_ivc;
   wire [0:num_vcs-1] gnt_out_ivc;
   
   // VC selector for flit buffer read address generation
   output [0:num_vcs-1] sel_ivc;
   wire [0:num_vcs-1] 	sel_ivc;

   // allow arbiter state updates
   input allow_update;
   
   generate
      
      if(allocator_type == `SW_ALLOC_TYPE_SEP_IF)
	begin
	   
	   //-------------------------------------------------------------------
	   // For the input-first separable allocator, we just pick a single 
	   // requesting VC and forward its request to the appropriate output 
	   // port.
	   //-------------------------------------------------------------------
	   
	   // was any output port granted to this input port?
	   wire gnt_in;
	   assign gnt_in = |gnt_in_op;
	   
	   // only update priorities if we were successful in both the input 
	   // and output stage; since output stage requests are generated 
	   // from input stage grants, it is sufficient to check for output
	   // stage grants here
	   wire update_arb;
	   assign update_arb = gnt_in & allow_update;
	   
	   wire [0:num_vcs-1] req_ivc;
	   assign req_ivc = req_in_ivc;
	   
	   // select one VC among all that have pending requests
	   wire [0:num_vcs-1] gnt_ivc;
	   c_arbiter
	     #(.num_ports(num_vcs),
	       .reset_type(reset_type),
	       .arbiter_type(arbiter_type))
	   gnt_ivc_arb
	     (.clk(clk),
	      .reset(reset),
	      .update(update_arb),
	      .req(req_ivc),
	      .gnt(gnt_ivc));
	   
	   // propagate winning VC's request to the appropriate output
	   c_select_1ofn
	     #(.width(num_ports),
	       .num_ports(num_vcs))
	   req_out_op_sel
	     (.select(gnt_ivc),
	      .data_in(route_ivc_op),
	      .data_out(req_out_op));
	   
	   // the selected VC's request was successful if the output it had 
	   // requested was granted to this input port; since we only 
	   // requested one output to begin with, it is sufficient here to 
	   // check if any output was granted
	   assign gnt_out_ivc = {num_vcs{gnt_in}} & gnt_ivc;
	   
	   // one whose requests for forwarded; thus, we can generate the flit 
	   // buffer read address early
	   assign sel_ivc = gnt_ivc;
	   
	   // the output-side port grants, so just pass these through (this 
	   // signal will be ignored at the router level anyway)
	   assign gnt_out_op = gnt_in_op;
	   
	end
      else if((allocator_type == `SW_ALLOC_TYPE_SEP_OF) ||
	      ((allocator_type >= `SW_ALLOC_TYPE_WF_BASE) &&
	       (allocator_type <= `SW_ALLOC_TYPE_WF_LIMIT)))
	begin
	   
	   //-------------------------------------------------------------------
	   // For both separable output-first and wavefront allocation, requests
	   // from all input VCs are combined to be propagated to the output 
	   // side or the actual wavefront block, respectively.
	   //-------------------------------------------------------------------
	   
	   // combine all VCs' requests
	   c_select_mofn
	     #(.width(num_ports),
	       .num_ports(num_vcs))
	   req_out_op_sel
	     (.select(req_in_ivc),
	      .data_in(route_ivc_op),
	      .data_out(req_out_op));
	   
	   wire [0:num_ports*num_vcs-1] route_op_ivc;
	   c_interleaver
	     #(.width(num_vcs*num_ports),
	       .num_blocks(num_vcs))
	   route_op_ivc_intl
	     (.data_in(route_ivc_op),
	      .data_out(route_op_ivc));
	   
	   if(allocator_type == `SW_ALLOC_TYPE_SEP_OF)
	     begin
		
		//--------------------------------------------------------------
		// For output-first allocation, we check which input VCs can use
		// the output ports that were granted to us, and perform 
		// arbitration between them; another option, which would likely 
		// perform better if the number of VCs is greater than the 
		// number of ports, would be to pre-select a winning VC for each
		// output port, select a single output port to use among all 
		// that were granted to us, and then use that to enable the VC 
		// that was preselected for it. Note that this is almost 
		// identical to what we do for the wavefront allocator, except 
		// for the arbitration across all outputs.
		//--------------------------------------------------------------
		
		// only update priorities if we were successful in both stages;
		// since an output-side grant implies that at least one of our 
		// VCs actually requested the granted output, it is sufficient 
		// to just check for output grants here
		wire update_arb;
		assign update_arb = |gnt_in_op & allow_update;
		
		// determine which of the requesting VCs can use each output
		wire [0:num_ports*num_vcs-1] usable_op_ivc;
		assign usable_op_ivc = route_op_ivc & {num_ports{req_in_ivc}};
		
		// check which VCs' requests can be satisfied using the output 
		// port that was actually granted
		wire [0:num_vcs-1] req_ivc;
		c_select_mofn
		  #(.width(num_vcs),
		    .num_ports(num_ports))
		req_ivc_sel
		  (.select(gnt_in_op),
		   .data_in(usable_op_ivc),
		   .data_out(req_ivc));
		
		// arbitrate between all of our VCs that can use one of the 
		// granted outputs
		wire [0:num_vcs-1] gnt_ivc;
		c_arbiter
		  #(.num_ports(num_vcs),
		    .reset_type(reset_type),
		    .arbiter_type(arbiter_type))
		gnt_ivc_arb
		  (.clk(clk),
		   .reset(reset),
		   .update(update_arb),
		   .req(req_ivc),
		   .gnt(gnt_ivc));
		
		// notify winning VC
		assign gnt_out_ivc = gnt_ivc;
		
		// the flit buffer read address is determined by the winning VC
		assign sel_ivc = gnt_ivc;
		
		// since multiple ports may have been granted in the output 
		// stage, we must select the winning one in order to drive the 
		// crossbar control signals
		c_select_1ofn
		  #(.width(num_ports),
		    .num_ports(num_vcs))
		gnt_op_sel
		  (.select(gnt_ivc),
		   .data_in(route_ivc_op),
		   .data_out(gnt_out_op));
		
	     end
	   else if((allocator_type >= `SW_ALLOC_TYPE_WF_BASE) &&
		   (allocator_type <= `SW_ALLOC_TYPE_WF_LIMIT))
	     begin
		
		//--------------------------------------------------------------
		// A wavefront allocator will assign at most one output port to 
		// each input port; consequentlyl, we can pre-select a winning 
		// VC for each output port and then use the output port grants 
		// to enable the corresponding VC (if any).
		//--------------------------------------------------------------
		
		wire [0:num_ports*num_vcs-1] presel_op_ivc;
		
		genvar 			     op;
		
		// pre-select winning VC for each output port
		for(op = 0; op < num_ports; op = op + 1)
		  begin:ops
		     
		     // only update arbiter if output port was actually granted
		     wire update_arb;
		     assign update_arb = gnt_in_op[op] & allow_update;
		     
		     wire [0:num_vcs-1] route_ivc;
		     assign route_ivc
		       = route_op_ivc[op*num_vcs:(op+1)*num_vcs-1];
		     
		     // determine VCs with requests for this output port
		     wire [0:num_vcs-1] req_ivc;
		     assign req_ivc = route_ivc & req_in_ivc;
		     
		     // arbitrate between all VCs that requested this output
		     wire [0:num_vcs-1] gnt_ivc;
		     c_arbiter
		       #(.num_ports(num_vcs),
			 .reset_type(reset_type),
			 .arbiter_type(arbiter_type))
		     gnt_ivc_arb
		       (.clk(clk),
			.reset(reset),
			.update(update_arb),
			.req(req_ivc),
			.gnt(gnt_ivc));
		     
		     assign presel_op_ivc[op*num_vcs:(op+1)*num_vcs-1]
			      = gnt_ivc;
		     
		  end
		
		// the preselection mask shows which output (if any) each VC was
		// preselected for
		wire [0:num_vcs*num_ports-1] presel_ivc_op;
		c_interleaver
		  #(.width(num_vcs*num_ports),
		    .num_blocks(num_ports))
		presel_ivc_op_intl
		  (.data_in(presel_op_ivc),
		   .data_out(presel_ivc_op));
		
		// mask actual grants with pre-selected VCs, ...
		wire [0:num_vcs*num_ports-1] gnt_ivc_op;
		assign gnt_ivc_op = {num_vcs{gnt_in_op}} & presel_ivc_op;

		// ... rearrange them by output port, ...
		wire [0:num_ports*num_vcs-1] gnt_op_ivc;
		c_interleaver
		  #(.width(num_vcs*num_ports),
		    .num_blocks(num_vcs))
		gnt_op_ivc_intl
		  (.data_in(gnt_ivc_op),
		   .data_out(gnt_op_ivc));
		
		// ... and generate per-VC grants
		wire [0:num_vcs-1] 	     gnt_ivc;
		c_or_nto1
		  #(.width(num_vcs),
		    .num_ports(num_ports))
		gnt_ivc_or
		  (.data_in(gnt_op_ivc),
		   .data_out(gnt_ivc));
		
		// notify winning VC
		assign gnt_out_ivc = gnt_ivc;
		
		// the flit buffer read address is determined by the winning VC
		assign sel_ivc = gnt_ivc;
		
		// in the wavefront case, the final input-side port grants are 
		// just the output-side port grants, so just pass these through
		// (this signal will be ignored at the router level anyway)
		assign gnt_out_op = gnt_in_op;
		
	     end
	   
	end
      
   endgenerate
   
endmodule
