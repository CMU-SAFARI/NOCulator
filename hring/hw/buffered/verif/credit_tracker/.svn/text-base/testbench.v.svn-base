module testbench
  ();
   
`include "c_functions.v"
`include "c_constants.v"
   
   parameter num_credits = 8;
   parameter reset_type = `RESET_TYPE_ASYNC;
   parameter Tclk = 2;
   parameter runtime = 1000;
   parameter rate_in = 50;
   parameter rate_out = 50;
   parameter initial_seed = 0;
   
   reg clk;
   reg reset;
   
   wire debit;
   wire credit;
   wire free;
   wire free_early;
   wire error;
   
   c_credit_tracker
     #(.num_credits(num_credits),
       .reset_type(reset_type))
   dut
     (.clk(clk),
      .reset(reset),
      .debit(debit),
      .credit(credit),
      .free(free),
      .free_early(free_early),
      .error(error));
   
   always
   begin
      clk <= 1'b1;
      #(Tclk/2);
      clk <= 1'b0;
      #(Tclk/2);
   end
   
   reg [0:clogb(num_credits+1)-1] cred_count;
   wire [0:clogb(num_credits+1)-1] cred_count_next;
   assign cred_count_next = reset ? num_credits : cred_count + credit - debit;
   
   reg 				  flag_in, flag_out;

   assign credit = !reset && flag_out && (cred_count < num_credits);
   assign debit = !reset && flag_in && (cred_count > 0);
   
   integer seed = initial_seed;

   always @(posedge clk)
     begin
	flag_in = !reset && ($dist_uniform(seed, 0, 99) < rate_in);
	flag_out = !reset && ($dist_uniform(seed, 0, 99) < rate_out);
	cred_count = reset ? num_credits : cred_count_next;
     end
   
   initial
   begin
      reset = 1'b1;

      #(3*Tclk);

      reset = 1'b0;

      #(Tclk);
      
      #(runtime*Tclk);

      $finish;
   end
      
endmodule
