module testbench
   ();
   
`include "c_constants.v"
`include "c_functions.v"
   
   parameter width = 16;
   
   parameter Tclk = 2;
   
   localparam cnt_width = clogb(width+1);
   
   reg [0:width-1] data;
   
   wire 	   dut_one_hot;
   c_one_hot_det
     #(.width(width))
   dut
     (.data(data),
      .one_hot(dut_one_hot));
   
   wire [0:cnt_width-1] count;
   c_add_nto1
     #(.width(1),
       .num_ports(width))
   adder
     (.data_in(data),
      .data_out(count));
   
   wire 		ref_one_hot;
   assign ref_one_hot = (count <= 1);
   
   wire 		error;
   assign error = ref_one_hot ^ dut_one_hot;
   
   reg 			clk;
   
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
      
      for(i = 0; i < (1 << width); i = i + 1)
	begin
	   
	   @(posedge clk);
	   data = i;
	   
	   @(negedge clk);
	   if(error)
	     begin
		$display("error detected for data=%x!", data);
		$stop;
	     end
	   
	end
      
      $finish;
      
   end
   
endmodule
