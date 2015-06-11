`include "defines.v"

module injector
(
    input `steer_w c0,
    input `steer_w c1,
    input `steer_w c2,
    input `steer_w c3,
    output `steer_w c0_o,
    output `steer_w c1_o,
    output `steer_w c2_o,
    output `steer_w c3_o,

    input `steer_w c_in,
    output used
);

    wire
        valid0 = c0[`valid_f], valid1 = c1[`valid_f],
        valid2 = c2[`valid_f], valid3 = c3[`valid_f],
        valid_inj = c_in[`valid_f];

    wire inj_0 = valid_inj && ~valid0;
    wire inj_1 = valid_inj && valid0 && ~valid1;
    wire inj_2 = valid_inj && valid0 && valid1 && ~valid2;
    wire inj_3 = valid_inj && valid0 && valid1 && valid2 && ~valid3;

    assign used = valid_inj & (~valid0 || ~valid1 || ~valid2 || ~valid3);

    assign c0_o = inj_0 ? c_in : c0;
    assign c1_o = inj_1 ? c_in : c1;
    assign c2_o = inj_2 ? c_in : c2;
    assign c3_o = inj_3 ? c_in : c3;

endmodule
