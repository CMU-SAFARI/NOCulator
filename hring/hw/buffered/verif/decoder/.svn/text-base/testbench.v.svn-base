module testbench
  #(parameter num_ports = 8,
    parameter num_tests = 1000,
    parameter initial_seed = 0)
   ();

`include "c_functions.v"

   localparam width = clogb(num_ports);
   
   reg [0:width-1] data_in;
   wire [0:num_ports-1] data_out;
   
   c_decoder
     #(.num_ports(num_ports))
   dut
     (.data_in(data_in),
      .data_out(data_out));

   integer 	       test;
   integer 	       i;
   integer 	       seed = initial_seed;

   reg [0:num_ports-1] data_ref;
   
   initial
   begin

      for(test = 0; test < num_tests; test = test + 1)
	begin
	   i = $dist_uniform(seed, 0, num_ports-1);
	   data_in = i;
	   data_ref = {num_ports{1'b0}};
	   data_ref[i] = 1'b1;
	   #1;
	   if(data_out != data_ref)
	     $display("ERROR: in=%b, out=%d, expected=%d",
		      data_in, data_out, data_ref);
	   #1;
	end
      
      $finish;
      
   end
   
endmodule
