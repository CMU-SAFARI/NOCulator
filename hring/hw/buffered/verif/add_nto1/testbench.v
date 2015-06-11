module testbench
  #(parameter num_ports = 4,
    parameter width = 8,
    parameter Tclk = 2,
    parameter sim_cycles = 1000,
    parameter initial_seed = 0)
   ();

`include "c_functions.v"

   localparam out_width = clogb(num_ports) + width;

   wire [0:out_width-1] data_out;

   reg [0:num_ports*width-1] data_in;

   reg 			     clk;
   
   integer 		     seed = initial_seed;
   integer 		     i;
   
   always @(posedge clk)
     begin
	for(i = 0; i < num_ports; i = i + 1)
	  begin
	     data_in[i*width +: width]
	       = $dist_uniform(seed, 0, 2**width-1);
	  end
     end

   c_add_nto1
     #(.width(width),
       .num_ports(num_ports))
   dut
     (.data_in(data_in),
      .data_out(data_out));
   
   always
   begin
      clk <= 1'b1;
      #(Tclk/2);
      clk <= 1'b0;
      #(Tclk/2);
   end
   
   initial
   begin
      #(sim_cycles*Tclk);
      $finish;
   end
   
endmodule
