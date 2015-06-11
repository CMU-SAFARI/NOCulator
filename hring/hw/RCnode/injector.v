`include "defines.v"

module injector
(
    input `steer_w c0,
    input `steer_w c1,
    output `steer_w c0_o,
    output `steer_w c1_o,

    input `steer_w c_in0,
    input `steer_w c_in1,    
    output ack0,
    output ack1
);

	assign c0_o = c0[`valid_f] ? c0 : c_in0;
	assign c1_o = c1[`valid_f] ? c1 : c_in1;
	assign ack0 = ~c0[`valid_f] & c_in0[`valid_f];
	assign ack1 = ~c1[`valid_f] & c_in1[`valid_f];
	
endmodule
