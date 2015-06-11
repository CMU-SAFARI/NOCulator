`include "defines.v"
`include "HRbridge.v"
`timescale 1ns/1ps
/*module connectRouter_nobuffer
#(parameter     addr = 4'b0000) //No. 0 connect router, port 0
(   
    input       `control_w  port_l0_i,
	output      `control_w  port_l0_o,
	input 		`control_w 	FIFO_l0_i,
	output 		`control_w 	FIFO_l0_o,
	input 					bfull_l0_i,
	input                   clk,
    input                   rst,
	output 					deQ_l0_o,
	output 					enQ_l0_o,
*/
module tb(
);

  
  reg clk, rst;
  
  reg `control_w port_l0, port_l1, port_g0, port_g1, port_g2, port_g3;
  reg `control_w FIFO_l0, FIFO_l1, FIFO_g0, FIFO_g1, FIFO_g2, FIFO_g3;
  reg bfull_l0,  bfull_l1,  bfull_g0,  bfull_g1,  bfull_g2,  bfull_g3;
  
  wire `control_w port_l0_o, port_l1_o, port_g0_o, port_g1_o, port_g2_o, port_g3_o;
  wire `control_w FIFO_l0_o, FIFO_l1_o, FIFO_g0_o, FIFO_g1_o, FIFO_g2_o, FIFO_g3_o;
  wire deQ_l0, deQ_l1, deQ_g0, deQ_g1, deQ_g2, deQ_g3; 
  wire enQ_l0, enQ_l1, enQ_g0, enQ_g1, enQ_g2, enQ_g3;
  
  wire accept, push;
  

  HRbridge r(
            .clk(clk),
            .rst(rst),
			.port_l0_i(port_l0), .port_l1_i(port_l1), .port_g0_i(port_g0), .port_g1_i(port_g1), .port_g2_i(port_g2), .port_g3_i(port_g3),
			.port_l0_o(port_l0_o), .port_l1_o(port_l1_o), .port_g0_o(port_g0_o), .port_g1_o(port_g1_o), .port_g2_o(port_g2_o), .port_g3_o(port_g3_o),
			.FIFO_l0_i(FIFO_l0), .FIFO_l1_i(FIFO_l1), .FIFO_g0_i(FIFO_g0), .FIFO_g1_i(FIFO_g1), .FIFO_g2_i(FIFO_g2), .FIFO_g3_i(FIFO_g3),  
			.FIFO_l0_o(FIFO_l0_o), .FIFO_l1_o(FIFO_l1_o), .FIFO_g0_o(FIFO_g0_o), .FIFO_g1_o(FIFO_g1_o), .FIFO_g2_o(FIFO_g2_o), .FIFO_g3_o(FIFO_g3_o),  
			.bfull_l0_i(bfull_l0), .bfull_l1_i(bfull_l1), .bfull_g0_i(bfull_g0), .bfull_g1_i(bfull_g1), .bfull_g2_i(bfull_g2), .bfull_g3_i(bfull_g3),   
			.deQ_l0_o(deQ_l0), .deQ_l1_o(deQ_l1), .deQ_g0_o(deQ_g0), .deQ_g1_o(deQ_g1), .deQ_g2_o(deQ_g2), .deQ_g3_o(deQ_g3), 
			.enQ_l0_o(enQ_l0), .enQ_l1_o(enQ_l1), .enQ_g0_o(enQ_g0), .enQ_g1_o(enQ_g1), .enQ_g2_o(enQ_g2), .enQ_g3_o(enQ_g3)
            );

  initial begin
//$set_toggle_region(tb.r);
//$toggle_start();

    clk = 0;
    rst = 0;
	port_l0 = 144'h0; port_l1 = 144'h0; port_g0 = 144'h0; port_g1 = 144'h0; port_g2 = 144'h0; port_g3 = 144'h0;
  	FIFO_l0 = 144'h0; FIFO_l1 = 144'h0; FIFO_g0 = 144'h0; FIFO_g1 = 144'h0; FIFO_g2 = 144'h0; FIFO_g3 = 144'h0;
  	bfull_l0 = 0; bfull_l1 = 0; bfull_g0 = 0; bfull_g1 = 0; bfull_g2 = 0; bfull_g3 = 0;
    port_l0 = 144'h0123456789abcdef0123456789abcdef185f;
	port_l1 = 144'h0111111111111111111111111111111f1854;
	port_g0 = 144'h0000000000000000000000000000000f1851;
	port_g1 = 144'h0111111111111111101654541645488f1850;
	bfull_l0 = 1;
	bfull_l1 = 1;
	bfull_g0 = 1;
	bfull_g1 = 1;
	FIFO_l0 = 144'h0123456789abcdef0123456789ffffff185f;
	FIFO_l1 = 144'h011111111111111111111111111fffff1856;
	FIFO_g0 = 144'h000000000000000000000000000fffff1852;
	FIFO_g1 = 144'h011111111111111f0123456789afffff1853;

#1;
clk = 1;
#1;
clk = 0;
    $display("clk = 0\n, port_l0 %04x\n, port_l1 %04x\n, port_g0 %04x\n, port_g1 %04x\n, port_g2 %04x\n, port_g3 %04x\n, FIFO_l0 %04x\n, FIFO_l1 %04x\n, FIFO_g0 %04x\n, FIFO_g1 %04x\n, FIFO_g2 %04x\n, FIFO_g3 %04x\n, deQ_l0 %04x\n, deQ_l1 %04x\n, deQ_g0 %04x\n, deQ_g1 %04x\n, deQ_g2 %04x\n, deQ_g3 %04x\n, enQ_l0 %04x\n, enQ_l1 %04x\n, enQ_g0 %04x\n, enQ_g1 %04x\n, enQ_g2 %04x\n, enQ_g3 %04x\n",
      	port_l0_o, port_l1_o, port_g0_o, port_g1_o, port_g2_o, port_g3_o, 
		FIFO_l0_o, FIFO_l1_o, FIFO_g0_o, FIFO_g1_o, FIFO_g2_o, FIFO_g3_o,
		deQ_l0, deQ_l1, deQ_g0, deQ_g1, deQ_g2, deQ_g3,
		enQ_l0, enQ_l1, enQ_g0, enQ_g1, enQ_g2, enQ_g3);
#1;
clk = 1;
#1;
clk = 0;
	port_l0 = 144'h0; port_l1 = 144'h0; port_g0 = 144'h0; port_g1 = 144'h0; port_g2 = 144'h0; port_g3 = 144'h0;
  	FIFO_l0 = 144'h0; FIFO_l1 = 144'h0; FIFO_g0 = 144'h0; FIFO_g1 = 144'h0; FIFO_g2 = 144'h0; FIFO_g3 = 144'h0;
  	bfull_l0 = 0; bfull_l1 = 0; bfull_g0 = 0; bfull_g1 = 0; bfull_g2 = 0; bfull_g3 = 0;

	$display("clk = 0\n, port_l0 %04x\n, port_l1 %04x\n, port_g0 %04x\n, port_g1 %04x\n, port_g2 %04x\n, port_g3 %04x\n, FIFO_l0 %04x\n, FIFO_l1 %04x\n, FIFO_g0 %04x\n, FIFO_g1 %04x\n, FIFO_g2 %04x\n, FIFO_g3 %04x\n, deQ_l0 %04x\n, deQ_l1 %04x\n, deQ_g0 %04x\n, deQ_g1 %04x\n, deQ_g2 %04x\n, deQ_g3 %04x\n, enQ_l0 %04x\n, enQ_l1 %04x\n, enQ_g0 %04x\n, enQ_g1 %04x\n, enQ_g2 %04x\n, enQ_g3 %04x\n",
      	port_l0_o, port_l1_o, port_g0_o, port_g1_o, port_g2_o, port_g3_o, 
		FIFO_l0_o, FIFO_l1_o, FIFO_g0_o, FIFO_g1_o, FIFO_g2_o, FIFO_g3_o,
		deQ_l0, deQ_l1, deQ_g0, deQ_g1, deQ_g2, deQ_g3,
		enQ_l0, enQ_l1, enQ_g0, enQ_g1, enQ_g2, enQ_g3);


//$toggle_stop();
//$toggle_report("./calf_backward_1.saif", 1.0e-9, "tb.r");
//$finish;

  end

endmodule
