module testbench
  ();
   
`include "c_functions.v"
`include "c_constants.v"
   
   parameter num_buffers = 8;
   parameter data_width = 16;
   parameter reset_type = `RESET_TYPE_ASYNC;
   parameter Tclk = 2;
   parameter runtime = 1000;
   parameter rate_in = 50;
   parameter rate_out = 50; 
   parameter initial_seed = 0;
   
   localparam addr_width = clogb(num_buffers);
   
   reg clk;
   reg reset;
   
   wire push;
   wire pop;
   wire [0:addr_width-1] write_addr;
   wire [0:addr_width-1] read_addr;
   wire 		 almost_empty;
   wire 		 empty;
   wire 		 almost_full;
   wire 		 full;
   wire [0:1] 		 ffc_errors;
   
   c_fifo_ctrl
     #(.depth(num_buffers),
       .reset_type(reset_type))
   ffc
     (.clk(clk),
      .reset(reset),
      .push(push),
      .pop(pop),
      .write_addr(write_addr),
      .read_addr(read_addr),
      .almost_empty(almost_empty),
      .empty(empty),
      .almost_full(almost_full),
      .full(full),
      .errors(ffc_errors));
   
   wire free;
   wire free_early;
   wire [0:1] ct_errors;
   
   c_credit_tracker
     #(.num_credits(num_buffers),
       .reset_type(reset_type))
   ct
     (.clk(clk),
      .reset(reset),
      .debit(push),
      .credit(pop),
      .free(free),
      .free_early(free_early),
      .errors(ct_errors));

   reg [0:data_width-1] write_data;
   wire [0:data_width-1] read_data;

   c_regfile
     #(.width(data_width),
       .depth(num_buffers))
   rf
     (.clk(clk),
      .write_enable(push),
      .write_address(write_addr),
      .write_data(write_data),
      .read_address(read_addr),
      .read_data(read_data));
   
   always
   begin
      clk <= 1'b1;
      #(Tclk/2);
      clk <= 1'b0;
      #(Tclk/2);
   end

   wire [0:data_width-1] write_data_next;
   
   assign write_data_next = reset ? {data_width{1'b0}} : (write_data + push);
   
   reg drain;
   reg flag_in, flag_out;

   assign push = ~reset & flag_in & free;
   assign pop = ~reset & flag_out & ~empty;
   
   integer seed = initial_seed;

   always @(posedge clk)
     begin
	flag_in <= ~drain && ($dist_uniform(seed, 0, 99) < rate_in);
	flag_out <= ($dist_uniform(seed, 0, 99) < rate_out);
	write_data <= write_data_next;
     end

   always @(negedge clk)
     begin
	if(push)
	  $display($time, " WRITE: %x=%x.", write_addr, write_data);
	if(pop)
	  $display($time, " READ:  %x=%x.", read_addr, read_data);
	if(pop & (^read_data === 1'bx))
	  $display($time, " ERROR: read X value");
     end
   
   initial
   begin
      reset = 1'b1;
      drain = 1'b0;

      #(Tclk);

      reset = 1'b0;

      #(runtime*Tclk);

      drain = 1'b1;

      while(!empty)
	#(Tclk);
      
      $finish;
   end
      
endmodule
