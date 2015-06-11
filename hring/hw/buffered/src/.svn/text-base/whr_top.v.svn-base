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



// top-level module for wormhole router
module whr_top
  (clk, reset, router_address, flit_ctrl_in_ip, flit_data_in_ip, 
   flow_ctrl_out_ip, flit_ctrl_out_op, flit_data_out_op, flow_ctrl_in_op, 
   error);
   
`include "c_functions.v"
`include "c_constants.v"
`include "whr_constants.v"
   
   // flit buffer entries per VC
   parameter num_flit_buffers = 8;
   
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
   
   // number of nodes per router (a.k.a. concentration factor)
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
   
   // select packet format
   parameter packet_format = `PACKET_FORMAT_EXPLICIT_LENGTH;
   
   // maximum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter max_payload_length = 4;
   
   // minimum payload length (in flits)
   // (note: only used if packet_format==`PACKET_FORMAT_EXPLICIT_LENGTH)
   parameter min_payload_length = 1;
   
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
   
   // enable performance counter
   parameter perf_ctr_enable = 1;
   
   // width of each counter
   parameter perf_ctr_width = 32;
   
   // configure error checking logic
   parameter error_capture_mode = `ERROR_CAPTURE_MODE_NO_HOLD;
   
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
   
   // use input register as part of the flit buffer
   parameter input_stage_can_hold = 0;
   
   // number of entries in flit buffer
   localparam fbf_depth
     = input_stage_can_hold ? (num_flit_buffers - 1) : num_flit_buffers;
   
   // required address size for flit buffer
   localparam fbf_addr_width = clogb(fbf_depth);
   
   // select implementation variant for flit buffer register file
   parameter fbf_regfile_type = `REGFILE_TYPE_FF_2D;
   
   // select which arbiter type to use for switch allocator
   parameter arbiter_type = `ARBITER_TYPE_ROUND_ROBIN;
   
   // select implementation variant for control crossbar
   parameter crossbar_type = `CROSSBAR_TYPE_MUX;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // current router's address
   input [0:router_addr_width-1] router_address;
   
   // control signals for each incoming flit
   input [0:num_ports*flit_ctrl_width-1] flit_ctrl_in_ip;
   
   // flit data for each incoming flit
   input [0:num_ports*flit_data_width-1] flit_data_in_ip;
   
   // outgoing flow control signals
   output [0:num_ports*flow_ctrl_width-1] flow_ctrl_out_ip;
   wire [0:num_ports*flow_ctrl_width-1] flow_ctrl_out_ip;
   
   // control signals for each outgoing flit
   output [0:num_ports*flit_ctrl_width-1] flit_ctrl_out_op;
   wire [0:num_ports*flit_ctrl_width-1] flit_ctrl_out_op;
   
   // flit data for each outgoing flit
   output [0:num_ports*flit_data_width-1] flit_data_out_op;
   wire [0:num_ports*flit_data_width-1] flit_data_out_op;
   
   // incoming flow control signals
   input [0:num_ports*flow_ctrl_width-1] flow_ctrl_in_op;
   
   // internal error condition detected
   output error;
   wire 				error;
   
   
   //---------------------------------------------------------------------------
   // input ports
   //---------------------------------------------------------------------------
   
   wire [0:num_ports*num_ports-1] 	req_ip_op;
   wire [0:num_ports-1] 		req_head_ip;
   wire [0:num_ports-1] 		req_tail_ip;
   wire [0:num_ports*num_ports-1] 	gnt_ip_op;
   
   wire [0:num_ports*flit_data_width-1] xbr_data_in_ip;
   
   wire [0:num_ports-1] 		ipc_error_ip;
   
   generate
      
      genvar 				    ip;
      
      for (ip = 0; ip < num_ports; ip = ip + 1)
	begin:ips
	   
	   
	   //-------------------------------------------------------------------
	   // input controller
	   //-------------------------------------------------------------------
	   
	   wire [0:flit_ctrl_width-1] flit_ctrl_in;
	   assign flit_ctrl_in
	     = flit_ctrl_in_ip[ip*flit_ctrl_width:(ip+1)*flit_ctrl_width-1];
	   
	   wire [0:flit_data_width-1] flit_data_in;
	   assign flit_data_in
	     = flit_data_in_ip[ip*flit_data_width:(ip+1)*flit_data_width-1];
	   
	   wire [0:num_ports-1]       gnt_op;
	   assign gnt_op = gnt_ip_op[ip*num_ports:(ip+1)*num_ports-1];
	   
	   wire [0:num_ports-1]       req_op;
	   wire                       req_head;
	   wire                       req_tail;
	   wire [0:flit_data_width-1] 	  flit_data_out;
	   wire [0:flow_ctrl_width-1] 	  flow_ctrl_out;
	   wire [0:fbf_addr_width-1] 	  fbf_write_addr;
	   wire 			  fbf_write_enable;
	   wire [0:flit_data_width-1] 	  fbf_write_data;
	   wire [0:fbf_addr_width-1] 	  fbf_read_addr;
	   wire 			  fbf_read_enable;
	   wire [0:flit_data_width-1] 	  fbf_read_data;
	   wire 			  ipc_error;
	   whr_ip_ctrl_mac
	     #(.num_flit_buffers(num_flit_buffers),
	       .num_header_buffers(num_header_buffers),
	       .num_routers_per_dim(num_routers_per_dim),
	       .num_dimensions(num_dimensions),
	       .num_nodes_per_router(num_nodes_per_router),
	       .connectivity(connectivity),
	       .packet_format(packet_format),
	       .max_payload_length(max_payload_length),
	       .min_payload_length(min_payload_length),
	       .flit_data_width(flit_data_width),
	       .restrict_turns(restrict_turns),
	       .routing_type(routing_type),
	       .dim_order(dim_order),
	       .header_fifo_type(header_fifo_type),
	       .input_stage_can_hold(input_stage_can_hold),
	       .perf_ctr_enable(perf_ctr_enable),
	       .perf_ctr_width(perf_ctr_width),
	       .error_capture_mode(error_capture_mode),
	       .port_id(ip),
	       .reset_type(reset_type))
	   ipc
	     (.clk(clk),
	      .reset(reset),
	      .router_address(router_address),
	      .flit_ctrl_in(flit_ctrl_in),
	      .flit_data_in(flit_data_in),
	      .req_op(req_op),
	      .req_head(req_head),
	      .req_tail(req_tail),
	      .gnt_op(gnt_op),
	      .flit_data_out(flit_data_out),
	      .flow_ctrl_out(flow_ctrl_out),
	      .fbf_write_addr(fbf_write_addr),
	      .fbf_write_enable(fbf_write_enable),
	      .fbf_write_data(fbf_write_data),
	      .fbf_read_enable(fbf_read_enable),
	      .fbf_read_addr(fbf_read_addr),
	      .fbf_read_data(fbf_read_data),
	      .error(ipc_error));
	   
	   c_pseudo_sram_mac
	     #(.width(flit_data_width),
	       .depth(fbf_depth),
	       .regfile_type(fbf_regfile_type),
	       .reset_type(reset_type))
	   fbf
	     (.clk(clk),
	      .reset(1'b0),
	      .write_enable(fbf_write_enable),
	      .write_address(fbf_write_addr),
	      .write_data(fbf_write_data),
	      .read_enable(fbf_read_enable),
	      .read_address(fbf_read_addr),
	      .read_data(fbf_read_data));
	   
	   assign req_ip_op[ip*num_ports:(ip+1)*num_ports-1] = req_op;
	   assign req_head_ip[ip] = req_head;
	   assign req_tail_ip[ip] = req_tail;
	   assign xbr_data_in_ip[ip*flit_data_width:(ip+1)*flit_data_width-1]
		    = flit_data_out;
	   assign flow_ctrl_out_ip[ip*flow_ctrl_width:
				   (ip+1)*flow_ctrl_width-1]
		    = flow_ctrl_out;
	   assign ipc_error_ip[ip] = ipc_error;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // crossbar
   //---------------------------------------------------------------------------
   
   wire [0:num_ports*num_ports-1]           xbr_ctrl_op_ip;
   
   wire [0:num_ports*flit_data_width-1]     xbr_data_out_op;
   whr_crossbar_mac
     #(.num_in_ports(num_ports),
       .num_out_ports(num_ports),
       .width(flit_data_width),
       .crossbar_type(crossbar_type),
       .reset_type(reset_type))
   xbr
     (.clk(clk),
      .reset(reset),
      .ctrl_op_ip(xbr_ctrl_op_ip),
      .data_in_ip(xbr_data_in_ip),
      .data_out_op(xbr_data_out_op));
   
   
   //---------------------------------------------------------------------------
   // output ports
   //---------------------------------------------------------------------------
   
   wire [0:num_ports*num_ports-1] 	req_op_ip;
   c_interleaver
     #(.width(num_ports*num_ports),
       .num_blocks(num_ports))
   req_op_ip_intl
     (.data_in(req_ip_op),
      .data_out(req_op_ip));
   
   wire [0:num_ports*num_ports-1] 	gnt_op_ip;
   c_interleaver
     #(.width(num_ports*num_ports),
       .num_blocks(num_ports))
   gnt_ip_op_intl
     (.data_in(gnt_op_ip),
      .data_out(gnt_ip_op));
   
   wire [0:num_ports-1] 	  opc_error_op;
   
   generate
      
      genvar 			  op;
      
      for(op = 0; op < num_ports; op = op + 1)
	begin:ops
	   
	   wire [0:flit_data_width-1] 	  flit_data_in;
	   assign flit_data_in
	     = xbr_data_out_op[op*flit_data_width:(op+1)*flit_data_width-1];
	   
	   wire [0:flow_ctrl_width-1] 	  flow_ctrl_in;
	   assign flow_ctrl_in
	     = flow_ctrl_in_op[op*flow_ctrl_width:(op+1)*flow_ctrl_width-1];
	   
	   wire [0:num_ports-1] 	  req_ip;
	   assign req_ip = req_op_ip[op*num_ports:(op+1)*num_ports-1];
	   
	   wire [0:num_ports-1] 	  gnt_ip;
	   wire [0:num_ports-1] 	  xbr_ctrl_ip;
	   wire [0:flit_ctrl_width-1] 	  flit_ctrl_out;
	   wire [0:flit_data_width-1] 	  flit_data_out;
	   wire 			  opc_error;
	   whr_op_ctrl_mac
	     #(.num_flit_buffers(num_flit_buffers),
	       .num_header_buffers(num_header_buffers),
	       .num_routers_per_dim(num_routers_per_dim),
	       .num_dimensions(num_dimensions),
	       .num_nodes_per_router(num_nodes_per_router),
	       .connectivity(connectivity),
	       .packet_format(packet_format),
	       .max_payload_length(max_payload_length),
	       .min_payload_length(min_payload_length),
	       .flit_data_width(flit_data_width),
	       .arbiter_type(arbiter_type),
	       .error_capture_mode(error_capture_mode),
	       .port_id(op),
	       .reset_type(reset_type))
	   opc
	     (.clk(clk),
	      .reset(reset),
	      .flow_ctrl_in(flow_ctrl_in),
	      .req_ip(req_ip),
	      .req_head_ip(req_head_ip),
	      .req_tail_ip(req_tail_ip),
	      .gnt_ip(gnt_ip),
	      .xbr_ctrl_ip(xbr_ctrl_ip),
	      .flit_data_in(flit_data_in),
	      .flit_ctrl_out(flit_ctrl_out),
	      .flit_data_out(flit_data_out),
	      .error(opc_error));
	   
	   assign gnt_op_ip[op*num_ports:(op+1)*num_ports-1] = gnt_ip;
	   
	   assign xbr_ctrl_op_ip[op*num_ports:(op+1)*num_ports-1] = xbr_ctrl_ip;
	   
	   assign flit_ctrl_out_op[op*flit_ctrl_width:(op+1)*flit_ctrl_width-1]
		    = flit_ctrl_out;
	   assign flit_data_out_op[op*flit_data_width:(op+1)*flit_data_width-1]
		    = flit_data_out;
	   
	   assign opc_error_op[op] = opc_error;
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // error reporting
   //---------------------------------------------------------------------------
   
   generate
      
      if(error_capture_mode != `ERROR_CAPTURE_MODE_NONE)
	begin
	   
	   // synopsys translate_off
	   
	   integer 		  i;
	   
	   always @(posedge clk)
	     begin

		for(i = 0; i < num_ports; i = i + 1)
		  begin
		     if(ipc_error_ip[i])
		       $display({"ERROR: Router error detected at input ", 
				 "port %d in module %m."}, i);
		  end
		
		for(i = 0; i < num_ports; i = i + 1)
		  begin
		     if(opc_error_op[i])
		       $display({"ERROR: Router error detected at output ", 
				 "port %d in module %m."}, i);
		  end
		
	     end
	   // synopsys translate_on
	   
	   wire [0:2*num_ports-1] errors_s, errors_q;
	   assign errors_s = {ipc_error_ip, opc_error_op};
	   c_err_rpt
	     #(.num_errors(2*num_ports),
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
