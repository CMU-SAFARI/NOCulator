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



// input VC controller
module vcr_ivc_ctrl
  (clk, reset, router_address, flit_valid_in, flit_head_in, flit_tail_in, 
   header_info_in, int_flow_ctrl_op_ovc, route_op, route_port, inc_rc, vc_req, 
   vc_gnt, vc_gnt_ovc, sw_req_nonspec, sw_req_spec, sw_gnt_nonspec, sw_gnt_spec,
   flit_head, flit_tail, la_route_info, fbc_write_addr, fbc_read_addr, 
   fbc_empty, allocated, allocated_ovc, free_unallocated, free_allocated, 
   errors, events);
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
   
   // flit buffer entries per VC
   parameter num_flit_buffers = 8;
   
   // width of credit hold register
   // (note: this parameter is only used for INT_FLOW_CTRL_TYPE_PUSH)
   localparam cred_hold_width = clogb(num_flit_buffers);
   
   // maximum number of packets that can be in a given VC buffer simultaneously
   parameter num_header_buffers = 4;
   
   // number of message classes (e.g. request, reply)
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
   
   // width required to select an individual port
   localparam port_idx_width = clogb(num_ports);
   
   // width required for lookahead routing information
   localparam la_route_info_width
     = port_idx_width + ((num_resource_classes > 1) ? 1 : 0);
   
   // select packet format
   parameter packet_format = `PACKET_FORMAT_EXPLICIT_LENGTH;
   
   // maximum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter max_payload_length = 4;
   
   // minimum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter min_payload_length = 1;
   
   // number of bits required to represent all possible payload sizes
   localparam payload_length_width
     = clogb(max_payload_length-min_payload_length+1);

   // width of counter for remaining flits
   localparam flit_ctr_width = clogb(max_payload_length);
   
   // width required to select individual router in a dimension
   localparam dim_addr_width = clogb(num_routers_per_dim);
   
   // width required to select individual router in network
   localparam router_addr_width = num_dimensions * dim_addr_width;
   
   // width required to select individual node at current router
   localparam node_addr_width = clogb(num_nodes_per_router);
   
   // width of global addresses
   localparam addr_width = router_addr_width + node_addr_width;
   
   // total number of bits required for storing routing information
   localparam route_info_width
     = num_resource_classes * router_addr_width + node_addr_width;
   
   // total number of bits of header information encoded in header flit payload
   localparam header_info_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (la_route_info_width + route_info_width) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (la_route_info_width + route_info_width + payload_length_width) : 
       -1;
   
   // width of flit control signals
   localparam flit_ctrl_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (1 + vc_idx_width + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + vc_idx_width + 1) : 
       -1;
   
   // select whether to set a packet's outgoing VC ID at the input or output 
   // controller
   parameter track_vcs_at_output = 0;
   
   // filter out illegal destination ports
   // (the intent is to allow synthesis to optimize away the logic associated 
   // with such turns)
   parameter restrict_turns = 1;
   
   // select routing function type
   parameter routing_type = `ROUTING_TYPE_DOR;
   
   // select order of dimension traversal
   parameter dim_order = `DIM_ORDER_ASCENDING;
   
   // select method for credit signaling from output to input controller
   parameter int_flow_ctrl_type = `INT_FLOW_CTRL_TYPE_PUSH;
   
   // number of bits to be used for credit level reporting
   // (note: must be less than or equal to cred_count_width as given below)
   // (note: this parameter is only used for INT_FLOW_CTRL_TYPE_LEVEL)
   parameter cred_level_width = 2;
   
   // width required for internal flow control signalling
   localparam int_flow_ctrl_width
     = (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_LEVEL) ?
       cred_level_width :
       (int_flow_ctrl_type == `INT_FLOW_CTRL_TYPE_PUSH) ?
       1 :
       -1;
   
   // select implementation variant for header FIFO
   parameter header_fifo_type = `FIFO_TYPE_INDEXED;
   
   // select implementation variant for VC allocator
   parameter vc_alloc_type = `VC_ALLOC_TYPE_SEP_IF;
   
   // select whether VCs must have credits available in order to be considered 
   // for VC allocation
   parameter vc_alloc_requires_credit = 0;
   
   // select speculation type for switch allocator
   parameter sw_alloc_spec_type = `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS;
   
   // enable performance counter
   parameter perf_ctr_enable = 1;
   
   // ID of current input VC
   parameter vc_id = 0;
   
   // message class to which this VC belongs
   localparam message_class
     = (vc_id / (num_resource_classes*num_vcs_per_class)) % num_message_classes;
   
   // resource class to which this VC belongs
   localparam resource_class
     = (vc_id / num_vcs_per_class) % num_resource_classes;
   
   // packet class to which this VC belongs
   localparam packet_class = (vc_id / num_vcs_per_class) % num_packet_classes;
   
   // width of routing information required for each VC
   localparam hop_route_info_width
     = (resource_class == (num_resource_classes - 1)) ?
       addr_width :
       (resource_class == (num_resource_classes - 2)) ?
       (router_addr_width + addr_width) :
       (2 * router_addr_width);
   
   // total number of bits of header information required for each VC
   localparam hdff_width = la_route_info_width + hop_route_info_width;
   
   // required address size for flit buffer
   localparam fbf_addr_width = clogb(num_vcs*num_flit_buffers);
   
   // index of leftmost entry in shared flit buffer
   localparam fbc_min_addr = vc_id * num_flit_buffers;
   
   // ID of current input port
   parameter port_id = 0;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // current router's address
   input [0:router_addr_width-1] router_address;
   
   // incoming flit valid
   input flit_valid_in;
   
   // incoming flit is head flit
   input flit_head_in;
   
   // incoming flit is tail
   input flit_tail_in;
   
   // header information associated with current flit
   input [0:header_info_width-1] header_info_in;
   
   // internal flow control signalling from output controller to input 
   // controllers
   input [0:num_ports*num_vcs*int_flow_ctrl_width-1] int_flow_ctrl_op_ovc;
   
   // destination port (1-hot)
   output [0:num_ports-1] route_op;
   wire [0:num_ports-1] route_op;
   
   // destination port (encoded)
   output [0:port_idx_width-1] route_port;
   wire [0:port_idx_width-1] route_port;
   
   // transition to next resource class
   output inc_rc;
   wire 		inc_rc;
   
   // request VC allocation
   output vc_req;
   wire vc_req;
   
   // VC allocation successful
   input vc_gnt;
   
   // granted output VC
   input [0:num_vcs-1] vc_gnt_ovc;
   
   // non-speculative switch allocator requests
   output sw_req_nonspec;
   wire 		sw_req_nonspec;
   
   // speculative switch allocator requests
   output sw_req_spec;
   wire 		sw_req_spec;
   
   // non-speculative switch allocator grants
   input sw_gnt_nonspec;
   
   // speculative switch allocator grants
   input sw_gnt_spec;
   
   // outgoing flit is head flit
   output flit_head;
   wire 		flit_head;
   
   // outgoign flit is tail flit
   output flit_tail;
   wire 		flit_tail;
   
   // lookahead routing information for outgoing flit
   output [0:la_route_info_width-1] la_route_info;
   wire [0:la_route_info_width-1] la_route_info;
   
   // write pointer for shared flit buffer
   output [0:fbf_addr_width-1] fbc_write_addr;
   wire [0:fbf_addr_width-1] 	  fbc_write_addr;
   
   // read pointer for shared flit buffer
   output [0:fbf_addr_width-1] fbc_read_addr;
   wire [0:fbf_addr_width-1] 	  fbc_read_addr;
   
   // flit buffer does not have any valid entries
   output fbc_empty;
   wire 			  fbc_empty;
   
   // has an output VC been assigned to this input VC?
   output allocated;
   wire 			  allocated;
   
   // if so, which output VC has been assigned?
   output [0:num_vcs-1] allocated_ovc;
   wire [0:num_vcs-1] 		  allocated_ovc;
   
   // credit availability if no VC has been assigned yet
   output free_unallocated;
   wire 			  free_unallocated;
   
   // credit availability if a VC has been assigned
   output free_allocated;
   wire 			  free_allocated;
   
   // internal error condition detected
   output [0:6] errors;
   wire [0:6] 			  errors;
   
   // performance counter events
   // (note: only valid if perf_ctr_enable=1)
   output [0:7] events;
   wire [0:7] 			  events;
   
   
   //---------------------------------------------------------------------------
   // keep track of VC allocation status
   //---------------------------------------------------------------------------
   
   wire 			  flit_sent;
   
   wire 			  packet_done;
   assign packet_done = flit_sent & flit_tail;
   
   wire 			  vc_allocated_s, vc_allocated_q;
   assign vc_allocated_s = (vc_allocated_q | vc_gnt) & ~packet_done;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   vc_allocatedq
     (.clk(clk),
      .reset(reset),
      .d(vc_allocated_s),
      .q(vc_allocated_q));
   
   assign allocated = vc_allocated_q;
   
   wire [0:num_vcs_per_class-1]   vc_gnt_ocvc;
   wire [0:num_vcs_per_class-1]   vc_allocated_ocvc_q;
   
   generate
      
      if(num_vcs_per_class > 1)
	begin
	   
	   wire [0:num_vcs_per_class-1]   vc_gnt_unchanged_ocvc;
	   assign vc_gnt_unchanged_ocvc
	     = vc_gnt_ovc[packet_class*num_vcs_per_class:
			  (packet_class+1)*num_vcs_per_class-1];
	   
	   if(resource_class == (num_resource_classes - 1))
	     begin
		
		assign vc_gnt_ocvc = vc_gnt_unchanged_ocvc;
		
	     end
	   else
	     begin
		
		wire [0:num_vcs_per_class-1] vc_gnt_changed_ocvc;
		assign vc_gnt_changed_ocvc
		  = vc_gnt_ovc[(packet_class+1)*num_vcs_per_class:
			       (packet_class+2)*num_vcs_per_class-1];
		
		// only one of the two will have a bit set, so we just OR them
		assign vc_gnt_ocvc
		  = vc_gnt_changed_ocvc | vc_gnt_unchanged_ocvc;
		
	     end
	   
	   wire [0:num_vcs_per_class-1]      vc_allocated_ocvc_s;
	   assign vc_allocated_ocvc_s
	     = vc_allocated_q ? vc_allocated_ocvc_q : vc_gnt_ocvc;
	   c_dff
	     #(.width(num_vcs_per_class),
	       .reset_type(reset_type))
	   vc_allocated_ocvcq
	     (.clk(clk),
	      .reset(reset),
	      .d(vc_allocated_ocvc_s),
	      .q(vc_allocated_ocvc_q));
	   
	end
      else
	begin
	   
	   assign vc_gnt_ocvc = 1'b1;
	   assign vc_allocated_ocvc_q = 1'b1;
	   
	end
      
      if(resource_class == (num_resource_classes - 1))
	begin
	   
	   c_align
	     #(.data_width(num_vcs_per_class),
	       .dest_width(num_vcs),
	       .offset(packet_class*num_vcs_per_class))
	   allocated_ovc_alg
	     (.data_in(vc_allocated_ocvc_q),
	      .dest_in({num_vcs{1'b0}}),
	      .data_out(allocated_ovc));
	   
	end
      else
	begin
	   
	   c_align
	     #(.data_width(2*num_vcs_per_class),
	       .dest_width(num_vcs),
	       .offset(packet_class*num_vcs_per_class))
	   allocated_ovc_alg
	     (.data_in({{num_vcs_per_class{~inc_rc}} & 
			vc_allocated_ocvc_q,
			{num_vcs_per_class{inc_rc}} & 
			vc_allocated_ocvc_q}),
	      .dest_in({num_vcs{1'b0}}),
	      .data_out(allocated_ovc));
	   
	end
      
      if(sw_alloc_spec_type == `SW_ALLOC_SPEC_TYPE_NONE)
	assign flit_sent = sw_gnt_nonspec;
      else
	assign flit_sent = (vc_gnt & sw_gnt_spec) | sw_gnt_nonspec;
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // generate head and tail indicators
   //---------------------------------------------------------------------------
   
   wire fbc_almost_empty;
   
   generate
      
      if(num_header_buffers > 1)
	begin
	   
	   // keep track of location(s) of head and tail flits in flit buffer
	   wire [0:num_flit_buffers-1] head_queue;
	   wire [0:num_flit_buffers-1] tail_queue;
	   
	   wire [0:num_flit_buffers-1] write_sel;
	   wire [0:num_flit_buffers-1] read_sel;
	   
	   genvar 		       idx;
	   
	   for(idx = 0; idx < num_flit_buffers; idx = idx + 1)
	     begin:idxs
		
		assign write_sel[idx]
			 = (fbc_write_addr == (fbc_min_addr + idx));
		assign read_sel[idx]
			 = (fbc_read_addr == (fbc_min_addr + idx));
		
		wire capture;
		assign capture = flit_valid_in & write_sel[idx];
		
		reg  head_q, tail_q;
		always @(posedge clk)
		  if(capture)
		    begin
		       head_q <= flit_head_in;
		       tail_q <= flit_tail_in;
		    end
		
		assign head_queue[idx] = head_q;
		assign tail_queue[idx] = tail_q;
		
	     end
	   
	   wire head_queue_muxed;
	   c_select_1ofn
	     #(.num_ports(num_flit_buffers),
	       .width(1))
	   head_queue_muxed_sel
	     (.select(read_sel),
	      .data_in(head_queue),
	      .data_out(head_queue_muxed));
	   
	   assign flit_head = fbc_empty ? flit_head_in : head_queue_muxed;
	   
	   wire tail_queue_muxed;
	   c_select_1ofn
	     #(.num_ports(num_flit_buffers),
	       .width(1))
	   tail_queue_muxed_sel
	     (.select(read_sel),
	      .data_in(tail_queue),
	      .data_out(tail_queue_muxed));
	   
	   assign flit_tail = fbc_empty ? flit_tail_in : tail_queue_muxed;
	   
	end
      else
	begin
	   
	   wire head_in;
	   assign head_in = flit_valid_in & flit_head_in;
	   
	   wire head_s, head_q;
	   assign head_s = (head_q | head_in) & ~flit_sent;
	   c_dff
	     #(.width(1),
	       .reset_type(reset_type))
	   headq
	     (.clk(clk),
	      .reset(reset),
	      .d(head_s),
	      .q(head_q));
	   
	   assign flit_head = fbc_empty ? flit_head_in : head_q;
	   
	   wire 		     tail_in;
	   assign tail_in = flit_valid_in & flit_tail_in;
	   
	   wire 		     tail_valid_s, tail_valid_q;
	   assign tail_valid_s
	     = (tail_valid_q | tail_in) & ~(flit_sent & flit_tail);
	   c_dff
	     #(.width(1),
	       .reset_type(reset_type))
	   tail_validq
	     (.clk(clk),
	      .reset(reset),
	      .d(tail_valid_s),
	      .q(tail_valid_q));
	   
	   assign flit_tail
	     = fbc_empty ? flit_tail_in : (tail_valid_q & fbc_almost_empty);
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // header buffer
   //---------------------------------------------------------------------------
   
   wire [0:la_route_info_width-1] la_route_info_in;
   assign la_route_info_in = header_info_in[0:la_route_info_width-1];
   
   wire [0:route_info_width-1] 	  route_info_in;
   assign route_info_in
     = header_info_in[la_route_info_width:
		      la_route_info_width+route_info_width-1];
   
   wire [0:hop_route_info_width-1] hop_route_info_in;
   
   wire 			   hdff_empty;
   
   wire 			   hdff_push;
   assign hdff_push = flit_valid_in & flit_head_in;
   
   wire 			   hdff_pop;
   assign hdff_pop = flit_sent & flit_tail;
   
   wire [0:la_route_info_width-1]  hdff_la_route_info;
   wire [0:hop_route_info_width-1] hdff_hop_route_info;
   wire 			   error_hdff_underflow;
   wire 			   error_hdff_overflow;
   
   generate
      
      if((resource_class == (num_resource_classes - 1)) ||
	 (resource_class == (num_resource_classes - 2)))
	assign hop_route_info_in
	  = route_info_in[route_info_width-hop_route_info_width:
			  route_info_width-1];
      else
	assign hop_route_info_in
	  = route_info_in[resource_class*router_addr_width:
			  (resource_class+2)*router_addr_width-1];
      
      if(num_header_buffers > 1)
	begin
	   
	   wire [0:hdff_width-1] 	  hdff_data_in;
	   assign hdff_data_in[0:la_route_info_width-1] = la_route_info_in;
	   assign hdff_data_in[la_route_info_width:
			       la_route_info_width+hop_route_info_width-1]
		    = hop_route_info_in;
	   
	   wire hdff_full;
	   wire [0:hdff_width-1] hdff_data_out;
	   wire [0:1] 		 hdff_errors;
	   c_fifo
	     #(.depth(num_header_buffers),
	       .width(hdff_width),
	       .fifo_type(header_fifo_type),
	       .reset_type(reset_type))
	   hdff
	     (.clk(clk),
	      .reset(reset),
	      .full(hdff_full),
	      .data_in(hdff_data_in),
	      .push(hdff_push),
	      .empty(hdff_empty),
	      .data_out(hdff_data_out),
	      .pop(hdff_pop),
	      .errors(hdff_errors));
	   
	   assign hdff_la_route_info
	     = hdff_empty ?
	       la_route_info_in :
	       hdff_data_out[0:la_route_info_width-1];
	   assign hdff_hop_route_info
	     = hdff_empty ?
	       hop_route_info_in :
	       hdff_data_out[la_route_info_width:
			     la_route_info_width+hop_route_info_width-1];
	   
	   assign error_hdff_underflow = hdff_errors[0];
	   assign error_hdff_overflow = hdff_errors[1];
	   
	end
      else
	begin
	   
	   wire [0:la_route_info_width-1] hdff_la_route_info_s,
					  hdff_la_route_info_q;
	   assign hdff_la_route_info_s
	     = hdff_push ? la_route_info_in : hdff_la_route_info_q;
	   c_dff
	     #(.width(la_route_info_width),
	       .reset_type(reset_type))
	   hdff_la_route_infoq
	     (.clk(clk),
	      .reset(1'b0),
	      .d(hdff_la_route_info_s),
	      .q(hdff_la_route_info_q));
	   
	   assign hdff_la_route_info
	     = hdff_empty ? la_route_info_in : hdff_la_route_info_q;
	   
	   wire [0:hop_route_info_width-1] hdff_hop_route_info_s,
					   hdff_hop_route_info_q;
	   assign hdff_hop_route_info_s
	     = hdff_push ? hop_route_info_in : hdff_hop_route_info_q;
	   c_dff
	     #(.width(hop_route_info_width),
	       .reset_type(reset_type))
	   hdff_hop_route_infoq
	     (.clk(clk),
	      .reset(1'b0),
	      .d(hdff_hop_route_info_s),
	      .q(hdff_hop_route_info_q));
	   
	   assign hdff_hop_route_info
	     = hdff_empty ? hop_route_info_in : hdff_hop_route_info_q;
	   
	   wire hdff_empty_s, hdff_empty_q;
	   assign hdff_empty_s = (hdff_empty_q | (hdff_pop & ~hdff_push)) &
				 ~(hdff_push & ~hdff_pop);
	   c_dff
	     #(.width(1),
	       .reset_value(1'b1),
	       .reset_type(reset_type))
	   hdff_emptyq
	     (.clk(clk),
	      .reset(reset),
	      .d(hdff_empty_s),
	      .q(hdff_empty_q));
	   
	   assign hdff_empty = hdff_empty_q;
	   
	   assign error_hdff_underflow = hdff_empty_q & ~hdff_push & hdff_pop;
	   assign error_hdff_overflow = ~hdff_empty_q & hdff_push & ~hdff_pop;
	   
	end
      
   endgenerate
   
   wire 	header_valid;
   assign header_valid = ~hdff_empty | flit_valid_in /*hdff_push*/;
   
   wire 	flit_valid;
   
   wire 	error_no_header;
   assign error_no_header = flit_valid & ~header_valid;
   
   
   //---------------------------------------------------------------------------
   // routing logic
   //---------------------------------------------------------------------------
   
   assign route_port = hdff_la_route_info[0:port_idx_width-1];
   
   wire [0:num_ports-1] route_unmasked_op;
   c_decoder
     #(.num_ports(num_ports))
   route_unmasked_op_dec
     (.data_in(route_port),
      .data_out(route_unmasked_op));
   
   wire 		pf_error;
   c_port_filter
     #(.num_message_classes(num_message_classes),
       .num_resource_classes(num_resource_classes),
       .num_ports(num_ports),
       .num_neighbors_per_dim(num_neighbors_per_dim),
       .num_nodes_per_router(num_nodes_per_router),
       .restrict_turns(restrict_turns),
       .connectivity(connectivity),
       .routing_type(routing_type),
       .dim_order(dim_order),
       .port_id(port_id),
       .message_class(message_class),
       .resource_class(resource_class))
   route_op_pf
     (.route_in_op(route_unmasked_op),
      .inc_rc(inc_rc),
      .route_out_op(route_op),
      .error(pf_error));
   
   wire 		error_invalid_port;
   assign error_invalid_port = header_valid & pf_error;
   
   generate
      
      if(resource_class < (num_resource_classes - 1))
	assign inc_rc = hdff_la_route_info[port_idx_width];
      else
	assign inc_rc = 1'b0;
      
   endgenerate
   
   vcr_la_routing_logic
     #(.num_resource_classes(num_resource_classes),
       .num_routers_per_dim(num_routers_per_dim),
       .num_dimensions(num_dimensions),
       .num_nodes_per_router(num_nodes_per_router),
       .connectivity(connectivity),
       .routing_type(routing_type),
       .dim_order(dim_order),
       .resource_class(resource_class),
       .reset_type(reset_type))
   lar
     (.clk(clk),
      .reset(reset),
      .router_address(router_address),
      .route_port(route_port),
      .inc_rc(inc_rc),
      .hop_route_info(hdff_hop_route_info),
      .la_route_info(la_route_info));
   
   
   //---------------------------------------------------------------------------
   // credit tracking
   //---------------------------------------------------------------------------
   
   wire [0:num_vcs*int_flow_ctrl_width-1] int_flow_ctrl_ovc;
   assign int_flow_ctrl_ovc
     = int_flow_ctrl_op_ovc[route_port*num_vcs*int_flow_ctrl_width +:
			    num_vcs*int_flow_ctrl_width];
   
   wire 				  error_ct_overflow;
   
   generate
      
      case(int_flow_ctrl_type)
	
	// the current number of credits available are transmitted from the 
	// output controllers as a multi-level signal (0, 1, 2 or 3+ credits
	// currently available)
	`INT_FLOW_CTRL_TYPE_LEVEL:
	  begin
	     
	     // credit status for the case where we do not change resource class
	     wire [0:num_vcs_per_class*2-1] free_unchanged_ocvc;
	     assign free_unchanged_ocvc
	       = int_flow_ctrl_ovc[packet_class*num_vcs_per_class*2:
				   (packet_class+1)*num_vcs_per_class*2-1];
	     
	     wire [0:num_vcs_per_class*2-1] free_ocvc;
	     
	     if(resource_class == (num_resource_classes - 1))
	       assign free_ocvc = free_unchanged_ocvc;
	     else
	       begin
		  
		  // credit status in case we change resource class
		  wire [0:num_vcs_per_class*2-1] free_changed_ocvc;
		  assign free_changed_ocvc
		    = int_flow_ctrl_ovc[(packet_class+1)*num_vcs_per_class*2:
					(packet_class+2)*num_vcs_per_class*2-1];
		  
		  assign free_ocvc
		    = inc_rc ? free_changed_ocvc : free_unchanged_ocvc;
		  
	       end
	     
	     wire 				 flit_sent_prev_s,
						 flit_sent_prev_q;
	     assign flit_sent_prev_s = flit_sent /*& ~flit_tail*/;
	     c_dff
	       #(.width(1),
		 .reset_type(reset_type))
	     flit_sent_prevq
	       (.clk(clk),
		.reset(reset),
		.d(flit_sent_prev_s),
		.q(flit_sent_prev_q));
	     
	     wire [0:1] 			 flit_sent_count;
	     assign flit_sent_count = flit_sent_prev_q + flit_sent;
	     
	     wire [0:num_vcs_per_class-1] 	 next_free_ocvc;
	     wire [0:num_vcs_per_class-1] 	 free_unallocated_ocvc;
	     
	     genvar 				 ocvc;
	     
	     for(ocvc = 0; ocvc < num_vcs_per_class; ocvc = ocvc + 1)
	       begin:ocvcs
		  
		  wire [0:1] free;
		  assign free = free_ocvc[ocvc*2:(ocvc+1)*2-1];
		  
		  wire [0:2] free_by_count;
		  assign free_by_count = {|free, free[0], &free};
		  
		  // NOTE: For allocated VCs, the free state must be updated 
		  // depending on how many flits were sent to the VC that are 
		  // not already reflected in the incoming credit count.
		  
		  wire       next_free;
		  assign next_free = free_by_count[flit_sent_count];
		  
		  assign next_free_ocvc[ocvc] = next_free;
		  
		  // NOTE: When sending the first flit in each packet, we must 
		  // have won VC allocation. Consequently, the destination 
		  // output VC must have been eligible and thus not assigned to 
		  // any input VC, so we know that nobody sent a flit to it in 
		  // the previous cycle.
		  
		  wire 	     free_unallocated;
		  assign free_unallocated = |free;
		  
		  assign free_unallocated_ocvc[ocvc] = free_unallocated;
		  
	       end
	     
	     wire 	     free_allocated_s, free_allocated_q;
	     assign free_allocated_s
	       = |(next_free_ocvc & 
		   (vc_allocated_q ? vc_allocated_ocvc_q : vc_gnt_ocvc));
	     c_dff
	       #(.width(1),
		 .reset_value(1'b1),
		 .reset_type(reset_type))
	     free_allocatedq
	       (.clk(clk),
		.reset(reset),
		.d(free_allocated_s),
		.q(free_allocated_q));
	     
	     assign free_allocated = free_allocated_q;
	     
	     assign free_unallocated = |(free_unallocated_ocvc & vc_gnt_ocvc);
	     
	     assign error_ct_overflow = 1'b0;
	     
	  end
	
	// credits are forwarded from the output controllers individually as 
	// pulses, accumulated at the input controllers, and used whenever a
	// flit is sent to the crossbar
	`INT_FLOW_CTRL_TYPE_PUSH:
	  begin
	     
	     // credit indicator for the case where we do not change the 
	     // resource class
	     wire [0:num_vcs_per_class-1] cred_unchanged_ocvc;
	     assign cred_unchanged_ocvc
	       = int_flow_ctrl_ovc[packet_class*num_vcs_per_class:
				   (packet_class+1)*num_vcs_per_class-1];
	     
	     wire [0:num_vcs_per_class-1] cred_ocvc;
	     
	     if(resource_class == (num_resource_classes - 1))
	       assign cred_ocvc = cred_unchanged_ocvc;
	     else
	       begin
		  
		  // credit status for the case where the resource class changes
		  wire [0:num_vcs_per_class-1] cred_changed_ocvc;
		  assign cred_changed_ocvc
		    = int_flow_ctrl_ovc[(packet_class+1)*num_vcs_per_class:
					(packet_class+2)*num_vcs_per_class-1];
		  
		  assign cred_ocvc = inc_rc ?
				     cred_changed_ocvc :
				     cred_unchanged_ocvc;
		  
	       end
	     
	     wire 			       cred_allocated;
	     assign cred_allocated = |(cred_ocvc & vc_allocated_ocvc_q);
	     
	     wire 			       cred_unallocated;
	     assign cred_unallocated = |(cred_ocvc & vc_gnt_ocvc);
	     
	     wire [0:cred_hold_width-1]        cred_hold_q;
	     
	     wire 			       has_cred_s, has_cred_q;
	     if(vc_alloc_requires_credit)
	       assign has_cred_s 
		 = vc_allocated_q ? 
		   ((has_cred_q & ~sw_gnt_nonspec) | (|cred_hold_q) | 
		    cred_allocated) :
		   (~sw_gnt_spec | cred_unallocated);
	     else
	       assign has_cred_s
		 = vc_allocated_q ?
		   ((has_cred_q & ~sw_gnt_nonspec) | (|cred_hold_q) | 
		    cred_allocated) :
		   (~sw_gnt_spec & cred_unallocated);
	     c_dff
	       #(.width(1),
		 .reset_type(reset_type))
	     has_credq
	       (.clk(clk),
		.reset(reset),
		.d(has_cred_s),
		.q(has_cred_q));
	     
	     assign free_allocated = has_cred_q;
	     
	     assign free_unallocated = cred_unallocated;
	     
	     wire 			       incr_cred_hold;
	     assign incr_cred_hold
	       = cred_allocated & ~sw_gnt_nonspec & has_cred_q;
	     
	     wire 			       decr_cred_hold;
	     assign decr_cred_hold
	       = ~cred_allocated & sw_gnt_nonspec & |cred_hold_q;
	     
	     wire [0:cred_hold_width-1]        cred_hold_new;
	     if(vc_alloc_requires_credit)
	       assign cred_hold_new = ~sw_gnt_spec & cred_unallocated;
	     else
	       assign cred_hold_new = {cred_hold_width{1'b0}};
	     
	     wire [0:cred_hold_width-1]        cred_hold_s;
	     assign cred_hold_s
	       = vc_allocated_q ?
		 (cred_hold_q + incr_cred_hold - decr_cred_hold) :
		 cred_hold_new;
	     c_dff
	       #(.width(cred_hold_width),
		 .reset_type(reset_type))
	     cred_holdq
	       (.clk(clk),
		.reset(reset),
		.d(cred_hold_s),
		.q(cred_hold_q));
	     
	     assign error_ct_overflow
	       = vc_allocated_q && (cred_hold_q == num_flit_buffers-1) &&
		 incr_cred_hold;
	     
	  end
	
      endcase
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // generate allocator control signals
   //---------------------------------------------------------------------------
   
   assign sw_req_nonspec = flit_valid & vc_allocated_q;
   
   generate
      
      if(sw_alloc_spec_type == `SW_ALLOC_SPEC_TYPE_NONE)
	assign sw_req_spec = 1'b0;
      else
	assign sw_req_spec = flit_valid & ~vc_allocated_q;
      
   endgenerate
   
   assign vc_req = header_valid & ~vc_allocated_q;
   
   
   //---------------------------------------------------------------------------
   // flit buffer control
   //---------------------------------------------------------------------------
   
   wire 		       fbc_push;
   assign fbc_push = flit_valid_in;
   
   wire 		       fbc_pop;
   assign fbc_pop = flit_sent;
   
   wire [0:1] 		       fbc_errors;
   wire 		       fbc_almost_full;
   wire 		       fbc_full;
   c_fifo_ctrl
     #(.addr_width(fbf_addr_width),
       .offset(fbc_min_addr),
       .depth(num_flit_buffers),
       .reset_type(reset_type))
   fbc
     (.clk(clk),
      .reset(reset),
      .push(fbc_push),
      .pop(fbc_pop),
      .write_addr(fbc_write_addr),
      .read_addr(fbc_read_addr),
      .almost_empty(fbc_almost_empty),
      .empty(fbc_empty),
      .almost_full(fbc_almost_full),
      .full(fbc_full),
      .errors(fbc_errors));
   
   wire 		       error_fbc_underflow;
   assign error_fbc_underflow = fbc_errors[0];
   
   wire 		       error_fbc_overflow;
   assign error_fbc_overflow = fbc_errors[1];
   
   assign flit_valid = flit_valid_in | ~fbc_empty;
   
   
   //---------------------------------------------------------------------------
   // error checking
   //---------------------------------------------------------------------------
   
   assign errors = {error_fbc_underflow,
		    error_fbc_overflow,
		    error_hdff_underflow,
		    error_hdff_overflow,
		    error_no_header,
		    error_ct_overflow,
		    error_invalid_port};
   
   
   //---------------------------------------------------------------------------
   // instrumentation
   //---------------------------------------------------------------------------
   
   generate
      
      if(perf_ctr_enable > 0)
	begin
	   
	   wire 		       ev_no_flit;
	   assign ev_no_flit = header_valid & ~flit_valid;
	   
	   wire 		       ev_flit_ready;
	   assign ev_flit_ready = header_valid & flit_valid;
	   
	   wire 		       ev_flit_stalled_vc;
	   assign ev_flit_stalled_vc
	     = header_valid & flit_valid & ~vc_allocated_q & ~vc_gnt;
	   
	   wire 		       ev_flit_stalled_sw;
	   if(sw_alloc_spec_type == `SW_ALLOC_SPEC_TYPE_NONE)
	     assign ev_flit_stalled_sw
	       = header_valid & flit_valid & vc_allocated_q & ~sw_gnt_nonspec;
	   else
	     assign ev_flit_stalled_sw
	       = header_valid & flit_valid & 
		 (vc_allocated_q & ~sw_gnt_nonspec) | 
		 (vc_gnt & ~sw_gnt_spec);
	   
	   wire 		       ev_spec_attempted;
	   assign ev_spec_attempted
	     = header_valid & flit_valid & ~vc_allocated_q;
	   
	   wire 		       ev_spec_failed;
	   if(sw_alloc_spec_type == `SW_ALLOC_SPEC_TYPE_NONE)
	     assign ev_spec_failed = 1'b0;
	   else
	     assign ev_spec_failed
	       = header_valid & flit_valid & ~vc_allocated_q & 
		 ~vc_gnt & sw_gnt_spec;
	   
	   wire 		       ev_flit_sent;
	   assign ev_flit_sent = flit_sent;
	   
	   wire 		       ev_flit_sent_spec;
	   assign ev_flit_sent_spec = flit_sent & ~vc_allocated_q;
	   
	   wire [0:7] 		       events_s, events_q;
	   assign events_s = {ev_no_flit,
			      ev_flit_ready,
			      ev_flit_stalled_vc,
			      ev_flit_stalled_sw,
			      ev_spec_attempted,
			      ev_spec_failed,
			      ev_flit_sent,
			      ev_flit_sent_spec};
	   c_dff
	     #(.width(8),
	       .reset_type(reset_type))
	   eventsq
	     (.clk(clk),
	      .reset(reset),
	      .d(events_s),
	      .q(events_q));
	   assign events = events_q;
	   
	end
      else
	assign events = 8'd0;
      
   endgenerate
   
endmodule
