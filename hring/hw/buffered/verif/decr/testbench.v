module testbench
  #(parameter width = 4,
    parameter min_value = 4,
    parameter max_value = 7)
   ();
   
   wire [0:width-1] data_out;
   reg [0:width-1]  data_in;
   
   c_decr
     #(.width(width),
       .min_value(min_value),
       .max_value(max_value))
   dut
     (.data_in(data_in),
      .data_out(data_out));
   
   integer 	    i;
   integer 	    j;
   
   initial
   begin
      
      for(i = min_value; i <= max_value; i = i + 1)
	begin
	   data_in = i;
	   j = ((i == min_value) ? max_value : (i - 1));
	   #(1);
	   $display("checking %d, expecting %d, got %d", i, j, data_out);
	   if(data_out != j)
	     $display("ERROR: %d != %d", data_out, j);
	   #(1);
	end

      $finish;
   end
   
endmodule
