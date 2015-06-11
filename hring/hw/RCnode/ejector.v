`include "defines.v"

module ejector
(
    input [`addr_n-1:0] addr,
    input `steer_w c0,
    input `steer_w c1,
    output `steer_w c0_o,
    output `steer_w c1_o,

    output `steer_w c_ej0,
    output `steer_w c_ej1
);

	
    wire ej_0 = c0[`valid_f] && (c0[`dest_f] == addr);
    wire ej_1 = c1[`valid_f] && (c1[`dest_f] == addr);
    assign c_ej0 = ej_0 ? c0 : 0;    
    assign c_ej1 = ej_1 ? c1 : 0;
    assign c0_o = ej_0 ? 0 : c0;
    assign c1_o = ej_1 ? 0 : c1;
	    
endmodule
