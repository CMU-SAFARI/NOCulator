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

// switch allocator performance measurement testbench
module testbench
  ();

   parameter Tclk = 2;
   parameter initial_seed = 0;
   parameter output_as_csv = 1;
   parameter rate = 50;
   parameter spec_rate = 20;
   parameter warmup_cycles = 10;
   parameter sim_cycles = 10000;
   
`include "c_functions.v"
`include "c_constants.v"
`include "vcr_constants.v"
   
   // select network topology
   parameter topology = `TOPOLOGY_FBFLY;
   
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
   
   // total number of nodes
   parameter num_nodes = 64;
   
   // number of dimensions in network
   parameter num_dimensions = 2;
   
   // number of nodes per router (a.k.a. concentration factor)
   parameter num_nodes_per_router = 4;
   
   // total number of routers
   localparam num_routers
     = (num_nodes + num_nodes_per_router - 1) / num_nodes_per_router;
   
   // number of routers in each dimension
   localparam num_routers_per_dim = croot(num_routers, num_dimensions);
   
   // number of adjacent routers in each dimension
   parameter num_neighbors_per_dim
     = (topology == `TOPOLOGY_MESH) ?
       2 :
       (topology == `TOPOLOGY_FBFLY) ?
       (num_routers_per_dim - 1) :
       -1;
   
   // number of input and output ports on router
   localparam num_ports
     = num_dimensions * num_neighbors_per_dim + num_nodes_per_router;
   
   // width required to select an individual port
   localparam port_idx_width = clogb(num_ports);
   
   // select implementation variant for switch allocator
   parameter allocator_type = `SW_ALLOC_TYPE_SEP_IF;
   
   // select implementation variant for wavefront allocator
   // select which arbiter type to use in allocator
   parameter arbiter_type = `ARBITER_TYPE_ROUND_ROBIN;
   
   // select speculation level
   parameter spec_type = `SW_ALLOC_SPEC_TYPE_REQ;
   
   parameter reset_type = `RESET_TYPE_ASYNC;
   
   reg clk;
   reg reset;
   
   always
   begin
      clk <= 1'b1;
      #(Tclk/2);
      clk <= 1'b0;
      #(Tclk/2);
   end

   // non-speculative switch requests
   wire [0:num_ports*num_vcs-1] req_nonspec_ip_ivc;
   
   // speculative switch requests
   wire [0:num_ports*num_vcs-1] req_spec_ip_ivc;
   
   // grants corresponding to non-speculative requests (to input controller)
   wire [0:num_ports*num_vcs-1] gnt_nonspec_ip_ivc;
   
   // grants corresponding to speculative requests (to input controller)
   wire [0:num_ports*num_vcs-1] gnt_spec_ip_ivc;
   
   wire [0:num_ports*num_ports-1] req_ip_op;
   
   // crossbar control signals
   wire [0:num_ports*num_ports-1] gnt_ip_op;
   
   wire [0:num_ports*num_vcs*port_idx_width-1] route_port_ip_ivc;
   
   function integer zero_or_one_hot(input [0:num_ports-1] vector);
      integer i;
      reg [0:num_ports-1] vec_sub1;
      reg [0:num_ports-1] vec_rev;
      reg [0:num_ports-1] vec_rev_sub1;
      reg [0:num_ports-1] vec_sub1rev;
      begin
	 for(i = 0; i < num_ports; i = i + 1)
	   begin
	      vec_rev[i] = vector[num_ports - i - 1];
	   end
	 vec_sub1 = vector - {{num_ports-1{1'b0}}, 1'b1};
	 vec_rev_sub1 = vec_rev - {{num_ports-1{1'b0}}, 1'b1};
	 for(i = 0; i < num_ports; i = i + 1)
	   begin
	      vec_sub1rev[i] = vec_rev_sub1[num_ports - i - 1];
	   end
	 zero_or_one_hot = &(vector ^ (vec_sub1 | vec_sub1rev));
      end
   endfunction
   
   integer 				  seed = initial_seed;
   
   generate
      
      genvar 				  ip;
      
      for(ip = 0; ip < num_ports; ip = ip + 1)
	begin:ips
	   
	   wire [0:num_vcs-1] req_ivc;
	   wire [0:num_vcs-1] req_nonspec_ivc;
	   wire [0:num_vcs-1] req_spec_ivc;
	   wire [0:num_vcs*port_idx_width-1] route_port_ivc;
	   wire [0:num_vcs*num_ports-1]      route_ivc_op;
	   
	   genvar mc;
	   
	   for(mc = 0; mc < num_message_classes; mc = mc + 1)
	     begin:mcs
		
		genvar irc;
		
		for(irc = 0; irc < num_resource_classes; irc = irc + 1)
		  begin:ircs
		     
		     genvar icvc;
		     
		     for(icvc = 0; icvc < num_vcs_per_class; icvc = icvc + 1)
		       begin:icvcs

			  reg req;
			  reg spec;
			  reg [0:port_idx_width-1] route_port;
			  always@ (posedge clk)
			    begin
			       if(reset)
				 begin
				    req <= 1'b0;
				    spec <= 1'b0;
				    route_port <= {port_idx_width{1'b0}};
				 end
			       else
				 begin
				    req <= ($dist_uniform(seed, 0, 99) < rate);
				    spec <= ($dist_uniform(seed, 0, 99) < spec_rate);
				    route_port <= $dist_uniform(seed, 0, num_ports-1);
				 end
			    end
			  
			  wire [0:num_ports-1] route_op;
			  c_decoder
			    #(.num_ports(num_ports))
			  route_op_dec
			    (.data_in(route_port),
			     .data_out(route_op));

			  assign req_ivc[(mc*num_resource_classes+irc)*
					 num_vcs_per_class+icvc]
				   = req;
			  
			  wire req_nonspec;
			  assign req_nonspec = req & ~spec;
			  
			  assign req_nonspec_ivc[(mc*num_resource_classes+irc)*
						 num_vcs_per_class+icvc]
				   = req_nonspec;
			  
			  wire req_spec;
			  assign req_spec = req & spec;
			  
			  assign req_spec_ivc[(mc*num_resource_classes+irc)*
					      num_vcs_per_class+icvc]
				   = req_spec;
			  
			  assign route_port_ivc[((mc*num_resource_classes+irc)*
						 num_vcs_per_class+icvc)*
						port_idx_width:
						((mc*num_resource_classes+irc)*
						 num_vcs_per_class+icvc+1)*
						port_idx_width-1]
				   = route_port;
			  
			  assign route_ivc_op[((mc*num_resource_classes+irc)*
					       num_vcs_per_class+icvc)*
					      num_ports:
					      ((mc*num_resource_classes+irc)*
					       num_vcs_per_class+icvc+1)*
					      num_ports-1]
				   = route_op;
			  
		       end
		     
		  end
		
	     end
	   
	   wire [0:num_ports-1] 	       req_op;
	   c_select_1ofn
	     #(.num_ports(num_vcs),
	       .width(num_ports))
	   req_op_sel
	     (.select(req_ivc),
	      .data_in(route_ivc_op),
	      .data_out(req_op));
	   
	   assign req_nonspec_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1]
		    = req_nonspec_ivc;
	   assign req_spec_ip_ivc[ip*num_vcs:(ip+1)*num_vcs-1]
		    = req_spec_ivc;
	   assign req_ip_op[ip*num_ports:(ip+1)*num_ports-1]
		    = req_op;
	   assign route_port_ip_ivc[ip*num_vcs*port_idx_width:
				    (ip+1)*num_vcs*port_idx_width-1]
		    = route_port_ivc;
	   
	end
      
   endgenerate
   
   wire [0:num_ports*num_ports-1] 	       gnt_op_ip;
   
   vcr_sw_alloc_mac
     #(.num_vcs(num_vcs),
       .num_ports(num_ports),
       .allocator_type(allocator_type),
       .arbiter_type(arbiter_type),
       .spec_type(spec_type),
       .reset_type(reset_type))
   swa
     (.clk(clk),
      .reset(reset),
      .route_port_ip_ivc(route_port_ip_ivc),
      .req_nonspec_ip_ivc(req_nonspec_ip_ivc),
      .req_spec_ip_ivc(req_spec_ip_ivc),
      .gnt_nonspec_ip_ivc(gnt_nonspec_ip_ivc),
      .gnt_spec_ip_ivc(gnt_spec_ip_ivc),
      .gnt_op_ip(gnt_op_ip));
   
   c_interleaver
     #(.width(num_ports*num_ports),
       .num_blocks(num_ports))
   gnt_intl
     (.data_in(gnt_op_ip),
      .data_out(gnt_ip_op));

   function [0:num_ports*num_ports-1] augment_alloc
     (input [0:num_ports*num_ports-1] requests,
      input [0:num_ports*num_ports-1] grants);
      reg [0:num_ports*num_ports-1] results;
      integer 			    i, j;
/*      integer 			    k;*/
      reg 			    done;
      reg 			    assigned;
      begin
/*	 $display("SIM: >>>augment_alloc");
	 $display("SIM: Requests:");
	 for(i = 0; i < num_ports; i = i + 1)
	   $display("SIM: %b", requests[i*num_ports +: num_ports]);
	 $display("SIM: Grants:");
	 for(i = 0; i < num_ports; i = i + 1)
	   $display("SIM: %b", grants[i*num_ports +: num_ports]);*/
	 done = 1'b0;
	 requests = requests & ~grants;
	 while(!done)
	   begin
/*	      $display("SIM: begin while loop");*/
	      done = 1'b1;
	      for(i = 0; i < num_ports; i = i + 1)
		begin
/*		   $display("SIM: begin for loop (i=%d)", i);*/
		   assigned = 1'b0;
		   for(j = 0; j < num_ports; j = j + 1)
		     begin
			if(grants[j*num_ports+i])
			  begin
			     assigned = 1'b1;
			  end
		     end
		   if(!assigned)
		     begin
/*			$display("SIM: checking port %d", i);*/
			results = find_aug_path(i, {num_ports{1'b0}}, requests,
						grants);
			if(|results)
			  begin
			     requests = requests ^ results;
			     grants = grants ^ results;
			     done = 1'b0;
			     i = num_ports;
/*			     $display("SIM: found path; restarting");
			     $display("SIM: New request matrix:");
			     for(k = 0; k < num_ports; k = k + 1)
			       $display("SIM: %b",
                                        requests[k*num_ports +: num_ports]);
			     $display("SIM: New grant matrix:");
			     for(k = 0; k < num_ports; k = k + 1)
			       $display("SIM: %b",
                                        grants[k*num_ports +: num_ports]);*/
			  end
		     end
/*		   $display("SIM: end for loop");*/
		end
/*	      $display("SIM: end while loop");*/
	   end
	 augment_alloc = grants;
/*	 $display("SIM: Augmented:");
	 for(i = 0; i < num_ports; i = i + 1)
	   $display("SIM: %b", augment_alloc[i*num_ports +: num_ports]);
	 $display("SIM: <<<augment_alloc");*/
      end
   endfunction
   
   function automatic [0:num_ports*num_ports-1] find_aug_path
     (input integer port_select,
      input [0:num_ports-1] ports_ignore,
      input [0:num_ports*num_ports-1] requests,
      input [0:num_ports*num_ports-1] grants);
      reg [0:num_ports*num_ports-1] results;
      reg 			    done;
      integer 			    i, j;
      begin
/*	 $display("SIM: >>>find_aug_path");
	 $display("SIM: port_select=%d, ports_ignore=%b", port_select, ports_ignore);
	 $display("SIM: Requests:");
	 for(i = 0; i < num_ports; i = i + 1)
	   $display("SIM: %b", requests[i*num_ports +: num_ports]);
	 $display("SIM: Grants:");
	 for(i = 0; i < num_ports; i = i + 1)
	   $display("SIM: %b", grants[i*num_ports +: num_ports]);*/
	 find_aug_path = {num_ports*num_ports{1'b0}};
	 done = 1'b0;
	 for(i = 0; (i < num_ports) && !done; i = i + 1)
	   begin

/*	      $display("SIM: checking input port %d", i);*/
	      
	      // check if there is an unsatisfied request from this input port
	      if(requests[i*num_ports+port_select] && !ports_ignore[i])
		begin

		   find_aug_path[i*num_ports+port_select] = 1'b1;

		   if(!(|grants[i*num_ports +: num_ports]))
		     begin

/*			$display("SIM: input port %d has not been granted to anybody so far; terminating", i);*/
			
			// if input port has not yet been granted, we have found
			// an augmenting path, and leave the loop (we know there
			// was a request from this input port to our initial
			// output port, i.e., the lack of grant is not simply
			// due to no request having been made by this input)
			done = 1'b1;
			     
		     end
			
		   // has this input port been granted to someone else?
		   // (can't be the current output, otherwise we wouldn't have
		   // an unsatisfied request)
		   for(j = 0; (j < num_ports) && !done; j = j + 1)
		     begin
			if(grants[i*num_ports+j])
			  begin

/*			     $display("SIM: input port %d was previously granted to output port %d; recursing", i, j);*/
			     
			     // try if removing that grant helps using recursion
			     find_aug_path[i*num_ports+j] = 1'b1;
			     results = find_aug_path(j, ports_ignore | ({1'b1, {num_ports-1{1'b0}}} >> i),
						     requests ^ find_aug_path,
						     grants ^ find_aug_path);
			     if(|results)
			       begin
				  
				  // we found an augmenting path, so look no
				  // further
				  find_aug_path
				    = find_aug_path | results;
				  done = 1'b1;
				  
			       end
			     else
			       begin

				  // this path didn't work out, so remove from
				  // results
				  find_aug_path[i*num_ports+j] = 1'b0;
			     
			       end
			  end
		     end
		   
		   if(!done)
		     begin

			// none of the paths through this input port worked out,
			// so remove from results
			find_aug_path[i*num_ports+port_select] = 1'b0;
			
		     end
		end
	   end
/*	 $display("SIM: Result:");
	 for(i = 0; i < num_ports; i = i + 1)
	   $display("SIM: %b", find_aug_path[i*num_ports +: num_ports]);
	 $display("SIM: <<<find_aug_path");*/
      end
   endfunction
   
   wire [0:num_ports*num_ports-1] 	 max_ip_op;
   assign max_ip_op = augment_alloc(req_ip_op, gnt_ip_op);

   function integer get_flow(input [0:num_ports*num_ports-1] gnt_matrix);
      integer i;
      begin
	 get_flow = 0;
	 for(i = 0; i < num_ports; i = i + 1)
	   get_flow = get_flow + |gnt_matrix[i*num_ports +: num_ports];
      end
   endfunction

   wire [0:31] req_flow;
   assign req_flow = get_flow(req_ip_op);

   wire [0:31] gnt_flow;
   assign gnt_flow = get_flow(gnt_ip_op);
   
   wire [0:31] max_flow;
   assign max_flow = get_flow(max_ip_op);
   
   integer 				 k;
   integer 				 req_count;
   integer 				 gnt_count;
   integer 				 max_count;
   reg 					 warmup;
   reg 					 measure;
   
   initial
   begin

      warmup = 1'b0;
      measure = 1'b0;
      
      #(Tclk/2);
      
      if(!output_as_csv) $display("SIM: starting reset sequence");
      
      reset = 1'b1;
      req_count = 0;
      gnt_count = 0;
      max_count = 0;
      
      #(2*Tclk);
      
      reset = 1'b0;
      
      if(!output_as_csv)
	begin
	   $display("SIM: reset sequence completed");
	   $display("SIM: starting warmup phase");
	end
      
      warmup = 1'b1;
      
      #(warmup_cycles*Tclk);
      
      warmup = 1'b0;
      
      if(!output_as_csv)
	begin
	   $display("SIM: warmup phase completed");
	   $display("SIM: starting simulation");
	end

      measure = 1'b1;
      
      for(k = 0; k < sim_cycles; k = k + 1)
	begin
	   req_count = req_count + req_flow;
	   gnt_count = gnt_count + gnt_flow;
	   max_count = max_count + max_flow;
	   #(Tclk);
	end
      
      if(output_as_csv)
	begin
	   $display("SIM: num_ports,num_vcs,spec_type,rate, spec_rate,req_flow,gnt_flow,max_flow,gnt_prob,max_prob,efficiency");
	   $display("SIM: %0d,%0d,%0d,%0d,%0d,%0d,%0d,%0d,%f,%f,%f", 
		    num_ports, num_vcs, spec_type, rate, spec_rate, 
		    req_count, gnt_count, max_count, 
		    ($itor(gnt_count)/$itor(req_count)), ($itor(max_count)/$itor(req_count)),
		    ($itor(gnt_count)/$itor(max_count)));
	end
      else
	begin
	   $display("SIM: simulation completed");
      
	   $display("SIM: flow analysis:");
	   $display("SIM: num_ports=%0d, num_vcs=%0d, spec_type=%0d, rate=%0d, spec_rate=%0d",
		    num_ports, num_vcs, spec_type, rate, spec_rate);
	   $display("SIM: req_count=%0d, gnt_count=%0d, max_count=%0d",
		    req_count, gnt_count, max_count);
	   $display("SIM: gnt_prob=%f, max_prob=%f",
		    ($itor(gnt_count)/$itor(req_count)), ($itor(max_count)/$itor(req_count)));
	   $display("SIM: efficiency=%f",
		    ($itor(gnt_count)/$itor(max_count)));
	end
      
      $finish;
      
   end
   
endmodule
