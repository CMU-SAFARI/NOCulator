`include "defines.v"
`include "mux2x4.v"
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

  reg `control_w port0, port1, portl0, portl1, portl2, portl3;

  reg clk, rst;

  wire `control_w port0_co, port1_co, port2_co, port3_co;

  mux2x4 r(
            .clk(clk),
            .rst(rst),
			.sel0(1),
			.sel1(1),
			.sel2(2),
			.sel3(3),
			.port1_ci(port1),
            .port0_ci(port0),
			.portl0_ci(portl0),
			.portl1_ci(portl1),
			.portl2_ci(portl2),
			.portl3_ci(portl3),
			.port0_co(port0_co),
			.port1_co(port1_co),
			.port2_co(port2_co),
			.port3_co(port3_co)
            );

  initial begin
//$set_toggle_region(tb.r);
//$toggle_start();

    clk = 0;
    rst = 0;
	
	port0 = 144'h0123456789abcdef0123456789abcdef1850;
	port1 = 144'h0123456789abcdef0123456789abcdef1851;
	portl0 = 144'h0123456789abcdef0123456789abcdef1852;
	portl1 = 144'h0123456789abcdef0123456789abcdef1853;
	portl2 = 144'h0123456789abcdef0123456789abcdef1854;
	portl3 = 144'h0123456789abcdef0123456789abcdef1855;


#1;
clk = 1;
#1;
clk = 0;

    $display("port0_co %04x\nport1_co %04x\nport2_co %04x\nport3_co %04x\n",
        port0_co, port1_co, port2_co, port3_co);

//$toggle_stop();
//$toggle_report("./calf_backward_0.saif", 1.0e-9, "tb.r");
//$finish;

  end

endmodule
