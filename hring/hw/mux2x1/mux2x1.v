`include "defines.v"

module mux2x1
(   
    input       `control_w  port0_ci,
	input 		`control_w  port1_ci,
	input 					sel,
    input                   clk,
    input                   rst,
    output      `control_w  port0_co
);

	assign port0_co = sel ? port0_ci : port1_ci;

endmodule
