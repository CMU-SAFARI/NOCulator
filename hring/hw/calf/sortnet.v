`include "defines.v"

`define goldsel_w [7:0]

module sortnet
(
    input clk,
    input `rmatrix_w rmatrix0,
    input `rmatrix_w rmatrix1,
    input `rmatrix_w rmatrix2,
    input `rmatrix_w rmatrix3,
    input `steer_w control0_in,
    input `steer_w control1_in,
    input `steer_w control2_in,
    input `steer_w control3_in,
    output `steer_w control0_out,
    output `steer_w control1_out,
    output `steer_w control2_out,
    output `steer_w control3_out
);

    wire `goldsel_w gold_sel;
    wire [15:0] rand;
    gold_counter gc(.clk(clk), .gold_sel(gold_sel));
    pseudorandom ps(.clk(clk), .pseudorand(rand));

    wire `steer_w inter0, inter1, inter2, inter3;
    wire `rmatrix_w inter0_rm, inter1_rm, inter2_rm, inter3_rm;

//    always @(*)
//        $display("sortnet 0: %04x %04x %04x %04x (gold_sel %d)", control0_in, control1_in, control2_in, control3_in, gold_sel);

    arbiter_block arb0(
            .gold_sel(gold_sel),
            .pseudorand(rand[3]),

            .in0(control0_in),
            .in1(control2_in),
            .rmatrix0(rmatrix0),
            .rmatrix1(rmatrix2),
            .out0(inter0),
            .out1(inter2),
            .rmout0(inter0_rm),
            .rmout1(inter2_rm),

            .route_0(4'b1100),
            .route_1(4'b0011));

    arbiter_block arb1(
            .gold_sel(gold_sel),
            .pseudorand(rand[3]),

            .in0(control1_in),
            .in1(control3_in),
            .rmatrix0(rmatrix1),
            .rmatrix1(rmatrix3),
            .out0(inter1),
            .out1(inter3),
            .rmout0(inter1_rm),
            .rmout1(inter3_rm),

            .route_0(4'b1100),
            .route_1(4'b0011));

    wire `rmatrix_w dummy0_rm, dummy1_rm, dummy2_rm, dummy3_rm;

//    always @(*)
//        $display("sortnet 1: %04x %04x %04x %04x", inter0, inter1, inter2, inter3);

    arbiter_block arb2(
            .gold_sel(gold_sel),
            .pseudorand(rand[3]),

            .in0(inter0),
            .in1(inter1),
            .rmatrix0(inter0_rm),
            .rmatrix1(inter1_rm),
            .out0(control0_out),
            .out1(control1_out),
            .rmout0(dummy0_rm),
            .rmout1(dummy1_rm),

            .route_0(4'b1000),
            .route_1(4'b0100));

    arbiter_block arb3(
            .gold_sel(gold_sel),
            .pseudorand(rand[3]),

            .in0(inter2),
            .in1(inter3),
            .rmatrix0(inter2_rm),
            .rmatrix1(inter3_rm),
            .out0(control2_out),
            .out1(control3_out),
            .rmout0(dummy2_rm),
            .rmout1(dummy3_rm),

            .route_0(4'b0010),
            .route_1(4'b0001));

//    always @(*)
//        $display("sortnet 2: %04x %04x %04x %04x", control0_out , control1_out , control2_out , control3_out );



endmodule

module arbiter_block
(
    input pseudorand,
    input `goldsel_w gold_sel,

    input `steer_w in0,
    input `steer_w in1,
    output `steer_w out0,
    output `steer_w out1,

    input `rmatrix_w rmatrix0,
    input `rmatrix_w rmatrix1,
    output `rmatrix_w rmout0,
    output `rmatrix_w rmout1,
    input `rmatrix_w route_0,
    input `rmatrix_w route_1
);

    wire desired0 = (rmatrix0 & route_1) != 0 ? 1 : 0;
    wire desired1 = (rmatrix1 & route_1) != 0 ? 1 : 0;

    // decide winner
    wire valid0 = in0[`valid_f], valid1 = in1[`valid_f];
    wire `goldsel_w goldnum0 = { in0[`mshr_f], in0[`src_f] }, goldnum1 = { in1[`mshr_f], in1[`src_f] };
    wire gold0 = (goldnum0 == gold_sel), gold1 = (goldnum1 == gold_sel);
    wire seq0 = in0[`seq_f], seq1 = in1[`seq_f];

    wire winner = ( 
            // in1 wins if: both valid both gold and tiebreak 1; both valid 1 gold; both valid,
            // neither gold, pseudorand; only 1 is valid.
            (valid0 && valid1) && (gold0 && gold1) && (seq1 > seq0) ||
            (valid0 && valid1) && (~gold0 && gold1) ||
            (valid0 && valid1) && (~gold0 && ~gold1) && pseudorand ||
            (~valid0 && valid1)
            );

    // swap?
    wire swap =
        winner ? ~desired1 : desired0;

//    always @(*)
//        $display("pseudorand = %d goldsel = %d in0 = %05x in1 = %05x rm0 = %x rm1 = %x desired0 = %d desired1 = %d valid0 = %d valid1 = %d gold0 = %d gold1 = %d winner = %d swap = %d out0 = %x out1 = %x",
//                pseudorand, gold_sel, in0, in1, rmatrix0, rmatrix1, desired0, desired1, valid0, valid1, gold0, gold1, winner, swap, out0, out1);

    // assign

    assign rmout0 = swap ? rmatrix1 : rmatrix0;
    assign rmout1 = swap ? rmatrix0 : rmatrix1;
    assign out0 = swap ? in1 : in0;
    assign out1 = swap ? in0 : in1;

endmodule

module gold_counter
(
    input clk,
    output reg `goldsel_w gold_sel
);

    initial gold_sel = 0;
    always @(posedge clk) gold_sel <= gold_sel + 1;

endmodule

module pseudorandom
(
    input clk,
    output reg [15:0] pseudorand
);

    // LFSR, taps at 7 and 3

    always @(posedge clk)
        pseudorand <= { pseudorand[14:0], pseudorand[7] ^ pseudorand[3] };
    initial pseudorand = 0;

endmodule
