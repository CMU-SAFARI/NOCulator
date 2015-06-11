`include "defines.v"

module xbar(
        input [2:0] ctl0,
        input [2:0] ctl1,
        input [2:0] ctl2,
        input [2:0] ctl3,
        input [2:0] ctl4,
        input `data_w di0,
        input `data_w di1,
        input `data_w di2,
        input `data_w di3,
        input `data_w di4,
        output `data_w do0,
        output `data_w do1,
        output `data_w do2,
        output `data_w do3,
        output `data_w do4
        );

    xbar_mux mux0(
            .di0(di0),
            .di1(di1),
            .di2(di2),
            .di3(di3),
            .di4(di4),
            .do(do0),
            .sel(ctl0));

    xbar_mux mux1(
            .di0(di0),
            .di1(di1),
            .di2(di2),
            .di3(di3),
            .di4(di4),
            .do(do1),
            .sel(ctl1));

    xbar_mux mux2(
            .di0(di0),
            .di1(di1),
            .di2(di2),
            .di3(di3),
            .di4(di4),
            .do(do2),
            .sel(ctl2));

    xbar_mux mux3(
            .di0(di0),
            .di1(di1),
            .di2(di2),
            .di3(di3),
            .di4(di4),
            .do(do3),
            .sel(ctl3));

    xbar_mux mux4(
            .di0(di0),
            .di1(di1),
            .di2(di2),
            .di3(di3),
            .di4(di4),
            .do(do4),
            .sel(ctl4));

endmodule

module xbar_mux(
        input `data_w di0,
        input `data_w di1,
        input `data_w di2,
        input `data_w di3,
        input `data_w di4,
        output reg `data_w do,
        input [2:0] sel);

    always @(sel or di0 or di1 or di2 or di3 or di4) begin
        case (sel)
            0: do <= di0;
            1: do <= di1;
            2: do <= di2;
            3: do <= di3;
            4: do <= di4;
        endcase
    end
endmodule
