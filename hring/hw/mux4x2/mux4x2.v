`include "defines.v"

module mux4x2
(   
    input       `control_w  port0_ci,
	input 		`control_w  port1_ci,
    input       `control_w  port2_ci,
	input 		`control_w  port3_ci,
	input 		`control_w  portl0_ci,
	input		`control_w  portl1_ci,
	input 			[2:0]		sel0,
	input 			[2:0]		sel1,
    input                   clk,
    input                   rst,
    output      `control_w  port0_co,
    output      `control_w  port1_co
);

	
	assign port0_co = (sel0 == 0) ? port0_ci : 
		((sel0 == 1) ? port1_ci :
			((sel0 == 2) ? port2_ci : 
				((sel0 == 3) ? port3_ci : portl0_ci)));
	assign port1_co = (sel1 == 0) ? port0_ci : 
		((sel1 == 1) ? port1_ci :
			((sel1 == 2) ? port2_ci : 
				((sel1 == 3) ? port3_ci : portl1_ci)));

endmodule
