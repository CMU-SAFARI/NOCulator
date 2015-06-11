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

module router_checker
  (clk, reset, router_address, flit_ctrl_in_ip, flit_data_in_ip, 
   flit_ctrl_out_op, flit_data_out_op, error);
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
   
   // flit buffer entries per VC
   parameter num_flit_buffers = 8;
   
   // number of message classes (e.g. request, reply)
   parameter num_message_classes = 2;
   
   // number of resource classes (e.g. minimal, adaptive)
   parameter num_resource_classes = 2;
   
   // total number of packet classes
   localparam num_packet_classes = num_message_classes * num_resource_classes;
   
   // number of VCs per class
   parameter num_vcs_per_class = 1;
   
   // number of VCs
   localparam num_vcs = num_packet_classes * num_vcs_per_class;
   
   // width required to select individual VC
   localparam vc_idx_width = clogb(num_vcs);
   
   // number of routers in each dimension
   parameter num_routers_per_dim = 4;
   
   // width required to select individual router in a dimension
   localparam dim_addr_width = clogb(num_routers_per_dim);
   
   // number of dimensions in network
   parameter num_dimensions = 2;
   
   // width required to select individual router in entire network
   localparam router_addr_width = num_dimensions * dim_addr_width;
   
   // number of nodes per router (a.k.a. concentration factor)
   parameter num_nodes_per_router = 1;
   
   // width required to select individual node at current router
   localparam node_addr_width = clogb(num_nodes_per_router);
   
   // width of global addresses
   localparam addr_width = router_addr_width + node_addr_width;
   
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
   
   // width of flit payload data
   parameter flit_data_width = 64;
   
   // width of flow control signals
   localparam flow_ctrl_width = 1 + vc_idx_width;
   
   // configure error checking logic
   parameter error_capture_mode = `ERROR_CAPTURE_MODE_NO_HOLD;
   
   // select routing function type
   parameter routing_type = `ROUTING_TYPE_DOR;
   
   // select order of dimension traversal
   parameter dim_order = `DIM_ORDER_ASCENDING;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // current router's address
   input [0:router_addr_width-1] router_address;
   
   // control signals for each incoming flit
   input [0:num_ports*flit_ctrl_width-1] flit_ctrl_in_ip;
   
   // flit data for each incoming flit
   input [0:num_ports*flit_data_width-1] flit_data_in_ip;
   
   // control signals for each outgoing flit
   input [0:num_ports*flit_ctrl_width-1] flit_ctrl_out_op;
   
   // flit data for each outgoing flit
   input [0:num_ports*flit_data_width-1] flit_data_out_op;
   
   // error indicator
   output error;
   wire error;
   
   wire [0:num_ports*num_vcs*num_ports-1] match_ip_ivc_op;
   
   wire [0:num_ports*num_vcs-1] 	  ref_valid_ip_ivc;
   wire [0:num_ports*num_vcs*flit_ctrl_width-1] ref_ctrl_ip_ivc;
   wire [0:num_ports*num_vcs*flit_data_width-1] ref_data_ip_ivc;
   wire [0:num_ports*num_vcs*port_idx_width-1] 	ref_port_ip_ivc;
   wire [0:num_ports*num_vcs*num_vcs-1] 	ref_vcs_ip_ivc;
   
   wire [0:num_ports-1] 			fifo_errors_ip;
   
   genvar 					ip;
   
   generate
      
      for(ip = 0; ip < num_ports; ip = ip + 1)
	begin:ips
	   
	   //-------------------------------------------------------------------
	   // extract signals for this input port
	   //-------------------------------------------------------------------
	   
	   wire [0:flit_ctrl_width-1] flit_ctrl;
	   assign flit_ctrl
	     = flit_ctrl_in_ip[ip*flit_ctrl_width:(ip+1)*flit_ctrl_width-1];
	   
	   wire [0:flit_data_width-1] flit_data;
	   assign flit_data
	     = flit_data_in_ip[ip*flit_data_width:(ip+1)*flit_data_width-1];
	   
	   wire [0:header_info_width-1]  header_info;
	   assign header_info = flit_data[0:header_info_width-1];
	   
	   wire [0:la_route_info_width-1] la_route_info_in;
	   assign la_route_info_in = header_info[0:la_route_info_width-1];
	   
	   wire [0:port_idx_width-1] 	  la_route_port;
	   assign la_route_port = la_route_info_in[0:port_idx_width-1];
	   
	   wire 			  la_inc_rc;
	   if(num_resource_classes > 1)
	     assign la_inc_rc = la_route_info_in[port_idx_width];
	   else
	     assign la_inc_rc = 1'b0;
	   
	   wire [0:route_info_width-1] 	  route_info;
	   assign route_info = header_info[la_route_info_width:
					   la_route_info_width+
					   route_info_width-1];
	   
	   wire 			  flit_valid;
	   assign flit_valid = flit_ctrl[0];
	   
	   wire 			  flit_head;
	   assign flit_head = flit_ctrl[1+vc_idx_width+0];
	   
	   
	   // determine if this is a tail flit
	   
	   wire 			  flit_tail;
	   case(packet_format)
	     
	     `PACKET_FORMAT_HEAD_TAIL:
	       begin
		  
		  assign flit_tail = flit_ctrl[1+vc_idx_width+1];
		  
	       end
	     
	     `PACKET_FORMAT_EXPLICIT_LENGTH:
	       begin
		  
		  if(max_payload_length == 0)
		    assign flit_tail = flit_head;
		  else if(max_payload_length == 1)
		    begin
		       
		       wire has_payload;
		       
		       if(payload_length_width > 0)
			 assign has_payload
			   = header_info[la_route_info_width+
					 route_info_width];
		       else
			 assign has_payload = 1'b1;
		       
		       assign flit_tail = ~flit_head | ~has_payload;
		       
		    end
		  else
		    begin
		       
		       wire [0:flit_ctr_width-1] flit_ctr_q;
		       wire [0:flit_ctr_width-1] flit_ctr_next;
		       
		       if(payload_length_width > 0)
			 begin
			    
			    wire [0:payload_length_width-1] payload_length;
			    assign payload_length
			      = header_info[la_route_info_width+
					    route_info_width:
					    la_route_info_width+
					    route_info_width+
					    payload_length_width-1];
			    
			    assign flit_ctr_next
			      = (min_payload_length - 1) + payload_length;
			    
			    if(max_payload_length == 0)
			      assign flit_tail = flit_head ?
						 ~|payload_length :
						 ~|flit_ctr_q;
			    else
			      assign flit_tail = ~flit_head & ~|flit_ctr_q;
			    
			 end
		       else
			 begin
			    assign flit_ctr_next = max_payload_length - 1;
			    assign flit_tail = ~flit_head & ~|flit_ctr_q;
			 end
		       
		       wire [0:flit_ctr_width-1] flit_ctr_s;
		       assign flit_ctr_s = flit_valid ? 
					   flit_head ? 
					   flit_ctr_next : 
					   (flit_ctr_q - 1'b1) : 
					   flit_ctr_q;
		       c_dff
			 #(.width(flit_ctr_width),
			   .reset_type(reset_type))
		       flit_ctrq
			 (.clk(clk),
			  .reset(reset),
			  .d(flit_ctr_s),
			  .q(flit_ctr_q));
		       
		    end
		  
	       end
	     
	   endcase
	   
	   
	   //-------------------------------------------------------------------
	   // compute destination port, VC mask and lookahead routing 
	   // information depending on packet class
	   //-------------------------------------------------------------------
	   
	   wire [0:num_vcs*port_idx_width-1] 	 route_port_ivc;
	   wire [0:num_vcs*num_vcs-1] 		 route_ivc_ovc;
	   wire [0:num_vcs*la_route_info_width-1] la_route_info_ivc;
	   
	   genvar 				  mc;
	   
	   for(mc = 0; mc < num_message_classes; mc = mc + 1)
	     begin:mcs
		
		genvar irc;
		
		for(irc = 0; irc < num_resource_classes; irc = irc + 1)
		  begin:ircs
		     
		     wire [0:num_resource_classes-1] sel_irc;
		     c_align
		       #(.data_width(1),
			 .dest_width(num_resource_classes),
			 .offset(irc))
		     sel_irc_alg
		       (.data_in(1'b1),
			.dest_in({num_resource_classes{1'b0}}),
			.data_out(sel_irc));
		     
		     
		     // compute destination port
		     
		     wire [0:num_ports-1] 	     route_op;
		     wire 			     inc_rc;
		     vcr_routing_logic
		       #(.num_resource_classes(num_resource_classes),
			 .num_routers_per_dim(num_routers_per_dim),
			 .num_dimensions(num_dimensions),
			 .num_nodes_per_router(num_nodes_per_router),
			 .connectivity(connectivity),
			 .routing_type(routing_type),
			 .dim_order(dim_order),
			 .reset_type(reset_type))
		     rtl
		       (.clk(clk),
			.reset(reset),
			.router_address(router_address),
			.sel_rc(sel_irc),
			.route_info(route_info),
			.route_op(route_op),
			.inc_rc(inc_rc));
		     
		     wire [0:port_idx_width-1] 	     route_port;
		     c_encoder
		       #(.num_ports(num_ports))
		     route_port_enc
		       (.data_in(route_op),
			.data_out(route_port));
		     
		     // determine candidate output VCs
		     
		     wire [0:num_vcs-1] 	 route_ovc;
		     c_align
		       #(.data_width(2*num_vcs_per_class),
			 .dest_width(num_vcs),
			 .offset((mc*num_resource_classes+irc)*
				 num_vcs_per_class))
		     route_ovc_alg
		       (.data_in({{num_vcs_per_class{~inc_rc}},
				  {num_vcs_per_class{inc_rc}}}),
			.dest_in({num_vcs{1'b0}}),
			.data_out(route_ovc));
		     
		     assign route_port_ivc[(mc*num_resource_classes+irc)*
					   num_vcs_per_class*
					   port_idx_width:
					   (mc*num_resource_classes+irc+1)*
					   num_vcs_per_class*
					   port_idx_width-1]
			      = {num_vcs_per_class{route_port}};
		     assign route_ivc_ovc[(mc*num_resource_classes+irc)*
					  num_vcs_per_class*num_vcs:
					  (mc*num_resource_classes+irc+1)*
					  num_vcs_per_class*num_vcs-1]
			      = {num_vcs_per_class{route_ovc}};
		     
		     
		     // generate lookahead routing information
		     
		     wire [0:((irc ==
			       (num_resource_classes - 1)) ?
			      addr_width :
			      (irc ==
			       (num_resource_classes - 2)) ?
			      (router_addr_width + addr_width) :
			      (2*router_addr_width))-1] hop_route_info_la;
		     if(irc == (num_resource_classes - 1))
		       assign hop_route_info_la
			 = route_info[route_info_width-addr_width:
				      route_info_width-1];
		     else if(irc == (num_resource_classes - 2))
		       assign hop_route_info_la
			 = route_info[route_info_width-
				      (router_addr_width+addr_width):
				      route_info_width-1];
		     else
		       assign hop_route_info_la
			 = route_info[irc*router_addr_width:
				      (irc+2)*router_addr_width-1];
		     
		     wire [0:la_route_info_width-1] 	la_route_info;
		     vcr_la_routing_logic
		       #(.num_resource_classes(num_resource_classes),
			 .num_routers_per_dim(num_routers_per_dim),
			 .num_dimensions(num_dimensions),
			 .num_nodes_per_router(num_nodes_per_router),
			 .connectivity(connectivity),
			 .routing_type(routing_type),
			 .dim_order(dim_order),
			 .resource_class(irc),
			 .reset_type(reset_type))
		     lar
		       (.clk(clk),
			.reset(reset),
			.router_address(router_address),
			.route_port(la_route_port),
			.inc_rc(la_inc_rc),
			.hop_route_info(hop_route_info_la),
			.la_route_info(la_route_info));
		     
		     assign la_route_info_ivc[(mc*num_resource_classes+irc)*
					      num_vcs_per_class*
					      la_route_info_width:
					      (mc*num_resource_classes+irc+1)*
					      num_vcs_per_class*
					      la_route_info_width-1]
			      = {num_vcs_per_class{la_route_info}};
		     
		  end
		
	     end
	   
	   
	   // select actual destination port given based on packet's actual VC
	   
	   wire [0:port_idx_width-1] 			route_port;
	   if(num_vcs > 1)
	     begin:route_port_mux
		
		wire [0:vc_idx_width-1] flit_vc;
		assign flit_vc = flit_ctrl[1:1+vc_idx_width-1];
		
		assign route_port
		  = route_port_ivc[flit_vc*port_idx_width +: 
				   port_idx_width];
		
	     end
	   else
	     assign route_port = route_port_ivc;
	   
	   
	   // save destination port for subsequent body and tail flits
	   
	   wire [0:port_idx_width-1] 	dest_port_s, dest_port_q;
	   assign dest_port_s = flit_valid ?
				(flit_head ?
				 route_port :
				 dest_port_q) :
				dest_port_q;
	   c_dff
	     #(.width(port_idx_width),
	       .reset_type(reset_type))
	   dest_portq
	     (.clk(clk),
	      .reset(reset),
	      .d(dest_port_s),
	      .q(dest_port_q));
	   
	   wire [0:port_idx_width-1] 	dest_port;
	   assign dest_port = (flit_valid & flit_head) ?
			      route_port :
			      dest_port_q;
	   
	   
	   // select actual candidate VCs based on packet's actual VC
	   
	   wire [0:num_vcs-1] 		route_ovc;
	   if(num_vcs > 1)
	     begin:route_ovc_mux
		
		wire [0:vc_idx_width-1] flit_vc;
		assign flit_vc = flit_ctrl[1:1+vc_idx_width-1];
		
		assign route_ovc
		  = route_ivc_ovc[flit_vc*num_vcs +: num_vcs];
		
	     end
	   else
	     assign route_ovc = route_ivc_ovc;
	   
	   
	   // save candidate VCs for subsequent body and tail flits
	   
	   wire [0:num_vcs-1] 		dest_ovc_s, dest_ovc_q;
	   assign dest_ovc_s = flit_valid ?
			       (flit_head ?
				route_ovc :
				dest_ovc_q) :
			       dest_ovc_q;
	   c_dff
	     #(.width(num_vcs),
	       .reset_type(reset_type))
	   dest_ovcq
	     (.clk(clk),
	      .reset(reset),
	      .d(dest_ovc_s),
	      .q(dest_ovc_q));
	   
	   wire [0:num_vcs-1] 		dest_ovc;
	   assign dest_ovc = (flit_valid & flit_head) ?
			     route_ovc :
			     dest_ovc_q;
	   
	   
	   // select actual lookahead routing information based on packet's
	   // actual VC
	   
	   wire [0:la_route_info_width-1] la_route_info_out;
	   if(num_vcs > 1)
	     begin:la_route_info_out_mux
		
		wire [0:vc_idx_width-1] flit_vc;
		assign flit_vc = flit_ctrl[1:1+vc_idx_width-1];
		
		assign la_route_info_out
		  = la_route_info_ivc[flit_vc*la_route_info_width +:
				      la_route_info_width];
		
	     end
	   else
	     assign la_route_info_out = la_route_info_ivc;
	   
	   
	   //-------------------------------------------------------------------
	   // store incoming flits with information about expected output and 
	   // candidate VCs in per-VC FIFOs
	   //-------------------------------------------------------------------
	   
	   wire [0:(flit_ctrl_width+flit_data_width+port_idx_width+
		    num_vcs)-1] 	data_in;
	   assign data_in[0:flit_ctrl_width-1] = flit_ctrl;
	   assign data_in[flit_ctrl_width:
			  flit_ctrl_width+la_route_info_width-1]
		    = flit_head ? 
		      la_route_info_out : 
		      flit_data[0:la_route_info_width-1];
	   assign data_in[flit_ctrl_width+la_route_info_width:
			  flit_ctrl_width+flit_data_width-1]
		    = flit_data[la_route_info_width:flit_data_width-1];
	   assign data_in[flit_ctrl_width+flit_data_width:
			  flit_ctrl_width+flit_data_width+port_idx_width-1]
		    = dest_port;
	   assign data_in[flit_ctrl_width+flit_data_width+port_idx_width:
			  flit_ctrl_width+flit_data_width+port_idx_width+
			  num_vcs-1]
		    = dest_ovc;
	   
	   wire [0:num_vcs*2-1] 	fifo_errors_ivc;
	   
	   genvar 			ivc;
	   
	   for(ivc = 0; ivc < num_vcs; ivc = ivc + 1)
	     begin:ivcs
		
		// generate FIFO control signals
		
		wire [0:flit_ctrl_width+flit_data_width+port_idx_width+
		      num_vcs-1] data_out;
		
		wire 		 push;
		
		if(num_vcs > 1)
		  begin
		     
		     wire [0:vc_idx_width-1] flit_vc;
		     assign flit_vc = flit_ctrl[1:1+vc_idx_width-1];
		     
		     assign push = flit_valid && (flit_vc == ivc);
		     
		  end
		else
		  assign push = flit_valid;
		
		wire 			     match;
		
		wire 			     pop;
		assign pop = match;
		
		wire 			     fifo_full;
		wire 			     fifo_empty;
		wire [0:1] 		     fifo_errors;
		
		
		// instantiate the FIFO
		
		c_fifo
		  #(.depth(num_flit_buffers),
		    .width(flit_ctrl_width+flit_data_width+port_idx_width+
			   num_vcs),
		    .reset_type(reset_type))
		ref_fifo
		  (.clk(clk),
		   .reset(reset),
		   .full(fifo_full),
		   .data_in(data_in),
		   .push(push),
		   .empty(fifo_empty),
		   .data_out(data_out),
		   .pop(pop),
		   .errors(fifo_errors));
		
		assign fifo_errors_ivc[ivc*2:(ivc+1)*2-1] = fifo_errors;
		
		
		// generate reference bit patterns
		
		wire [0:flit_ctrl_width-1] ref_ctrl;
		assign ref_ctrl = data_out[0:flit_ctrl_width-1];
		
		wire [0:flit_data_width-1] ref_data;
		assign ref_data
		  = data_out[flit_ctrl_width:
			     flit_ctrl_width+flit_data_width-1];
		
		wire [0:port_idx_width-1]  ref_port;
		assign ref_port
		  = data_out[flit_ctrl_width+flit_data_width:
			     flit_ctrl_width+flit_data_width+
			     port_idx_width-1];
		
		wire [0:num_vcs-1] 	   ref_ovc;
		assign ref_ovc
		  = data_out[flit_ctrl_width+flit_data_width+
			     port_idx_width:
			     flit_ctrl_width+flit_data_width+
			     port_idx_width+num_vcs-1];
		
		
		// extract observed bit patterns
		
		wire [0:flit_ctrl_width-1] check_ctrl;
		assign check_ctrl
		  = flit_ctrl_out_op[ref_port*flit_ctrl_width +:
				     flit_ctrl_width];
		
		wire [0:flit_data_width-1] check_data;
		assign check_data
		  = flit_data_out_op[ref_port*flit_data_width +: 
				     flit_data_width];
		
		// when comparing control bits, ignore the VC ID field as the 
		// packet may change VCs inside the router (proper VC checking 
		// is performed separately below) 
		wire 			   match_ctrl;
		if(flit_ctrl_width > (1 + vc_idx_width))
		  assign match_ctrl = (ref_ctrl[0] == check_ctrl[0]) &&
				      (ref_ctrl[1+vc_idx_width:
						flit_ctrl_width-1] ==
				       check_ctrl[1+vc_idx_width:
						  flit_ctrl_width-1]);
		else
		  assign match_ctrl = (ref_ctrl[0] == check_ctrl[0]);
		
		wire 			     match_data;
		assign match_data = (ref_data == check_data);
		
		// check VC mapping
		wire 			     match_vcs;
		if(num_vcs > 1)
		  begin
		     
		     wire [0:vc_idx_width-1] check_vc;
		     assign check_vc = check_ctrl[1:1+vc_idx_width-1];
		     
		     assign match_vcs = ref_ovc[check_vc];
		     
		  end
		else
		  assign match_vcs = ref_ovc[0];
		
		// check only if output is active and FIFO contains data
		assign match = check_ctrl[0] & ~fifo_empty & 
			       match_ctrl & match_data & match_vcs;
		
		
		// indicate which port matched
		
		wire [0:num_ports-1] 	     port_mask;
		c_decoder
		  #(.num_ports(num_ports))
		port_mask_dec
		  (.data_in(ref_port),
		   .data_out(port_mask));
		
		wire [0:num_ports-1] 	     match_op;
		assign match_op = {num_ports{match}} & port_mask;
		
		assign match_ip_ivc_op[(ip*num_vcs+ivc)*num_ports:
				       (ip*num_vcs+ivc+1)*num_ports-1]
			 = match_op;
		
		// determine head-of-line expected flits for each VC
		assign ref_valid_ip_ivc[ip*num_vcs+ivc] = ~fifo_empty;
		assign ref_ctrl_ip_ivc[(ip*num_vcs+ivc)*flit_ctrl_width:
				       (ip*num_vcs+ivc+1)*flit_ctrl_width-1]
			 = ref_ctrl;
		assign ref_data_ip_ivc[(ip*num_vcs+ivc)*flit_data_width:
				       (ip*num_vcs+ivc+1)*flit_data_width-1]
			 = ref_data;
		assign ref_port_ip_ivc[(ip*num_vcs+ivc)*port_idx_width:
				       (ip*num_vcs+ivc+1)*port_idx_width-1]
			 = ref_port;
		assign ref_vcs_ip_ivc[(ip*num_vcs+ivc)*num_vcs:
				      (ip*num_vcs+ivc+1)*num_vcs-1]
			 = ref_ovc;
		
	     end
	   
	   if(error_capture_mode != `ERROR_CAPTURE_MODE_NONE)
	     begin
		
		// synopsys translate_off
		
		integer i;
		
		always @(posedge clk)
		  begin
		     
		     for(i = 0; i < num_vcs; i = i + 1)
		       begin
			  
			  if(fifo_errors_ivc[i*2])
			    $display({"ERROR: Reference FIFO underflow at ", 
				      "port %d, VC %d."}, ip, i);
			  
			  if(fifo_errors_ivc[i*2+1])
			    $display({"ERROR: Reference FIFO overflow at ", 
				      "port %d, VC %d."}, ip, i);
			  
		       end
		     
		  end
		// synopsys translate_on
		
		wire [0:num_vcs*2-1] errors_s, errors_q;
		assign errors_s = fifo_errors_ivc;
		c_err_rpt
		  #(.num_errors(num_vcs*2),
		    .capture_mode(error_capture_mode),
		    .reset_type(reset_type))
		chk
		  (.clk(clk),
		   .reset(reset),
		   .errors_in(errors_s),
		   .errors_out(errors_q));
		
		assign fifo_errors_ip[ip] = |errors_q;
		
	     end
	   else
	     assign fifo_errors_ip[ip] = 1'b0;
	   
	   
	end
      
   endgenerate
   
   wire [0:num_ports-1] 	   flit_valid_out_op;
   
   wire [0:num_ports-1] 	   x_error_op;
   
   genvar 			   op;
   
   generate
      
      for(op = 0; op < num_ports; op = op + 1)
	begin:ops
	   
	   wire [0:flit_ctrl_width-1] flit_ctrl_out;
	   assign flit_ctrl_out = flit_ctrl_out_op[op*flit_ctrl_width:
						   (op+1)*flit_ctrl_width-1];
	   
	   wire 		      flit_valid_out;
	   assign flit_valid_out = flit_ctrl_out[0];
	   
	   assign flit_valid_out_op[op] = flit_valid_out;
	   
	   wire [0:flit_data_width-1] flit_data_out;
	   assign flit_data_out = flit_data_out_op[op*flit_data_width +: 
						   flit_data_width];
	   
	   assign x_error_op[op]
		    = (flit_valid_out === 1'bx) ||
		      ((flit_valid_out === 1'b1) &&
		       (^{flit_ctrl_out, flit_data_out} === 1'bx));
	   
	end
      
   endgenerate
   
   // summary signal indicating if any input VC matched the flit at each output
   wire [0:num_ports-1] 	      match_op;
   c_or_nto1
     #(.num_ports(num_ports*num_vcs),
       .width(num_ports))
   match_op_or
     (.data_in(match_ip_ivc_op),
      .data_out(match_op));
   
   // unmatched flits indicate something went wrong
   wire [0:num_ports-1] 	      check_error_op;
   assign check_error_op = (flit_valid_out_op & ~match_op);

   // synopsys translate_off
   
   integer 			      port;
   integer 			      x;
   
   always @(posedge clk)
     begin
	for(port = 0; port < num_ports; port = port + 1)
	  begin
	     if(check_error_op[port])
	       begin
		  $display("check error: op=%d, cyc=%d", port, $time);
		  $display("ctrl=%b, data=%x",
			   flit_ctrl_out_op[port*flit_ctrl_width +: 
					    flit_ctrl_width],
			   flit_data_out_op[port*flit_data_width +: 
					    flit_data_width]);
		  $display("expected flits:");
		  for(x = 0; x < num_ports*num_vcs; x = x + 1)
		    begin
		       if(ref_valid_ip_ivc[x])
			 $display("ip=%d,ivc=%d,ctrl=%b,data=%x,op=%d,vcs=%b",
				  x / num_vcs, x % num_vcs,
				  ref_ctrl_ip_ivc[x*flit_ctrl_width +: 
						  flit_ctrl_width],
				  ref_data_ip_ivc[x*flit_data_width +: 
						  flit_data_width],
				  ref_port_ip_ivc[x*port_idx_width +: 
						  port_idx_width],
				  ref_vcs_ip_ivc[x*num_vcs +: num_vcs]);
		    end
	       end
	     if(x_error_op[port])
	       begin
		  $display("X value error: op=%d, cyc=%d", port, $time);
		  $display("ctrl=%b, data=%x",
			   flit_ctrl_out_op[port*flit_ctrl_width +:
					    flit_ctrl_width],
			   flit_data_out_op[port*flit_data_width +: 
					    flit_data_width]);
	       end
	  end
     end
   // synopsys translate_on
   
   generate
      
      if(error_capture_mode != `ERROR_CAPTURE_MODE_NONE)
	begin
	   
	   // synopsys translate_off
	   
	   integer 			  i;
	   
	   always @(posedge clk)
	     begin
		
		for(i = 0; i < num_ports; i = i + 1)
		  begin
		     if(fifo_errors_ip[i])
		       $display({"ERROR: Reference FIFO error detected at ", 
				 "input port %d."}, i);
		  end
		
	     end
	   // synopsys translate_on
	   
	   wire [0:3*num_ports-1] errors_s, errors_q;
	   assign errors_s = {fifo_errors_ip, check_error_op, x_error_op};
	   c_err_rpt
	     #(.num_errors(3*num_ports),
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
   
endmodule
