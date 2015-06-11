`include "defines.v"

module mux2x4
(   
    input       `control_w  port0_ci,
	input 		`control_w  port1_ci,
	input 		`control_w  portl0_ci,
	input 		`control_w  portl1_ci,
	input 		`control_w  portl2_ci,
	input 		`control_w  portl3_ci,
	input 			[1:0]		sel0,
	input 			[1:0]		sel1,
	input 			[1:0]		sel2,
	input 			[1:0]		sel3,
    input                   clk,
    input                   rst,
    output      `control_w  port0_co,
    output      `control_w  port1_co,
    output      `control_w  port2_co,
    output      `control_w  port3_co
);

	assign port0_co = (sel0 == 0) ? port0_ci : 
		((sel0 == 1) ? port1_ci : portl0_ci);
	assign port1_co = (sel1 == 0) ? port0_ci : 
		((sel1 == 1) ? port1_ci : portl1_ci);
	assign port2_co = (sel2 == 0) ? port0_ci : 
		((sel2 == 1) ? port1_ci : portl2_ci);
	assign port3_co = (sel3 == 0) ? port0_ci : 
		((sel3 == 1) ? port1_ci : portl3_ci);

endmodule
