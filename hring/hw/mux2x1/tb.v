`include "defines.v"
`include "mux2x1.v"
`timescale 1ns/1ps
/*module mux2x1
(   
    input       `control_w  port0_ci,
	input 		`control_w  port1_ci,
	input 					sel,
    input                   clk,
    input                   rst,
    output      `control_w  port0_co
)*/

module tb(
    );

  reg `control_w port0, port1;

  reg clk, rst;

  wire `control_w port0_co;

  mux2x1 r(
            .clk(clk),
            .rst(rst),
			.sel(1),
			.port1_ci(port1),
            .port0_ci(port0), .port0_co(port0_co)
            );

  initial begin
//$set_toggle_region(tb.r);
//$toggle_start();

    clk = 0;
    rst = 0;
	
	port0 = 144'h0123456789abcdef0123456789abcdef1850;
	port1 = 144'h0123456789abcdef0123456789abcdef1851;


#1;
clk = 1;
#1;
clk = 0;

    $display("port0_co %04x\n",
        port0_co);

//$toggle_stop();
//$toggle_report("./calf_backward_0.saif", 1.0e-9, "tb.r");
//$finish;

  end

endmodule
