// $Id: whr_ip_ctrl_mac.v 2076 2010-06-01 03:41:42Z dub $

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



// input port controller
module whr_ip_ctrl_mac
  (clk, reset, router_address, flit_ctrl_in, flit_data_in, req_op, req_head, 
   req_tail, gnt_op, flit_data_out, flow_ctrl_out, fbf_write_addr, 
   fbf_write_enable, fbf_write_data, fbf_read_enable, fbf_read_addr, 
   fbf_read_data, error);
   
`include "c_functions.v"
`include "c_constants.v"
`include "whr_constants.v"
   
   // flit buffer entries per VC
   parameter num_flit_buffers = 8;
   
   // width required to select individual buffer slot
   localparam flit_buffer_idx_width = clogb(num_flit_buffers);
   
   // maximum number of packets that can be in a given VC buffer simultaneously
   parameter num_header_buffers = 4;
   
   // number of routers in each dimension
   parameter num_routers_per_dim = 4;
   
   // width required to select individual router in a dimension
   localparam dim_addr_width = clogb(num_routers_per_dim);
   
   // number of dimensions in network
   parameter num_dimensions = 2;
   
   // width required to select individual router in entire network
   localparam router_addr_width = num_dimensions * dim_addr_width;
   
   // number of nodes per router (a.k.a. consentration factor)
   parameter num_nodes_per_router = 1;
   
   // width required to select individual node at current router
   localparam node_addr_width = clogb(num_nodes_per_router);
   
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
   localparam la_route_info_width = port_idx_width;
   
   // width of global addresses
   localparam addr_width = router_addr_width + node_addr_width;
   
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
   
   // total number of bits required for storing routing information
   localparam route_info_width = addr_width;
   
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
       (1 + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + 1) : 
       -1;
   
   // width of flit payload data
   parameter flit_data_width = 64;
   
   // width of flow control signals
   localparam flow_ctrl_width = 1;
   
   // filter out illegal destination ports
   // (the intent is to allow synthesis to optimize away the logic associated 
   // with such turns)
   parameter restrict_turns = 1;
   
   // select routing function type
   parameter routing_type = `ROUTING_TYPE_DOR;
   
   // select order of dimension traversal
   parameter dim_order = `DIM_ORDER_ASCENDING;
   
   // select implementation variant for header FIFO
   parameter header_fifo_type = `FIFO_TYPE_INDEXED;
   
   // total number of bits of header information required for each VC
   localparam hdff_width = la_route_info_width + route_info_width;
   
   // use input register as part of the flit buffer
   parameter input_stage_can_hold = 0;
   
   // number of entries in flit buffer
   localparam fbf_depth
     = input_stage_can_hold ? (num_flit_buffers - 1) : num_flit_buffers;
   
   // required address size for flit buffer
   localparam fbf_addr_width = clogb(fbf_depth);
   
   // enable performance counter
   parameter perf_ctr_enable = 1;
   
   // width of each counter
   parameter perf_ctr_width = 32;
   
   // configure error checking logic
   parameter error_capture_mode = `ERROR_CAPTURE_MODE_NO_HOLD;
   
   // ID of current input port
   parameter port_id = 0;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
      
   input clk;
   input reset;
   
   // current router's address
   input [0:router_addr_width-1] router_address;
   
   // incoming flit control signals
   input [0:flit_ctrl_width-1] flit_ctrl_in;
   
   // incoming flit data
   input [0:flit_data_width-1] flit_data_in;
   
   // request for output port
   output [0:num_ports-1] req_op;
   wire [0:num_ports-1]        req_op;
   
   // current flit is head flit
   output req_head;
   wire 		       req_head;
   
   // current flit is tail flit
   output req_tail;
   wire 		       req_tail;
   
   // output port granted
   input [0:num_ports-1] gnt_op;
   
   // outgoing flit data
   output [0:flit_data_width-1] flit_data_out;
   wire [0:flit_data_width-1] 	  flit_data_out;
   
   // outgoing flow control signals
   output [0:flow_ctrl_width-1] flow_ctrl_out;
   wire [0:flow_ctrl_width-1] 	  flow_ctrl_out;
   
   // flit buffer write address
   output [0:fbf_addr_width-1] fbf_write_addr;
   wire [0:fbf_addr_width-1] 	  fbf_write_addr;
   
   // flit buffer write enable
   output fbf_write_enable;
   wire 			  fbf_write_enable;
   
   // flit buffer write data
   output [0:flit_data_width-1] fbf_write_data;
   wire [0:flit_data_width-1] 	  fbf_write_data;
   
   // flit buffer read enable
   output fbf_read_enable;
   wire 			  fbf_read_enable;
   
   // flit buffer read address
   output [0:fbf_addr_width-1] fbf_read_addr;
   wire [0:fbf_addr_width-1] 	  fbf_read_addr;
   
   // flit buffer read data
   input [0:flit_data_width-1] fbf_read_data;
   
   // internal error condition detected
   output error;
   wire 			  error;
   
   
   //---------------------------------------------------------------------------
   // input stage
   //---------------------------------------------------------------------------
   
   wire 			  flit_valid_in;
   wire 			  flit_valid_out;
   
   wire 			  fbc_almost_full;
   
   wire 			  hold_input_enable;
   
   generate
      
      if(input_stage_can_hold)
	begin
	   
	   wire hold_input_enable_set;
	   assign hold_input_enable_set = fbc_almost_full & flit_valid_in;
	   
	   wire hold_input_enable_reset;
	   assign hold_input_enable_reset = flit_valid_out;
	   
	   wire hold_input_enable_s, hold_input_enable_q;
	   assign hold_input_enable_s
	     = (hold_input_enable_q | hold_input_enable_set) & 
	       ~hold_input_enable_reset;
	   c_dff
	     #(.width(1),
	       .reset_type(reset_type))
	   hold_input_enableq
	     (.clk(clk),
	      .reset(reset),
	      .d(hold_input_enable_s),
	      .q(hold_input_enable_q));
	   
	   assign hold_input_enable = hold_input_enable_q;
	   
	end
      else
	assign hold_input_enable = 1'b0;
      
   endgenerate
   
   wire 	flit_valid_thru;
   assign flit_valid_thru = flit_valid_in & ~hold_input_enable;
   
   wire 	hold_input_stage_b;
   assign hold_input_stage_b = ~(flit_valid_in & hold_input_enable);
   
   wire [0:flit_ctrl_width-1] 	  flit_ctrl_in_s, flit_ctrl_in_q;
   assign flit_ctrl_in_s = hold_input_stage_b ? flit_ctrl_in : flit_ctrl_in_q;
	   
   wire [0:flit_data_width-1] 	  flit_data_in_s, flit_data_in_q;
   assign flit_data_in_s = hold_input_stage_b ? flit_data_in : flit_data_in_q;
   
   generate
	   
      case(packet_format)
	
	`PACKET_FORMAT_HEAD_TAIL,
	`PACKET_FORMAT_EXPLICIT_LENGTH:
	  begin
	     
	     wire                 flit_valid_in_s, flit_valid_in_q;
	     assign flit_valid_in_s = flit_ctrl_in_s[0];
	     c_dff
	       #(.width(1),
		 .reset_type(reset_type))
	     flit_valid_inq
	       (.clk(clk),
		.reset(reset),
		.d(flit_valid_in_s),
		.q(flit_valid_in_q));
	     
	     assign flit_ctrl_in_q[0] = flit_valid_in_q;
	     
	     c_dff
	       #(.width(flit_ctrl_width-1),
		 .reset_type(reset_type))
	     flit_ctrl_inq
	       (.clk(clk),
		.reset(1'b0),
		.d(flit_ctrl_in_s[1:flit_ctrl_width-1]),
		.q(flit_ctrl_in_q[1:flit_ctrl_width-1]));
	     
	  end
	
      endcase
      
   endgenerate
   
   c_dff
     #(.width(flit_data_width),
       .reset_type(reset_type))
   flit_data_inq
     (.clk(clk),
      .reset(1'b0),
      .d(flit_data_in_s),
      .q(flit_data_in_q));
   
   // extract header information from header data
   wire [0:header_info_width-1] header_info_in_q;
   assign header_info_in_q = flit_data_in_q[0:header_info_width-1];
   
   
   //---------------------------------------------------------------------------
   // decode control signals
   //---------------------------------------------------------------------------
   
   wire 			flit_head_in;   
   wire 			flit_tail_in;
   
   generate
      
      case(packet_format)
	
	`PACKET_FORMAT_HEAD_TAIL,
	`PACKET_FORMAT_EXPLICIT_LENGTH:
	  begin
	     
	     assign flit_valid_in = flit_ctrl_in_q[0];
	     assign flit_head_in = flit_ctrl_in_q[1];
	     
	  end
	
      endcase
      
      case(packet_format)
	
	`PACKET_FORMAT_HEAD_TAIL:
	  begin
	     assign flit_tail_in = flit_ctrl_in_q[2];
	  end
	
	`PACKET_FORMAT_EXPLICIT_LENGTH:
	  begin
	     
	     if(max_payload_length == 0)
	       assign flit_tail_in = flit_head_in;
	     else if(max_payload_length == 1)
	       begin
		  
		  wire has_payload;
		  
		  if(min_payload_length == 0)
		    assign has_payload
		      = header_info_in_q[la_route_info_width+route_info_width];
		  else
		    assign has_payload = 1'b1;
		  
		  assign flit_tail_in = ~flit_head_in | ~has_payload;
		  
	       end
	     else
	       begin
		  
		  wire flit_ctr_zero;
		  wire [0:flit_ctr_width-1] flit_ctr_next;
		  
		  if(payload_length_width > 0)
		    begin
		       
		       wire [0:payload_length_width-1] payload_length;
		       assign payload_length
			 = header_info_in_q[la_route_info_width+
					    route_info_width:
					    la_route_info_width+
					    route_info_width+
					    payload_length_width-1];
		       
		       assign flit_ctr_next
			 = (min_payload_length - 1) + payload_length;
		       
		       if(min_payload_length == 0)
			 assign flit_tail_in
			   = flit_head_in ? ~|payload_length : flit_ctr_zero;
		       else
			 assign flit_tail_in = ~flit_head_in & flit_ctr_zero;
		       
		    end
		  else
		    begin
		       assign flit_ctr_next = max_payload_length - 1;
		       assign flit_tail_in = ~flit_head_in & flit_ctr_zero;
		    end
		  
		  wire [0:flit_ctr_width-1] flit_ctr_s, flit_ctr_q;
		  assign flit_ctr_s = flit_valid_in ? 
				      (flit_head_in ? 
				       flit_ctr_next : 
				       (flit_ctr_q - hold_input_stage_b)) : 
				      flit_ctr_q;
		  c_dff
		    #(.width(flit_ctr_width),
		      .reset_type(reset_type))
		  flit_ctrq
		    (.clk(clk),
		     .reset(reset),
		     .d(flit_ctr_s),
		     .q(flit_ctr_q));
		  
		  assign flit_ctr_zero = ~|flit_ctr_q;
		  
	       end
	     
	  end
	
      endcase
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // generate head and tail indicators
   //---------------------------------------------------------------------------

   wire                                     gnt;
   assign gnt = |gnt_op;
   
   assign flit_valid_out = gnt;

   wire 				    fbc_empty;
   
   wire 				    flit_valid;
   assign flit_valid = flit_valid_in | ~fbc_empty;
   
   wire 				    fbc_almost_empty;
   wire [0:fbf_addr_width-1] 		    fbc_write_addr;
   wire [0:fbf_addr_width-1] 		    fbc_read_addr;
   
   wire 				    flit_head_out;
   wire 				    flit_tail_out;
   
   generate
      
      if(num_header_buffers > 1)
	begin
	   
	   // keep track of location(s) of head and tail flits in flit buffer
	   reg [0:fbf_depth-1] head_queue;
	   reg [0:fbf_depth-1] tail_queue;
	   
	   always @(posedge clk)
	     if(flit_valid_thru)
		begin
		   head_queue[fbc_write_addr] <= flit_head_in;
		   tail_queue[fbc_write_addr] <= flit_tail_in;
		end
	   
	   wire head_queue_muxed;
	   assign head_queue_muxed = head_queue[fbc_read_addr];
	   
	   assign flit_head_out = fbc_empty ? flit_head_in : head_queue_muxed;
	   
	   wire tail_queue_muxed;
	   assign tail_queue_muxed = tail_queue[fbc_read_addr];
	   
	   assign flit_tail_out = fbc_empty ? flit_tail_in : tail_queue_muxed;
	   
	end
      else
	begin
	   
	   wire head_in;
	   assign head_in = flit_valid_thru & flit_head_in;
	   
	   wire head_valid_s, head_valid_q;
	   assign head_valid_s = flit_valid ? 
				 ((head_valid_q | head_in) & ~flit_valid_out) : 
				 head_valid_q;
	   c_dff
	     #(.width(1),
	       .reset_type(reset_type))
	   head_validq
	     (.clk(clk),
	      .reset(reset),
	      .d(head_valid_s),
	      .q(head_valid_q));
	   
	   assign flit_head_out = fbc_empty ? flit_head_in : head_valid_q;
	   
	   wire 		     tail_in;
	   assign tail_in = flit_valid_thru & flit_tail_in;
	   
	   wire 		     tail_valid_s, tail_valid_q;
	   assign tail_valid_s = flit_valid ? 
				 ((tail_valid_q | tail_in) & 
				  ~(flit_valid_out & flit_tail_out)) : 
				 tail_valid_q;
	   c_dff
	     #(.width(1),
	       .reset_type(reset_type))
	   tail_validq
	     (.clk(clk),
	      .reset(reset),
	      .d(tail_valid_s),
	      .q(tail_valid_q));
	   
	   assign flit_tail_out
	     = fbc_empty ? flit_tail_in : (tail_valid_q & fbc_almost_empty);
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // header buffer
   //---------------------------------------------------------------------------
   
   wire [0:la_route_info_width-1]    la_route_info_in;
   assign la_route_info_in = header_info_in_q[0:la_route_info_width-1];
   
   wire [0:route_info_width-1] 	     route_info_in;
   assign route_info_in
     = header_info_in_q[la_route_info_width:
			la_route_info_width+route_info_width-1];
   
   wire 			     hdff_empty;
   
   wire 			     hdff_push;
   assign hdff_push = flit_valid_thru & flit_head_in;
   
   wire 			     hdff_pop;
   assign hdff_pop = flit_valid_out & flit_tail_out;
   
   wire [0:la_route_info_width-1]    hdff_la_route_info;
   wire [0:route_info_width-1] 	     hdff_route_info;
   wire 			     error_hdff_underflow;
   wire 			     error_hdff_overflow;
   
   generate
      
      if(num_header_buffers > 1)
	begin
	   
	   wire [0:hdff_width-1] hdff_data_in;
	   assign hdff_data_in[0:la_route_info_width-1] = la_route_info_in;
	   assign hdff_data_in[la_route_info_width:
			       la_route_info_width+route_info_width-1]
		    = route_info_in;
	   
	   wire 		 hdff_full;
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
	   assign hdff_route_info
	     = hdff_empty ?
	       route_info_in :
	       hdff_data_out[la_route_info_width:
			     la_route_info_width+route_info_width-1];
	   
	   assign error_hdff_underflow = hdff_errors[0];
	   assign error_hdff_overflow = hdff_errors[1];
	   
	end
      else
	begin
	   
	   wire [0:la_route_info_width-1] hdff_la_route_info_s,
					  hdff_la_route_info_q;
	   assign hdff_la_route_info_s
	     = flit_valid_in ? 
	       (hdff_push ? la_route_info_in : hdff_la_route_info_q) : 
	       hdff_la_route_info_q;
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
	   
	   wire [0:route_info_width-1] hdff_route_info_s, hdff_route_info_q;
	   assign hdff_route_info_s
	     = flit_valid_in ? 
	       (hdff_push ? route_info_in : hdff_route_info_q) : 
	       hdff_route_info_q;
	   c_dff
	     #(.width(route_info_width),
	       .reset_type(reset_type))
	   hdff_route_infoq
	     (.clk(clk),
	      .reset(1'b0),
	      .d(hdff_route_info_s),
	      .q(hdff_route_info_q));
	   
	   assign hdff_route_info
	     = hdff_empty ? route_info_in : hdff_route_info_q;
	   
	   wire hdff_empty_s, hdff_empty_q;
	   assign hdff_empty_s = flit_valid ? 
				 ((hdff_empty_q | (hdff_pop & ~hdff_push)) & 
				  ~(hdff_push & ~hdff_pop)) : 
				 hdff_empty_q;
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
	   
	   assign error_hdff_underflow = hdff_empty_q & hdff_pop & ~hdff_push;
	   assign error_hdff_overflow = ~hdff_empty_q & hdff_push & ~hdff_pop;
	   
	end
      
   endgenerate
   
   wire 	header_valid;
   assign header_valid = ~hdff_empty | hdff_push;
   
   wire 	error_no_header;
   assign error_no_header = flit_valid & ~header_valid;
   
   
   //---------------------------------------------------------------------------
   // routing logic
   //---------------------------------------------------------------------------
   
   wire [0:port_idx_width-1] route_port;
   assign route_port = hdff_la_route_info[0:port_idx_width-1];
   
   wire [0:num_ports-1] route_unmasked_op;
   c_decoder
     #(.num_ports(num_ports))
   route_unmasked_op_dec
     (.data_in(route_port),
      .data_out(route_unmasked_op));
   
   wire [0:num_ports-1]      route_op;
   wire 		     pf_error;
   c_port_filter
     #(.num_message_classes(1),
       .num_resource_classes(1),
       .num_ports(num_ports),
       .num_neighbors_per_dim(num_neighbors_per_dim),
       .num_nodes_per_router(num_nodes_per_router),
       .restrict_turns(restrict_turns),
       .connectivity(connectivity),
       .routing_type(routing_type),
       .dim_order(dim_order),
       .port_id(port_id),
       .message_class(0),
       .resource_class(0))
   route_op_pf
     (.route_in_op(route_unmasked_op),
      .inc_rc(1'b0),
      .route_out_op(route_op),
      .error(pf_error));
   
   wire 		     error_invalid_port;
   assign error_invalid_port = header_valid & pf_error;
   
   wire [0:la_route_info_width-1] la_route_info_out;
   whr_la_routing_logic
     #(.num_routers_per_dim(num_routers_per_dim),
       .num_dimensions(num_dimensions),
       .num_nodes_per_router(num_nodes_per_router),
       .connectivity(connectivity),
       .routing_type(routing_type),
       .dim_order(dim_order),
       .reset_type(reset_type))
   lar
     (.clk(clk),
      .reset(reset),
      .router_address(router_address),
      .route_op(route_op),
      .route_info(hdff_route_info),
      .la_route_info(la_route_info_out));
   
   
   //---------------------------------------------------------------------------
   // flit buffer control
   //---------------------------------------------------------------------------
   
   wire 		     fbc_push;
   assign fbc_push = flit_valid_thru;
   
   wire 		     fbc_pop;
   assign fbc_pop = flit_valid_out;
   
   wire 		     fbc_full;
   wire [0:1]		     fbc_errors;
   c_fifo_ctrl
     #(.addr_width(fbf_addr_width),
       .depth(fbf_depth),
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
   
   wire 		     error_fbc_underflow;
   assign error_fbc_underflow = fbc_errors[0];
   
   wire 		     error_fbc_overflow;
   assign error_fbc_overflow = fbc_errors[1];
   
   // read enable doesn't need to be exact, so we read whenever we have a flit
   assign fbf_read_enable = flit_valid /*flit_valid_out*/;
   
   assign fbf_read_addr = fbc_read_addr;
   assign fbf_write_enable = flit_valid_thru;
   assign fbf_write_addr = fbc_write_addr;
   assign fbf_write_data = flit_data_in_q;
   
   
   //---------------------------------------------------------------------------
   // generate control signals to switch allocator
   //---------------------------------------------------------------------------
   
   wire                      req;
   assign req = flit_valid;
   
   assign req_op = {num_ports{req}} & route_op;
   
   assign req_head = flit_head_out;
   assign req_tail = flit_tail_out;
   
   
   //---------------------------------------------------------------------------
   // generate outputs to switch
   //---------------------------------------------------------------------------
   
   wire                      flit_head_out_s, flit_head_out_q;
   assign flit_head_out_s = flit_valid ? flit_head_out : flit_head_out_q;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   flit_head_outq
     (.clk(clk),
      .reset(1'b0),
      .d(flit_head_out_s),
      .q(flit_head_out_q));
   
   wire [0:la_route_info_width-1] la_route_info_s, la_route_info_q;
   assign la_route_info_s = flit_valid ? la_route_info_out : la_route_info_q;
   c_dff
     #(.width(la_route_info_width),
       .reset_type(reset_type))
   la_route_infoq
     (.clk(clk),
      .reset(1'b0),
      .d(la_route_info_s),
      .q(la_route_info_q));
   
   assign flit_data_out[0:la_route_info_width-1]
	    = flit_head_out_q ? 
	      la_route_info_q : 
	      fbf_read_data[0:la_route_info_width-1];
   assign flit_data_out[la_route_info_width:flit_data_width-1]
	    = fbf_read_data[la_route_info_width:flit_data_width-1];
   
   
   //---------------------------------------------------------------------------
   // generate outgoing credits
   //---------------------------------------------------------------------------
   
   wire 			  cred_valid_s, cred_valid_q;
   assign cred_valid_s = flit_valid_out;
   c_dff
     #(.width(1),
       .reset_type(reset_type))
   cred_validq
     (.clk(clk),
      .reset(reset),
      .d(cred_valid_s),
      .q(cred_valid_q));
   
   assign flow_ctrl_out[0] = cred_valid_q;
   
   
   //---------------------------------------------------------------------------
   // error checking
   //---------------------------------------------------------------------------
   
   generate
      
      if(error_capture_mode != `ERROR_CAPTURE_MODE_NONE)
	begin

	   // synopsys translate_off
	   always @(posedge clk)
	     begin
		
		if(error_fbc_underflow)
		     $display({"ERROR: Flit buffer controller underflow in ", 
			       "module %m."});
		
		if(error_fbc_overflow)
		     $display({"ERROR: Flit buffer controller overflow in ", 
			       "module %m."});
		
		if(error_hdff_underflow)
		  $display("ERROR: Head FIFO underflow in module %m.");
		
		if(error_hdff_overflow)
		     $display("ERROR: Head FIFO overflow in module %m.");
		
		if(error_no_header)
		     $display("ERROR: Stray flit received in module %m.");
		
		if(error_invalid_port)
		     $display({"ERROR: Received flit's destination does not ", 
			       "match constraints for input port %d in ",
			       "module %m."}, 
			      port_id);
		
	     end
	   // synopsys translate_on
	   
	   wire [0:5] errors_s, errors_q;
	   assign errors_s = {error_fbc_underflow,
			      error_fbc_overflow,
			      error_hdff_underflow,
			      error_hdff_overflow,
			      error_no_header,
			      error_invalid_port};
	   c_err_rpt
	     #(.num_errors(6),
	       .capture_mode(error_capture_mode),
	       .reset_type(reset_type))
	   chk
	     (.clk(clk),
	      .reset(reset),
	      .errors_in(errors_s),
	      .errors_out(errors_q));
	   
	   assign error = |errors_q;
	   
	end
      else
	assign error = 1'b0;
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // instrumentation / performance counters
   //---------------------------------------------------------------------------
   
   generate
      
      if(perf_ctr_enable > 0)
	begin
	   
	   wire 		       ev_no_flit;
	   assign ev_no_flit = header_valid & ~flit_valid;
	   
	   wire 		       ev_flit_stalled_sw;
	   assign ev_flit_stalled_sw = header_valid & flit_valid & ~gnt;
	   
	   wire 		       ev_flit_sent;
	   assign ev_flit_sent = flit_valid_out;
	   
	   wire [0:2] 		       events_s, events_q;
	   assign events_s = {ev_no_flit,
			      ev_flit_stalled_sw,
			      ev_flit_sent};
	   c_dff
	     #(.width(3),
	       .reset_type(reset_type))
	   eventsq
	     (.clk(clk),
	      .reset(reset),
	      .d(events_s),
	      .q(events_q));
	   
	   genvar 		       ctr;
	   
	   for(ctr = 0; ctr < 3; ctr = ctr + 1)
	     begin:ctrs
		
		wire [0:perf_ctr_width-1] ctr_s, ctr_q;
		assign ctr_s = ctr_q + events_q[ctr];
		c_dff
		  #(.width(perf_ctr_width),
		    .reset_type(reset_type))
		ctrq
		  (.clk(clk),
		   .reset(reset),
		   .d(ctr_s),
		   .q(ctr_q));
		
	     end
	   
	end
      
   endgenerate
   
endmodule
