// $Id: c_port_filter.v 1747 2010-01-30 08:17:23Z dub $

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



// module to filter output port requests based on turn restrictions
module c_port_filter
  (route_in_op, inc_rc, route_out_op, error);
   
`include "c_constants.v"
   
   // number of message classes (e.g. request, reply)
   parameter num_message_classes = 2;
   
   // nuber of resource classes (e.g. minimal, adaptive)
   parameter num_resource_classes = 2;
   
   // number of input and output ports on router
   parameter num_ports = 5;
   
   // number of adjacent routers in each dimension
   parameter num_neighbors_per_dim = 2;
   
   // number of nodes per router (a.k.a. consentration factor)
   parameter num_nodes_per_router = 4;
   
   // filter out illegal destination ports
   // (the intent is to allow synthesis to optimize away the logic associated 
   // with such turns)
   parameter restrict_turns = 1;
   
   // connectivity within each dimension
   parameter connectivity = `CONNECTIVITY_LINE;
   
   // select routing function type
   parameter routing_type = `ROUTING_TYPE_DOR;
   
   // select order of dimension traversal
   parameter dim_order = `DIM_ORDER_ASCENDING;
   
   // ID of current port
   parameter port_id = 0;
   
   // current message class
   parameter message_class = 0;
   
   // current resource class
   parameter resource_class = 0;
   
   // unmasked output port
   input [0:num_ports-1] route_in_op;

   // if we have reached our destination for the current resource class, 
   // increment the resource class (unless it's already the highest)
   input inc_rc;
   
   // filtered output port
   output [0:num_ports-1] route_out_op;
   wire [0:num_ports-1] route_out_op;
   
   // internal error condition detected
   output error;
   wire error;   
   
   wire [0:num_ports-1] route_restricted_op;
   wire [0:num_ports-1] error_op;
   
   generate
   
      genvar 					op;
      
      for(op = 0; op < num_ports; op = op + 1)
	begin:ops
	   
	   // packets in the lower resource classes can only go back out through
	   // the same port they came in on if the current router is the
	   // destination router for their current resource class (this cannot 
	   // happen for packets in the highest resource class); for the
	   // flattened butterfly, this applies to all links in the same
	   // dimension; likewise, in the case of dimension-order routing, 
	   // ports for dimensions that were already traversed can only be used 
	   // when changing resource classes
	   if(((op < (num_ports - num_nodes_per_router)) && 
	       ((routing_type == `ROUTING_TYPE_DOR) && 
		((((connectivity == `CONNECTIVITY_LINE) || 
		   (connectivity == `CONNECTIVITY_RING)) && 
		  (op == port_id)) || 
		 ((connectivity == `CONNECTIVITY_FULL) && 
		  ((op / num_neighbors_per_dim) == 
		   (port_id / num_neighbors_per_dim))) || 
		 ((port_id < (num_ports - num_nodes_per_router)) && 
		  (((dim_order == `DIM_ORDER_ASCENDING) && 
		    ((op / num_neighbors_per_dim) < 
		     (port_id / num_neighbors_per_dim))) || 
		   ((dim_order == `DIM_ORDER_DESCENDING) && 
		    ((op / num_neighbors_per_dim) > 
		     (port_id / num_neighbors_per_dim)))))))))
	     begin
		if(resource_class == (num_resource_classes - 1))
		  begin
		     assign route_restricted_op[op] = 1'b0;
		     assign error_op[op] = route_in_op[op];
		  end
		else
		  begin
		     
		     // we could mask this by inc_rc; however, the goal of port 
		     // filtering is to allow synthesis to take advantage of
		     // turn restrictions in optimizing the design, so from this
		     // point of view, masking here would be counterproductive;
		     // also, note that we don't actually recover from illegal 
		     // port requests -- packets just remain in the input buffer
		     // forever; consequently, we save the gate and don't mask 
		     // here
		     assign route_restricted_op[op]
			      = route_in_op[op] /* & inc_rc*/;
		     
		     assign error_op[op] = route_in_op[op] & ~inc_rc;
		     
		  end
		
	     end
	   
	   // only packets in the highest resource class can go to the 
	   // injection/ejection ports; also, a packet coming in on an 
	   // injection/ejection port should never exit the router on the same 
	   // injection/ejection port
	   else if((op >= (num_ports - num_nodes_per_router)) &&
		   ((resource_class < (num_resource_classes - 1)) ||
		    (op == port_id)))
	     begin
		assign route_restricted_op[op] = 1'b0;
		assign error_op[op] = route_in_op[op];
	     end
	   
	   // remaining port requests are valid
	   else
	     begin
		assign route_restricted_op[op] = route_in_op[op];
		assign error_op[op] = 1'b0;
	     end
	   
	end
      
      if(restrict_turns)
	begin
	   assign route_out_op = route_restricted_op;
	   assign error = |error_op;
	end
      else
	begin
	   assign route_out_op = route_in_op;
	   assign error = 1'b0;
	end
      
   endgenerate
   
endmodule
