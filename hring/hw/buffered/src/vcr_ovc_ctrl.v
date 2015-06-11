// $Id: vcr_ovc_ctrl.v 2069 2010-05-31 23:47:53Z dub $

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



// output port controller (tracks state of buffers in downstream router)
module vcr_ovc_ctrl
  (clk, reset, flow_ctrl, vc_gnt, vc_gnt_ip, vc_gnt_ivc, sw_gnt_q, sw_gnt_ip_q, 
   int_flit_ctrl, match, elig, empty, int_flow_ctrl, errors);
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
   
   // flit buffer entries per VC
   parameter num_flit_buffers = 8;
   
   // width required to select a buffer
   localparam flit_buffer_idx_width = clogb(num_flit_buffers);
   
   // maximum number of packets that can be in a given VC buffer simultaneously
   parameter num_header_buffers = 4;
   
   // number of traffic classes (e.g. request, reply)
   parameter num_message_classes = 2;
   
   // number of resource classes (e.g. minimal, adaptive)
   parameter num_resource_classes = 2;
   
   // total number of packet classes
   localparam num_packet_classes = num_message_classes * num_resource_classes;
   
   // number of VCs available for each class
   parameter num_vcs_per_class = 1;
   
   // number of VCs
   localparam num_vcs = num_packet_classes * num_vcs_per_class;
   
   // width required to select individual VC
   localparam vc_idx_width = clogb(num_vcs);
   
   // number of routers in each dimension
   parameter num_routers_per_dim = 4;
   
   // number of dimensions in network
   parameter num_dimensions = 2;
   
   // number of nodes per router (a.k.a. consentration factor)
   parameter num_nodes_per_router = 1;
   
   // connectivity within each dimension
   parameter connectivity = `CONNECTIVITY_LINE;
   
   // number of adjacent routers in each dimension
   localparam num_neighbors_per_dim
     = ((connectivity == `CONNECTIVITY_LINE) ||
	(connectivity == `CONNECTIVITY_RING)) ?
       2 :
       (connectivity == `CONNECTIVITY_FULL) ?
       (num_routers_per_dim - 1) :
       -1;
   
   // number of input and output ports on router
   localparam num_ports
     = num_dimensions * num_neighbors_per_dim + num_nodes_per_router;
   
   // select packet format
   parameter packet_format = `PACKET_FORMAT_EXPLICIT_LENGTH;
   
   // width of flit control signals
   localparam flit_ctrl_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (1 + vc_idx_width + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + vc_idx_width + 1) : 
       -1;
   
   // width of flow control signals
   localparam flow_ctrl_width = 1 + vc_idx_width;
   
   // ID of current input VC
   parameter vc_id = 0;
   
   // select whether VCs must have credits available in order to be considered 
   // for VC allocation
   parameter vc_alloc_requires_credit = 0;
   
   // select whether to set a packet's outgoing VC ID at the input or output 
   // controller
   parameter track_vcs_at_output = 0;
   
   // select method for credit signaling from output to input controller
   parameter int_flow_ctrl_type = `INT_FLOW_CTRL_TYPE_PUSH;
   
   // number of bits to be used for credit level reporting
   // (note: must be less than or equal to cred_count_width as given below)
   // (note: this parameter is only used for INT_FLOW_CTRL_TYPE_LEVEL)
   parameter cred_level_width = 2;
   
   // width required for internal flit control signalling
   localparam int_flit_ctrl_width = 1 + vc_idx_width + 1 + 1;
   
   // width required for internal flow control signalling
   localparam int_flow_ctrl_width
     = (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_LEVEL) ?
       cred_level_width :
       (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_PUSH) ?
       1 :
       -1;
   
   // width for full range of credit count ([0:num_credits])
   localparam cred_count_width = clogb(num_flit_buffers + 1);
   
   // width of counter for credits waiting to be sent to input controller
   localparam cred_hold_width = vc_alloc_requires_credit ? 
				clogb(num_flit_buffers) : 
				cred_count_width;
   
   // initial value for credit hold counter
   localparam [0:cred_hold_width-1] cred_hold_reset_value
     = vc_alloc_requires_credit ? (num_flit_buffers - 1) : num_flit_buffers;
   
   // capped reset value for credit level
   localparam [0:cred_level_width-1] free_reset_value
     = (cred_count_width > cred_level_width) ? 
       ((1 << cred_level_width) - 1) : 
       num_flit_buffers;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // incoming flow control signals
   input [0:flow_ctrl_width-1] flow_ctrl;
   
   // output VC was granted to an input VC
   input vc_gnt;
   
   // input port that the output VC was granted to
   input [0:num_ports-1] vc_gnt_ip;
   
   // input VC that the output VC was granted to
   input [0:num_vcs-1] vc_gnt_ivc;
   
   // switch allocation produced a grant for this output port
   input sw_gnt_q;
   
   // input port that this output port was granted to during switch allocation
   input [0:num_ports-1] sw_gnt_ip_q;
   
   // incoming flit control signals
   input [0:int_flit_ctrl_width-1] int_flit_ctrl;
   
   // output VC owns the current flit
   // (NOTE: only valid if track_vcs_at_output=1)
   output match;
   wire match;
   
   // output VC is eligible for allocation (i.e., not currently allocated)
   output elig;
   wire elig;
   
   // no buffer slots in output VC are in use (i.e., all credits are present)
   output empty;
   wire empty;
   
   // internal flow control signalling from output controller to input 
   // controllers
   output [0:int_flow_ctrl_width-1] int_flow_ctrl;
   wire [0:int_flow_ctrl_width-1] int_flow_ctrl;
   
   // internal error condition detected
   output [0:3] errors;
   wire [0:3] 			  errors;
   
   
   //---------------------------------------------------------------------------
   // decode control signals
   //---------------------------------------------------------------------------
   
   wire 			  flit_valid;
   assign flit_valid = int_flit_ctrl[0] & sw_gnt_q;
   
   wire 			  flit_tail;
   assign flit_tail = int_flit_ctrl[1+vc_idx_width+1];
   
   wire 			  cred_valid;
   assign cred_valid = flow_ctrl[0];
   
   wire 			  credit;
   
   generate
      
      if(num_vcs > 1)
	begin
	   
	   wire [0:vc_idx_width-1] cred_vc;
	   assign cred_vc = flow_ctrl[1:1+vc_idx_width-1];
	   
	   assign credit = cred_valid && (cred_vc == vc_id);
	   
	end
      else
	assign credit = cred_valid;
      
   endgenerate
   
   wire 			   flit_valid_qual;
   assign flit_valid_qual = flit_valid & match;
   
   wire 			   debit;
   assign debit = flit_valid_qual;
   
   
   //---------------------------------------------------------------------------
   // track whether this output VC is currently assigend to an input VC
   //---------------------------------------------------------------------------
   
   wire 			   allocated_q;
   
   wire 			   next_allocated;   
   assign next_allocated = vc_gnt | allocated_q;
   
   wire 			   allocated_s;
   assign allocated_s = next_allocated & ~(flit_valid_qual & flit_tail);
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   allocatedq
     (.clk(clk),
      .reset(reset),
      .d(allocated_s),
      .q(allocated_q));
   
   generate
      
      if(num_vcs > 1)
	begin
	   
	   wire [0:vc_idx_width-1] flit_vc;
	   assign flit_vc = int_flit_ctrl[1:1+vc_idx_width-1];
	   
	   // if VC assignment is tracked at the output, each output VC needs to
	   // check if it is currently assigned to the incoming VC ID at the 
	   // selected input port
	   if(track_vcs_at_output)
	     begin
		
		wire [0:num_ports-1] allocated_ip_s, allocated_ip_q;
		assign allocated_ip_s = vc_gnt ? vc_gnt_ip : allocated_ip_q;
		c_dff
		  #(.width(num_ports),
		    .reset_type(reset_type))
		allocated_ipq
		  (.clk(clk),
		   .reset(reset),
		   .d(allocated_ip_s),
		   .q(allocated_ip_q));
		
		wire [0:vc_idx_width-1] vc_gnt_vc;
		c_encoder
		  #(.num_ports(num_vcs))
		vc_gnt_vc_enc
		  (.data_in(vc_gnt_ivc),
		   .data_out(vc_gnt_vc));
		
		wire [0:vc_idx_width-1]   allocated_vc_s, allocated_vc_q;
		assign allocated_vc_s = vc_gnt ? vc_gnt_vc : allocated_vc_q;
		c_dff
		  #(.width(vc_idx_width),
		    .reset_type(reset_type))
		allocated_vcq
		  (.clk(clk),
		   .reset(reset),
		   .d(allocated_vc_s),
		   .q(allocated_vc_q));
		
		assign match = allocated_q &&
			       |(allocated_ip_q & sw_gnt_ip_q) &&
			       (allocated_vc_q == flit_vc);
		
	     end
	   
	   // if VC assignments are tracked at the input side, we can just check
	   // for our own VC ID
	   else
	     assign match = (flit_vc == vc_id);
	   
	end
      
      // if we only have a single VC to begin with, it is always selected
      else
	assign match = 1'b1;
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // track number of packets that downstream buffer can still accept
   //---------------------------------------------------------------------------
   
   // credit info only ever gets update when this output was granted or a credit
   // was received
   wire 			     update_credits;
   assign update_credits = sw_gnt_q | cred_valid;
   
   wire [0:cred_count_width-1] cred_count_q;
   wire 		       head_free;
   wire 		       error_hct_underflow;
   wire 		       error_hct_overflow;
   
   generate
      
      if(num_header_buffers == num_flit_buffers)
	begin
	   
	   // if every flit buffer entry can be a head flit, we don't need 
	   // dedicated credit tracking for headers, and can instead just rely 
	   // on the normal credit tracking mechanism
	   assign head_free = 1'b1;
	   
	   assign error_hct_underflow = 1'b0;
	   assign error_hct_overflow = 1'b0;
	   
	end
      else
	begin
	   
	   // if only a subset of the flit buffer entries can be headers, we 
	   // need to keep track of how many such slots are left
	   
	   wire head_debit;
	   assign head_debit = vc_gnt;
	   
	   wire head_credit;
	   
	   if(num_header_buffers == 1)
	     begin
		
		// if we can only have one packet per buffer, we can accept the 
		// next one once the buffer has been completely drained after 
		// the tail has been sent
		
		wire last_credit_received;
		assign last_credit_received
		  = (cred_count_q == (num_flit_buffers - 1)) && credit;
		
		wire tail_sent_q;
		
		assign head_credit = tail_sent_q & last_credit_received;
		
		wire tail_sent_s;
		assign tail_sent_s
		  = update_credits ? 
		    ((tail_sent_q & ~last_credit_received) | 
		     (flit_valid_qual & flit_tail)) : 
		    tail_sent_q;
		c_dff
		  #(.width(1),
		    .reset_type(reset_type))
		tail_sentq
		  (.clk(clk),
		   .reset(reset),
		   .d(tail_sent_s),
		   .q(tail_sent_q));
		
		wire head_free_s, head_free_q;
		assign head_free_s = (head_free_q & ~head_debit) | head_credit;
		c_dff
		  #(.width(1),
		    .reset_type(reset_type),
		    .reset_value(1'b1))
		head_freeq
		  (.clk(clk),
		   .reset(reset),
		   .d(head_free_s),
		   .q(head_free_q));
		
		assign head_free = head_free_q;
		
		assign error_hct_underflow = head_debit & ~head_free_q;
		assign error_hct_overflow = head_credit & head_free_q;
		
	     end
	   else
	     begin
		
		wire [0:flit_buffer_idx_width-1] push_ptr_next, push_ptr_q;
		c_incr
		  #(.width(flit_buffer_idx_width),
		    .min_value(0),
		    .max_value(num_flit_buffers-1))
		push_ptr_incr
		  (.data_in(push_ptr_q),
		   .data_out(push_ptr_next));
		
		wire [0:flit_buffer_idx_width-1] push_ptr_s;
		assign push_ptr_s
		  = update_credits ?
		    (flit_valid_qual ? push_ptr_next : push_ptr_q) : 
		    push_ptr_q;
		c_dff
		  #(.width(flit_buffer_idx_width),
		    .reset_type(reset_type))
		push_ptrq
		  (.clk(clk),
		   .reset(reset),
		   .d(push_ptr_s),
		   .q(push_ptr_q));
		
		wire [0:flit_buffer_idx_width-1] pop_ptr_next, pop_ptr_q;
		c_incr
		  #(.width(flit_buffer_idx_width),
		    .min_value(0),
		    .max_value(num_flit_buffers-1))
		pop_ptr_incr
		  (.data_in(pop_ptr_q),
		   .data_out(pop_ptr_next));
		
		wire [0:flit_buffer_idx_width-1] pop_ptr_s;
		assign pop_ptr_s
		  = update_credits ? 
		    (credit ? pop_ptr_next : pop_ptr_q) : 
		    pop_ptr_q;
		c_dff
		  #(.width(flit_buffer_idx_width),
		    .reset_type(reset_type))
		pop_ptrq
		  (.clk(clk),
		   .reset(reset),
		   .d(pop_ptr_s),
		   .q(pop_ptr_q));
		
		reg [0:num_flit_buffers-1] 	 tail_queue;
		
		always @(posedge clk)
		  if(update_credits)
		    if(flit_valid_qual)
		      tail_queue[push_ptr_q] <= flit_tail;
		
		wire 				 tail;
		assign tail = tail_queue[pop_ptr_q];
		
		assign head_credit = credit & tail;		
		
		// track header credits using stock credit tracker module
		wire [0:1] 			 hct_errors;
		c_credit_tracker
		  #(.num_credits(num_header_buffers),
		    .reset_type(reset_type))
		hct
		  (.clk(clk),
		   .reset(reset),
		   .credit(head_credit),
		   .debit(head_debit),
		   .free(head_free),
		   .errors(hct_errors));
		
		assign error_hct_underflow = hct_errors[0];
		assign error_hct_overflow = hct_errors[1];
		
	     end
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // track number of flits that downstream buffer can still accept
   //---------------------------------------------------------------------------

   wire [0:cred_count_width-1] 			 cred_count_plus_credit;
   assign cred_count_plus_credit = cred_count_q + credit;
   
   wire [0:cred_count_width-1] 			 cred_count_next;
   assign cred_count_next = cred_count_plus_credit - debit;
   
   wire [0:cred_count_width-1] 			 cred_count_s;
   assign cred_count_s = update_credits ? cred_count_next : cred_count_q;
   c_dff
     #(.width(cred_count_width),
       .reset_type(reset_type),
       .reset_value(num_flit_buffers))
   cred_countq
     (.clk(clk),
      .reset(reset),
      .d(cred_count_s),
      .q(cred_count_q));
   
   wire 					 cred_count_zero;
   assign cred_count_zero = ~|cred_count_q;
   
   wire 					 error_ct_underflow;
   assign error_ct_underflow = cred_count_zero & debit;
   
   wire 					 cred_count_all;
   assign cred_count_all = (cred_count_q == num_flit_buffers);
   
   wire 					 error_ct_overflow;
   assign error_ct_overflow = cred_count_all & credit;
   
   assign empty = cred_count_all;
   
   
   //---------------------------------------------------------------------------
   // generate flow control signals to input controller
   //---------------------------------------------------------------------------
   
   generate
      
      case(int_flow_ctrl_type)
	
	`INT_FLOW_CTRL_TYPE_LEVEL:
	  begin
	     
	     // NOTE: The debit input to this module is driven by the input VC 
	     // controller. Consequently, the debit signal is already readily 
	     // available at the input controller, and we can simply pass the 
	     // current credit level, incremented by one if a credit was just 
	     // received, from output to input controller, rather having the 
	     // debit signal effectively go from input to output controller 
	     // and back. Note that whenever an output VC is allocated to an 
	     // input VC, no flit can have been sent in the current cycle, as
	     // in order for VC allocation to succeed, the output VC must be 
	     // eligible for allocation in the current cycle, and thus cannot 
	     // be assigned to another input VC.
	     
	     if(cred_count_width > cred_level_width)
	       begin
		  
		  wire limit_cred_level;
		  assign limit_cred_level
		    = |cred_count_plus_credit[0:
					      cred_count_width-
					      cred_level_width-1];
		  
		  assign int_flow_ctrl
		    = {cred_level_width{limit_cred_level}} | 
		      cred_count_plus_credit[cred_count_width-cred_level_width:
					     cred_count_width-1];
		  
	       end
	     else if(cred_count_width == cred_level_width)
	       assign int_flow_ctrl
		 = cred_count_plus_credit[0:cred_count_width-1];
	     else
	       begin
		  assign int_flow_ctrl[0:cred_level_width-cred_count_width-1]
			   = {(cred_level_width-cred_count_width){1'b0}};
		  assign int_flow_ctrl[cred_level_width-cred_count_width:
				       cred_level_width-1]
			   = cred_count_plus_credit;
	       end
	     
	  end
	
	`INT_FLOW_CTRL_TYPE_PUSH:
	  begin
	     
	     wire [0:cred_hold_width-1]  cred_hold_copy;
	     if(vc_alloc_requires_credit)
	       assign cred_hold_copy
		 = cred_count_next[(cred_count_width-cred_hold_width):
				   cred_count_width-1] - 
		   |cred_count_next;
	     else
	       assign cred_hold_copy = cred_count_next;
	     
	     if(cred_hold_width >= 2)
	       begin
		  
		  wire [0:cred_hold_width-1]  cred_hold_s, cred_hold_q;
		  assign cred_hold_s
		    = next_allocated ?
		      (cred_hold_q - (|cred_hold_q & ~credit)) :
		      cred_hold_copy;
		  c_dff
		    #(.width(cred_hold_width),
		      .reset_type(reset_type),
		      .reset_value(cred_hold_reset_value))
		  cred_holdq
		    (.clk(clk),
		     .reset(reset),
		     .d(cred_hold_s),
		     .q(cred_hold_q));
		  
		  assign int_flow_ctrl
		    = next_allocated ?
		      (|cred_hold_q[0:cred_hold_width-2] | credit) :
		      |cred_hold_copy;
		  
	       end
	     else
	       assign int_flow_ctrl = next_allocated ? credit : |cred_hold_copy;
	     
	  end
	
      endcase
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // keep track of whether current VC is available for allocation
   //---------------------------------------------------------------------------
   
   generate
      
      if(vc_alloc_requires_credit)
	begin
	   
	   // Instead of just computing this as the reduction of of the updated 
	   // credit count sum, we can optimize a bit. In particular, we have 
	   // one or more credits available iff...
	   //  a) ... we just received a credit. In this case, we either had 
	   //     credits available to begin with, or there cannot be a debit 
	   //     at the same time.
	   //  b) ... we currently have at least two credits available. In this 
	   //     case, even if there is a debit and no credit, we will still 
	   //     have at least one credit left.
	   //  c) ... we currently have an odd number of credits available, and 
	   //     did not just receive a debit. For odd numbers greater than 
	   //     one, we will always have credit left, and in case we have one 
	   //     credit left, we will still have at least one left if no debit 
	   //     occurs.
	   // This only leaves out the case where we are not receiving a credit,
	   // do not currently have two or more credits and either have no 
	   // credits left or have a single credit left and receive a debit.
	   wire cred_s, cred_q;
	   assign cred_s = credit | 
			   |cred_count_q[0:cred_count_width-2] | 
			   (cred_count_q[cred_count_width-1] & ~debit);
	   c_dff
	     #(.width(1),
	       .reset_type(reset_type))
	   credq
	     (.clk(clk),
	      .reset(reset),
	      .d(cred_s),
	      .q(cred_q));
	   
	   assign elig = ~allocated_q & head_free & cred_q;
	   
	end
      else
	assign elig = ~allocated_q & head_free;
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // error checking
   //---------------------------------------------------------------------------
   
   assign errors = {error_ct_underflow, 
		    error_ct_overflow,
		    error_hct_underflow, 
		    error_hct_overflow};
   
endmodule
