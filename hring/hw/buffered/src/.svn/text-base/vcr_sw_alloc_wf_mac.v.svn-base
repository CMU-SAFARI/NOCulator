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



// wavefront block for switch allocation
module vcr_sw_alloc_wf_mac
  (clk, reset, alloc_in_ip_op, alloc_out_ip_op);
   
`include "c_constants.v"
`include "vcr_constants.v"
   
   // number of input and output ports on switch
   parameter num_ports = 5;
   
   // select which wavefront allocator variant to use
   parameter wf_alloc_type = `WF_ALLOC_TYPE_REP;
   
   // select speculation type
   parameter spec_type = `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS;
   
   // number of bits required for request signals
   localparam req_width = (spec_type == `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : (1 + 1);
   
   // number of bits required for grant signals
   localparam gnt_width = req_width;
   
   // width of incoming allocator control signals
   localparam alloc_in_width = req_width;
   
   // width of outgoing allocator control signals
   localparam alloc_out_width
     = gnt_width + ((spec_type != `SW_ALLOC_SPEC_TYPE_NONE) ? 1 : 0);
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   input clk;
   input reset;
   
   // incoming allocator control signals
   input [0:num_ports*num_ports*alloc_in_width-1] alloc_in_ip_op;
   
   // outgoing allocator control signals
   output [0:num_ports*num_ports*alloc_out_width-1] alloc_out_ip_op;
   wire [0:num_ports*num_ports*alloc_out_width-1] alloc_out_ip_op;
   
   
   //---------------------------------------------------------------------------
   // pack/unpack control signals
   //---------------------------------------------------------------------------
   
   wire [0:num_ports*num_ports-1] 		  req_nonspec_ip_op;
   wire [0:num_ports*num_ports-1] 		  gnt_nonspec_ip_op;
   
   wire [0:num_ports*num_ports-1] 		  req_spec_ip_op;
   wire [0:num_ports*num_ports-1] 		  gnt_spec_ip_op;
   
   generate
      
      genvar 					  ip;
      
      for(ip = 0; ip < num_ports; ip = ip + 1)
	begin:ips
	   
	   genvar op;
	   
	   for(op = 0; op < num_ports; op = op + 1)
	     begin:ops
		
		assign req_nonspec_ip_op[ip*num_ports+op]
			 = alloc_in_ip_op[(ip*num_ports+op)*alloc_in_width];
		
		assign alloc_out_ip_op[(ip*num_ports+op)*alloc_out_width]
			 = gnt_nonspec_ip_op[ip*num_ports+op];
		
		if(spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
		  begin
		     
		     assign req_spec_ip_op[ip*num_ports+op]
			      = alloc_in_ip_op[(ip*num_ports+op)*
					       alloc_in_width+1];
		     
		     assign alloc_out_ip_op[(ip*num_ports+op)*alloc_out_width+1]
			      = gnt_spec_ip_op[ip*num_ports+op];
		     
		     wire gnt;
		     assign gnt = gnt_nonspec_ip_op[ip*num_ports+op] |
				  gnt_spec_ip_op[ip*num_ports+op];
		     
		     assign alloc_out_ip_op[(ip*num_ports+op)*alloc_out_width+
					    gnt_width]
			      = gnt;
		     
		  end
		else
		  assign req_spec_ip_op[ip*num_ports+op] = 1'b0;
		
	     end
	   
	end
      
   endgenerate
   
   
   //---------------------------------------------------------------------------
   // non-speculative allocation
   //---------------------------------------------------------------------------
   
   wire 			  update_alloc_nonspec;
   assign update_alloc_nonspec = |req_nonspec_ip_op;
   
   wire [0:num_ports*num_ports-1] req_wf_nonspec_ip_op;
   assign req_wf_nonspec_ip_op = req_nonspec_ip_op;
   
   wire [0:num_ports*num_ports-1] gnt_wf_nonspec_ip_op;
   c_wf_alloc
     #(.num_ports(num_ports),
       .wf_alloc_type(wf_alloc_type),
       .reset_type(reset_type))
   gnt_wf_nonspec_ip_op_alloc
     (.clk(clk),
      .reset(reset),
      .update(update_alloc_nonspec),
      .req(req_wf_nonspec_ip_op),
      .gnt(gnt_wf_nonspec_ip_op));
   
   assign gnt_nonspec_ip_op = gnt_wf_nonspec_ip_op;
   
   
   //---------------------------------------------------------------------------
   // support logic for speculative allocation
   //---------------------------------------------------------------------------
   
   generate

      if(spec_type != `SW_ALLOC_SPEC_TYPE_NONE)
	begin
	   
	   wire 			  update_alloc_spec;
	   wire [0:num_ports*num_ports-1] req_wf_spec_ip_op;
	   wire [0:num_ports*num_ports-1] gnt_wf_spec_ip_op;
	   
	   case(spec_type)
	     
	     `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS,
	     `SW_ALLOC_SPEC_TYPE_REQS_MASK_REQS:
	       begin
		  
		  wire [0:num_ports-1] noreq_nonspec_op;
		  c_nor_nto1
		    #(.width(num_ports),
		      .num_ports(num_ports))
		  noreq_nonspec_op_nor
		    (.data_in(req_nonspec_ip_op),
		     .data_out(noreq_nonspec_op));
		  
		  wire [0:num_ports*num_ports-1] req_nonspec_op_ip;
		  c_interleaver
		    #(.width(num_ports*num_ports),
		      .num_blocks(num_ports))
		  req_nonspec_op_ip_intl
		    (.data_in(req_nonspec_ip_op),
		     .data_out(req_nonspec_op_ip));
		  
		  wire [0:num_ports-1] 		 noreq_nonspec_ip;
		  c_nor_nto1
		    #(.width(num_ports),
		      .num_ports(num_ports))
		  noreq_nonspec_ip_nor
		    (.data_in(req_nonspec_op_ip),
		     .data_out(noreq_nonspec_ip));
		  
		  wire [0:num_ports*num_ports-1] noreq_nonspec_ip_op;
		  c_mat_mult
		    #(.dim1_width(num_ports),
		      .dim2_width(1),
		      .dim3_width(num_ports))
		  noreq_nonspec_ip_op_mmult
		    (.input_a(noreq_nonspec_ip),
		     .input_b(noreq_nonspec_op),
		     .result(noreq_nonspec_ip_op));
		  
		  assign update_alloc_spec
		    = |(req_spec_ip_op & noreq_nonspec_ip_op);
		  
		  case(spec_type)
		    
 		    `SW_ALLOC_SPEC_TYPE_REQS_MASK_GNTS:
		      begin
			 
			 assign req_wf_spec_ip_op = req_spec_ip_op;
			 
			 assign gnt_spec_ip_op 
			   = noreq_nonspec_ip_op & gnt_wf_spec_ip_op;
			 
		      end
		    
 		    `SW_ALLOC_SPEC_TYPE_REQS_MASK_REQS:
		      begin
			 
			 assign req_wf_spec_ip_op
			   = noreq_nonspec_ip_op & req_spec_ip_op;
			 
			 assign gnt_spec_ip_op = gnt_wf_spec_ip_op;
			 
		      end
		    
		  endcase
		  
	       end
	     
	     `SW_ALLOC_SPEC_TYPE_GNTS_MASK_GNTS,
	     `SW_ALLOC_SPEC_TYPE_GNTS_MASK_REQS:
	       begin
		  
		  wire [0:num_ports-1] nognt_nonspec_op;
		  c_nor_nto1
		    #(.width(num_ports),
		      .num_ports(num_ports))
		  nognt_nonspec_op_nor
		    (.data_in(gnt_nonspec_ip_op),
		     .data_out(nognt_nonspec_op));
		  
		  wire [0:num_ports*num_ports-1] gnt_nonspec_op_ip;
		  c_interleaver
		    #(.width(num_ports*num_ports),
		      .num_blocks(num_ports))
		  gnt_nonspec_op_ip_intl
		    (.data_in(gnt_nonspec_ip_op),
		     .data_out(gnt_nonspec_op_ip));
		  
		  wire [0:num_ports-1] 		 nognt_nonspec_ip;
		  c_nor_nto1
		    #(.width(num_ports),
		      .num_ports(num_ports))
		  nognt_nonspec_ip_nor
		    (.data_in(gnt_nonspec_op_ip),
		     .data_out(nognt_nonspec_ip));
		  
		  wire [0:num_ports*num_ports-1] nognt_nonspec_ip_op;
		  c_mat_mult
		    #(.dim1_width(num_ports),
		      .dim2_width(1),
		      .dim3_width(num_ports))
		  nognt_nonspec_ip_op_mmult
		    (.input_a(nognt_nonspec_ip),
		     .input_b(nognt_nonspec_op),
		     .result(nognt_nonspec_ip_op));
		  
		  assign update_alloc_spec
		    = |(req_spec_ip_op & nognt_nonspec_ip_op);
		  
		  case(spec_type)
		    
		    `SW_ALLOC_SPEC_TYPE_GNTS_MASK_GNTS:
		      begin
			 
			 assign req_wf_spec_ip_op = req_spec_ip_op;
			 
			 assign gnt_spec_ip_op
			   = nognt_nonspec_ip_op & gnt_wf_spec_ip_op;
			 
		      end
		    
		    `SW_ALLOC_SPEC_TYPE_GNTS_MASK_REQS:
		      begin
			 
			 assign req_wf_spec_ip_op
			   = nognt_nonspec_ip_op & req_spec_ip_op;
			 
			 assign gnt_spec_ip_op = gnt_wf_spec_ip_op;
			 
		      end
		    
		  endcase
		  
	       end
	     
	   endcase
	   
	   c_wf_alloc
	     #(.num_ports(num_ports),
	       .wf_alloc_type(wf_alloc_type),
	       .reset_type(reset_type))
	   gnt_wf_spec_ip_op_alloc
	     (.clk(clk),
	      .reset(reset),
	      .update(update_alloc_spec),
	      .req(req_wf_spec_ip_op),
	      .gnt(gnt_wf_spec_ip_op));
	   
	end
      else
	begin
	   
	   // if speculation is disabled, tie control signals to zero
	   assign gnt_spec_ip_op = {(num_ports*num_ports){1'b0}};
	   
	end
      
   endgenerate
   
endmodule
