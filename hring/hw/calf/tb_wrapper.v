`include "defines.v"

module tb_wrapper();

    wire `control_w c0, c1, c2, c3, c4;
    wire `data_w d0, d1, d2, d3, d4;

    reg clk;

    tb t(
            .clk(clk),
            .port0_ci(0), .port0_di(0), .port0_co(c0), .port0_do(d0),
            .port1_ci(0), .port1_di(0), .port1_co(c1), .port1_do(d1),
            .port2_ci(0), .port2_di(0), .port2_co(c2), .port2_do(d2),
            .port3_ci(0), .port3_di(0), .port3_co(c3), .port3_do(d3),
            .port4_ci(0), .port4_di(0), .port4_co(c4), .port4_do(d4)
            );

    initial clk = 0;
    always begin
        #1;
        clk = ~clk;
    end

endmodule
