module testbench
   ();
   
`include "c_constants.v"
   
   parameter width = 4;
   
   parameter index = 0;
   
   parameter Tclk = 2;
   
   wire [0:width-1] feedback;
   c_fbgen
     #(.width(width),
       .index(index))
   fbgen
     (.feedback(feedback));
   
   reg 		    clk;
   reg 		    reset;
   reg 		    complete;
   reg 		    load;
   reg 		    run;
   reg [0:width-1]  d;
   
   wire [0:width-1] lfsr_q;
   c_lfsr
     #(.width(width))
   lfsr
     (.clk(clk),
      .reset(reset),
      .load(load),
      .run(run),
      .feedback(feedback),
      .complete(complete),
      .d(d),
      .q(lfsr_q));
   
   wire [0:width-1] ulfsr_q;
   c_lfsr
     #(.width(width),
       .iterations(width))
   ulfsr
     (.clk(clk),
      .reset(reset),
      .load(load),
      .run(run),
      .feedback(feedback),
      .complete(complete),
      .d(d),
      .q(ulfsr_q));
   
   always
   begin
      clk <= 1'b1;
      #(Tclk/2);
      clk <= 1'b0;
      #(Tclk/2);
   end
   
   integer i;
   
   initial
   begin
      reset = 1'b1;
      
      #(Tclk);
      
      #(Tclk/2);
      
      reset = 1'b0;
      
      complete = 1'b0;
      load = 1'b1;
      run = 1'b0;
      d = {width{1'b1}};
      
      #(Tclk);
      
      load = 1'b0;
      run = 1'b1;
      d = {width{1'b0}};
      
      $display("feedback=%x, complete=%b:", feedback, complete);
      
      for(i = 0; i < (1 << width); i = i + 1)
	begin
	   $display("%8d | %b (%x) | %b (%x)", 
		    i, lfsr_q, lfsr_q, ulfsr_q, ulfsr_q);
	   #(Tclk);
	end
      
      complete = 1'b1;
      load = 1'b1;
      run = 1'b0;
      d = {width{1'b1}};
      
      #(Tclk);
      
      load = 1'b0;
      run = 1'b1;
      d = {width{1'b0}};
      
      $display("feedback=%x, complete=%b:", feedback, complete);
      
      for(i = 0; i < ((1 << width) + 1); i = i + 1)
	begin
	   $display("%8d | %b (%x) | %b (%x)", 
		    i, lfsr_q, lfsr_q, ulfsr_q, ulfsr_q);
	   #(Tclk);
	end
      
      $finish;
   end
   
endmodule
