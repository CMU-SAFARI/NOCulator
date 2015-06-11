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

`default_nettype none

module testbench
  ();
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
`include "parameters.v"
   
   parameter Tclk = 2;
   parameter initial_seed = 0;
   
   // maximum number of packets to generate (-1 = no limit)
   parameter max_packet_count = -1;
   
   // packet injection rate (percentage of cycles)
   parameter packet_rate = 25;
   
   // flit consumption rate (percentage of cycles)
   parameter consume_rate = 50;
   
   // width of packet count register
   parameter packet_count_width = 32;
   
   // channel latency in cycles
   parameter channel_latency = 1;
   
   // only inject traffic at the node ports
   parameter inject_node_ports_only = 0;
   
   // warmup time in cycles
   parameter warmup_time = 100;
   
   // measurement interval in cycles
   parameter measure_time = 10000;
   
   // select packet length mode (0: uniform random, 1: bimodal)
   parameter packet_length_mode = 0;
   
   
   // width required to select individual buffer slot
   localparam flit_buffer_idx_width = clogb(num_flit_buffers);
   
   // total number of packet classes
   localparam num_packet_classes = num_message_classes * num_resource_classes;
   
   // number of VCs
   localparam num_vcs = num_packet_classes * num_vcs_per_class;
   
   // width required to select individual VC
   localparam vc_idx_width = clogb(num_vcs);
   
   // total number of routers
   localparam num_routers
     = (num_nodes + num_nodes_per_router - 1) / num_nodes_per_router;
   
   // number of routers in each dimension
   localparam num_routers_per_dim = croot(num_routers, num_dimensions);
   
   // width required to select individual router in a dimension
   localparam dim_addr_width = clogb(num_routers_per_dim);
   
   // width required to select individual router in entire network
   localparam router_addr_width = num_dimensions * dim_addr_width;
   
   // connectivity within each dimension
   localparam connectivity
     = (topology == `TOPOLOGY_MESH) ?
       `CONNECTIVITY_LINE :
       (topology == `TOPOLOGY_TORUS) ?
       `CONNECTIVITY_RING :
       (topology == `TOPOLOGY_FBFLY) ?
       `CONNECTIVITY_FULL :
       -1;
   
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
   
   // width of flit control signals
   localparam flit_ctrl_width
     = (packet_format == `PACKET_FORMAT_HEAD_TAIL) ? 
       (1 + vc_idx_width + 1 + 1) : 
       (packet_format == `PACKET_FORMAT_EXPLICIT_LENGTH) ? 
       (1 + vc_idx_width + 1) : 
       -1;
   
   // width of flow control signals
   localparam flow_ctrl_width = 1 + vc_idx_width;
   
   
   reg 		    reset;
   reg 		    clk;
   
   wire [0:num_ports*flit_ctrl_width-1] flit_ctrl_in_ip;
   wire [0:num_ports*flit_data_width-1] flit_data_in_ip;
   wire [0:num_ports*flow_ctrl_width-1] flow_ctrl_out_ip;
   wire [0:num_ports-1] 		flit_valid_in_ip;
   wire [0:num_ports-1] 		cred_valid_out_ip;
   
   wire [0:num_ports*flit_ctrl_width-1] flit_ctrl_out_op;
   wire [0:num_ports*flit_data_width-1] flit_data_out_op;
   wire [0:num_ports*flow_ctrl_width-1] flow_ctrl_in_op;
   wire [0:num_ports-1] 		flit_valid_out_op;
   wire [0:num_ports-1] 		cred_valid_in_op;
   
   wire [0:num_ports-1] 		ps_error_ip;
   
   wire 				rtr_error;
   
   reg [0:router_addr_width-1] 		router_address;
   
   reg 					run;
   
   generate
      
      genvar 				ip;
      
      for(ip = 0; ip < num_ports; ip = ip + 1)
	begin:ips
	   
	   if(inject_node_ports_only && (ip < (num_ports-num_nodes_per_router)))
	     begin
		
		assign flit_ctrl_in_ip[ip*flit_ctrl_width:
				       (ip+1)*flit_ctrl_width-1]
			 = {flit_ctrl_width{1'b0}};
		assign flit_valid_in_ip[ip] = 1'b0;
		assign flit_data_in_ip[ip*flit_data_width:
				       (ip+1)*flit_data_width-1]
			 = {flit_data_width{1'b0}};
		
		wire [0:flow_ctrl_width-1] flow_ctrl_out;
		assign flow_ctrl_out
		  = flow_ctrl_out_ip[ip*flow_ctrl_width:
				     (ip+1)*flow_ctrl_width-1];
		
		assign cred_valid_out_ip[ip] = flow_ctrl_out[0];
		
		assign ps_error_ip[ip] = 1'b0;
		
	     end
	   else
	     begin
		
		wire [0:flow_ctrl_width-1] flow_ctrl;
		assign flow_ctrl
		  = flow_ctrl_out_ip[ip*flow_ctrl_width:
				     (ip+1)*flow_ctrl_width-1];
		
		assign cred_valid_out_ip[ip] = flow_ctrl[0];
		
		wire [0:flit_ctrl_width-1] flit_ctrl;
		wire [0:flit_data_width-1] flit_data;
		
		wire [0:packet_count_width-1] ps_out_flits;
		
		wire 			      ps_error;
		packet_source
		  #(.initial_seed(initial_seed+ip),
		    .max_packet_count(max_packet_count),
		    .packet_rate(packet_rate),
		    .packet_count_width(packet_count_width),
		    .packet_length_mode(packet_length_mode),
		    .topology(topology),
		    .num_flit_buffers(num_flit_buffers),
		    .num_header_buffers(num_header_buffers),
		    .num_message_classes(num_message_classes),
		    .num_resource_classes(num_resource_classes),
		    .num_vcs_per_class(num_vcs_per_class),
		    .num_nodes(num_nodes),
		    .num_dimensions(num_dimensions),
		    .num_nodes_per_router(num_nodes_per_router),
		    .packet_format(packet_format),
		    .max_payload_length(max_payload_length),
		    .min_payload_length(min_payload_length),
		    .flit_data_width(flit_data_width),
		    .routing_type(routing_type),
		    .dim_order(dim_order),
		    .port_id(ip),
		    .reset_type(reset_type))
		ps
		  (.clk(clk),
		   .reset(reset),
		   .router_address(router_address),
		   .flit_ctrl(flit_ctrl),
		   .flit_data(flit_data),
		   .flow_ctrl(flow_ctrl),
		   .run(run),
		   .out_flits(ps_out_flits),
		   .error(ps_error));
		
		assign flit_ctrl_in_ip[ip*flit_ctrl_width:
				       (ip+1)*flit_ctrl_width-1]
			 = flit_ctrl;
		assign flit_valid_in_ip[ip] = flit_ctrl[0];
		assign flit_data_in_ip[ip*flit_data_width:
				       (ip+1)*flit_data_width-1]
			 =  flit_data;
		assign ps_error_ip[ip] = ps_error;
		
	     end
	end
      
   endgenerate
   
   router_wrap
     #(.topology(topology),
       .num_flit_buffers(num_flit_buffers),
       .num_header_buffers(num_header_buffers),
       .num_message_classes(num_message_classes),
       .num_resource_classes(num_resource_classes),
       .num_vcs_per_class(num_vcs_per_class),
       .num_nodes(num_nodes),
       .num_dimensions(num_dimensions),
       .num_nodes_per_router(num_nodes_per_router),
       .packet_format(packet_format),
       .max_payload_length(max_payload_length),
       .min_payload_length(min_payload_length),
       .perf_ctr_enable(perf_ctr_enable),
       .perf_ctr_width(perf_ctr_width),
       .error_capture_mode(error_capture_mode),
       .track_vcs_at_output(track_vcs_at_output),
       .router_type(router_type),
       .flit_data_width(flit_data_width),
       .dim_order(dim_order),
       .int_flow_ctrl_type(int_flow_ctrl_type),
       .header_fifo_type(header_fifo_type),
       .input_stage_can_hold(input_stage_can_hold),
       .fbf_regfile_type(fbf_regfile_type),
       .vc_alloc_type(vc_alloc_type),
       .vc_alloc_arbiter_type(vc_alloc_arbiter_type),
       .sw_alloc_type(sw_alloc_type),
       .sw_alloc_arbiter_type(sw_alloc_arbiter_type),
       .sw_alloc_spec_type(sw_alloc_spec_type),
       .ctrl_crossbar_type(ctrl_crossbar_type),
       .data_crossbar_type(data_crossbar_type),
       .reset_type(reset_type))
   rtr
     (.clk(clk),
      .reset(reset),
      .router_address(router_address),
      .flit_ctrl_in_ip(flit_ctrl_in_ip),
      .flit_data_in_ip(flit_data_in_ip),
      .flow_ctrl_out_ip(flow_ctrl_out_ip),
      .flit_ctrl_out_op(flit_ctrl_out_op),
      .flit_data_out_op(flit_data_out_op),
      .flow_ctrl_in_op(flow_ctrl_in_op),
      .error(rtr_error));
   
   wire 				      rchk_error;
   
   router_checker
     #(.num_flit_buffers(num_flit_buffers),
       .num_message_classes(num_message_classes),
       .num_resource_classes(num_resource_classes),
       .num_vcs_per_class(num_vcs_per_class),
       .num_routers_per_dim(num_routers_per_dim),
       .num_dimensions(num_dimensions),
       .num_nodes_per_router(num_nodes_per_router),
       .connectivity(connectivity),
       .packet_format(packet_format),
       .max_payload_length(max_payload_length),
       .min_payload_length(min_payload_length),
       .flit_data_width(flit_data_width),
       .error_capture_mode(error_capture_mode),
       .routing_type(routing_type),
       .dim_order(dim_order),
       .reset_type(reset_type))
   rchk
     (.clk(clk),
      .reset(reset),
      .router_address(router_address),
      .flit_ctrl_in_ip(flit_ctrl_in_ip),
      .flit_data_in_ip(flit_data_in_ip),
      .flit_ctrl_out_op(flit_ctrl_out_op),
      .flit_data_out_op(flit_data_out_op),
      .error(rchk_error));
   
   
   wire [0:num_ports-1] 		      buffer_error_op;
   
   generate
      
      genvar 				      op;
      
      for(op = 0; op < num_ports; op = op + 1)
	begin:ops
	   
	   wire [0:flit_ctrl_width-1] flit_ctrl_out;
	   assign flit_ctrl_out = flit_ctrl_out_op[op*flit_ctrl_width:
						   (op+1)*flit_ctrl_width-1];
	   
	   assign flit_valid_out_op[op] = flit_ctrl_out[0];
	   
	   wire [0:flit_ctrl_width-1] flit_ctrl_dly;
	   c_shift_reg
	     #(.width(flit_ctrl_width),
	       .depth(channel_latency),
	       .reset_type(reset_type))
	   flit_ctrl_dly_sr
	     (.clk(clk),
	      .reset(reset),
	      .enable(1'b1),
	      .data_in(flit_ctrl_out),
	      .data_out(flit_ctrl_dly));
	   
	   wire 		      flit_valid_dly;
	   assign flit_valid_dly = flit_ctrl_dly[0];
	   
	   integer 		      seed = initial_seed + num_ports + op;
	   
	   reg 			      consume;
	   initial
	   begin
	      consume <= $dist_uniform(seed, 0, 99) < consume_rate;
	   end
	   
	   always @(posedge clk)
	     begin
		consume <= $dist_uniform(seed, 0, 99) < consume_rate;
	     end
	   
	   wire [0:flow_ctrl_width-1] flow_ctrl_s;
	   
	   wire [0:num_vcs-1] 	      buffer_empty_vc;
	   
	   wire [0:num_vcs-1] 	      buffer_push_vc;
	   wire [0:num_vcs-1] 	      buffer_pop_vc;
	   
	   if(num_vcs > 1)
	     begin
		
		wire [0:vc_idx_width-1] flit_vc_dly;
		assign flit_vc_dly = flit_ctrl_dly[1:1+vc_idx_width-1];
		
		wire [0:num_vcs-1] 	sel_vc;
		c_decoder
		  #(.num_ports(num_vcs))
		sel_vc_in_dec
		  (.data_in(flit_vc_dly),
		   .data_out(sel_vc));
		
		assign buffer_push_vc = {num_vcs{flit_valid_dly}} & sel_vc;
		
		wire [0:num_vcs-1] 	req_vc;
		assign req_vc = ~buffer_empty_vc;
		
		wire 			update;
		assign update = ~&buffer_empty_vc & consume;
		
		wire [0:num_vcs-1] 	gnt_vc;
		c_rr_arbiter
		  #(.num_ports(num_vcs),
		    .reset_type(reset_type))
		buffer_sel_vc_arb
		  (.clk(clk),
		   .reset(reset),
		   .update(update),
		   .req(req_vc),
		   .gnt(gnt_vc));
		
		assign buffer_pop_vc = {num_vcs{consume}} & gnt_vc;
		
		wire [0:vc_idx_width-1] flit_vc_out;
		c_encoder
		  #(.num_ports(num_vcs))
		flit_vc_out_enc
		  (.data_in(gnt_vc),
		   .data_out(flit_vc_out));
		
		assign flow_ctrl_s[0] = update;
		assign flow_ctrl_s[1:1+vc_idx_width-1] = flit_vc_out;
		
	     end
	   else
	     begin
		assign buffer_push_vc = flit_valid_dly;
		assign buffer_pop_vc = ~buffer_empty_vc & consume;
		assign flow_ctrl_s = buffer_pop_vc;
	     end
	   
	   wire [0:num_vcs-1]       buffer_error_vc;
	   
	   genvar 		    vc;
	   
	   for(vc = 0; vc < num_vcs; vc = vc + 1)
	     begin:vcs
		
		wire buffer_push;
		assign buffer_push = buffer_push_vc[vc];
		
		wire buffer_pop;
		assign buffer_pop = buffer_pop_vc[vc];
		
		wire [0:flit_buffer_idx_width-1] buffer_write_addr;
		wire [0:flit_buffer_idx_width-1] buffer_read_addr;		
		wire buffer_almost_empty;
		wire buffer_empty;
		wire buffer_almost_full;
		wire buffer_full;
		wire [0:1] buffer_errors;
		c_fifo_ctrl
		  #(.addr_width(flit_buffer_idx_width),
		    .depth(num_flit_buffers),
		    .reset_type(reset_type))
		buffer
		  (.clk(clk),
		   .reset(reset),
		   .push(buffer_push),
		   .pop(buffer_pop),
		   .write_addr(buffer_write_addr),
		   .read_addr(buffer_read_addr),
		   .almost_empty(buffer_almost_empty),
		   .empty(buffer_empty),
		   .almost_full(buffer_almost_full),
		   .full(buffer_full),
		   .errors(buffer_errors));
		
		assign buffer_empty_vc[vc] = buffer_empty;
		assign buffer_error_vc[vc] = |buffer_errors;
		
	     end
	   
	   assign buffer_error_op[op] = |buffer_error_vc;
	   
	   wire [0:flow_ctrl_width-1] flow_ctrl_q;
	   c_dff
	     #(.width(flow_ctrl_width),
	       .reset_type(reset_type))
	   flow_ctrlq
	     (.clk(clk),
	      .reset(reset),
	      .d(flow_ctrl_s),
	      .q(flow_ctrl_q));
	   
	   wire [0:flow_ctrl_width-1] flow_ctrl_in;
	   c_shift_reg
	     #(.width(flow_ctrl_width),
	       .depth(channel_latency),
	       .reset_type(reset_type))
	   flow_ctrl_in_sr
	     (.clk(clk),
	      .reset(reset),
	      .enable(1'b1),
	      .data_in(flow_ctrl_q),
	      .data_out(flow_ctrl_in));
	   
	   assign flow_ctrl_in_op[op*flow_ctrl_width:(op+1)*flow_ctrl_width-1]
		    = flow_ctrl_in;
	   assign cred_valid_in_op[op] = flow_ctrl_in[0];
	   
	end
      
   endgenerate
   
   wire [0:2] tb_errors;
   assign tb_errors = {|ps_error_ip, |buffer_error_op, rchk_error};
   
   wire       tb_error;
   assign tb_error = |tb_errors;
   
   wire [0:31] in_flits_s, in_flits_q;
   assign in_flits_s = in_flits_q + pop_count(flit_valid_in_ip);
   c_dff
     #(.width(32),
       .reset_type(reset_type))
   in_flitsq
     (.clk(clk),
      .reset(reset),
      .d(in_flits_s),
      .q(in_flits_q));
   
   wire [0:31] in_flits;
   assign in_flits = in_flits_s;
   
   wire [0:31] in_creds_s, in_creds_q;
   assign in_creds_s = in_creds_q + pop_count(cred_valid_out_ip);
   c_dff
     #(.width(32),
       .reset_type(reset_type))
   in_credsq
     (.clk(clk),
      .reset(reset),
      .d(in_creds_s),
      .q(in_creds_q));
   
   wire [0:31] in_creds;
   assign in_creds = in_creds_q;
   
   wire [0:31] out_flits_s, out_flits_q;
   assign out_flits_s = out_flits_q + pop_count(flit_valid_out_op);
   c_dff
     #(.width(32),
       .reset_type(reset_type))
   out_flitsq
     (.clk(clk),
      .reset(reset),
      .d(out_flits_s),
      .q(out_flits_q));
   
   wire [0:31] out_flits;
   assign out_flits = out_flits_s;
   
   wire [0:31] out_creds_s, out_creds_q;
   assign out_creds_s = out_creds_q + pop_count(cred_valid_in_op);
   c_dff
     #(.width(32),
       .reset_type(reset_type))
   out_credsq
     (.clk(clk),
      .reset(reset),
      .d(out_creds_s),
      .q(out_creds_q));
   
   wire [0:31] out_creds;
   assign out_creds = out_creds_q;
   
   reg 	       count_en;
   
   wire [0:31] count_in_flits_s, count_in_flits_q;
   assign count_in_flits_s
     = count_en ?
       count_in_flits_q + pop_count(flit_valid_in_ip) :
       count_in_flits_q;
   c_dff
     #(.width(32),
       .reset_type(reset_type))
   count_in_flitsq
     (.clk(clk),
      .reset(reset),
      .d(count_in_flits_s),
      .q(count_in_flits_q));
   
   wire [0:31] count_in_flits;
   assign count_in_flits = count_in_flits_s;
   
   wire [0:31] count_out_flits_s, count_out_flits_q;
   assign count_out_flits_s
     = count_en ?
       count_out_flits_q + pop_count(flit_valid_out_op) :
       count_out_flits_q;
   c_dff
     #(.width(32),
       .reset_type(reset_type))
   count_out_flitsq
     (.clk(clk),
      .reset(reset),
      .d(count_out_flits_s),
      .q(count_out_flits_q));
   
   wire [0:31] count_out_flits;
   assign count_out_flits = count_out_flits_s;
   
   reg 	       clk_en;
   
   always
   begin
      clk <= clk_en;
      #(Tclk/2);
      clk <= 1'b0;
      #(Tclk/2);
   end
   
   always @(posedge clk)
     begin
	if(rtr_error)
	  begin
	     $display("internal error detected, cyc=%d", $time);
	     $stop;
	  end
	if(tb_error)
	  begin
	     $display("external error detected, cyc=%d", $time);
	     $stop;
	  end
     end
   
   integer cycles;
   integer d;
   
   initial
   begin
      
      for(d = 0; d < num_dimensions; d = d + 1)
	begin
	   router_address[d*dim_addr_width +: dim_addr_width]
	     = num_routers_per_dim / 2;
	end
      
      reset = 1'b0;
      clk_en = 1'b0;
      run = 1'b0;
      count_en = 1'b0;
      cycles = 0;
      
      #(Tclk);
      
      #(Tclk/2);
      
      reset = 1'b1;
      
      #(Tclk);
      
      reset = 1'b0;
      
      #(Tclk);
      
      clk_en = 1'b1;
      
      #(Tclk/2);
      
      $display("warming up...");
      
      run = 1'b1;

      while(cycles < warmup_time)
	begin
	   cycles = cycles + 1;
	   #(Tclk);
	end
      
      $display("measuring...");
      
      count_en = 1'b1;
      
      while(cycles < warmup_time + measure_time)
	begin
	   cycles = cycles + 1;
	   #(Tclk);
	end
      
      count_en = 1'b0;
      
      $display("measured %d cycles", measure_time);
      
      $display("%d flits in, %d flits out", count_in_flits, count_out_flits);
      
      $display("cooling down...");
      
      run = 1'b0;
      
      while((in_flits > out_flits) || (in_flits > in_creds))
	begin
	   cycles = cycles + 1;
	   #(Tclk);
	end
      
      #(Tclk*10);
      
      $display("simulation ended after %d cycles", cycles);
      
      $display("%d flits received, %d flits sent", in_flits, out_flits);
      
      $finish;
      
   end
   
endmodule
