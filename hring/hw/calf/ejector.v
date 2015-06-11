`include "defines.v"

module ejector
(
    input [`addr_n-1:0] addr,
    input `steer_w c0,
    input `steer_w c1,
    input `steer_w c2,
    input `steer_w c3,
    output `steer_w c0_o,
    output `steer_w c1_o,
    output `steer_w c2_o,
    output `steer_w c3_o,

    output `steer_w c_out
);

    wire ej_0 = c0[`valid_f] && (c0[`dest_f] == addr);
    wire ej_1 = c1[`valid_f] && (c1[`dest_f] == addr);
    wire ej_2 = c2[`valid_f] && (c2[`dest_f] == addr);
    wire ej_3 = c3[`valid_f] && (c3[`dest_f] == addr);

    wire ej0 = ej_0;
    wire ej1 = ~ej_0 && ej_1;
    wire ej2 = ~ej_0 && ~ej_1 && ej_2;
    wire ej3 = ~ej_0  && ~ej_1 && ~ej_2 && ej_3;

    assign c0_o = ej0 ? 0 : c0;
    assign c1_o = ej1 ? 0 : c1;
    assign c2_o = ej2 ? 0 : c2;
    assign c3_o = ej3 ? 0 : c3;

    assign c_out =
        ej_0 ? c0 :
        (ej_1 ? c1 :
         (ej_2 ? c2 :
          (ej_3 ? c3 : 0)));
    
endmodule
