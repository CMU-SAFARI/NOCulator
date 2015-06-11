`include "defines.v"
/****
 * Compares 4 input ports based on their age and source
 * and outputs 4 priorities. The priorities contain the
 * port number that is that priority with p0 being the
 * highest and p3 being the lowest. Compares based on age
 * with highest more priority and then based on source
 * in the case of a tie.
 ****/
module priority_comparator(
    input       `control_w  control0,
    input       `control_w  control1,
    input       `control_w  control2,
    input       `control_w  control3,
    output      [1:0]   priority0,
    output      [1:0]   priority1,
    output      [1:0]   priority2,
    output      [1:0]   priority3);

    wire    `age_w ha00, ha01, ha10, ha11, ha20;
    wire    `src_w hs00, hs01, hs10, hs11, hs20;
    wire    `age_w la00, la01, la10, la11, la20;
    wire    `src_w ls00, ls01, ls10, ls11, ls20;
    wire    [1:0]  hp00, hp01, hp10, hp11, hp20;
    wire    [1:0]  lp00, lp01, lp10, lp11, lp20;
    wire           hv00, hv01, hv10, hv11, hv20;
    wire           lv00, lv01, lv10, lv11, lv20;
    
    // Stage 0
    pc_cell cmp00(.age0(control0[`age_f]),
                  .age1(control1[`age_f]),
                  .src0(control0[`src_f]),
                  .src1(control1[`src_f]),
                  .valid0(control0[`valid_f]),
                  .valid1(control1[`valid_f]),
                  .portno0(2'b00),
                  .portno1(2'b01),
                  .age_h(ha00),
                  .age_l(la00),
                  .src_h(hs00),
                  .src_l(ls00),
                  .valid_h(hv00),
                  .valid_l(lv00),
                  .portno_h(hp00),
                  .portno_l(lp00));
    pc_cell cmp01(.age0(control2[`age_f]),
                  .age1(control3[`age_f]),
                  .src0(control2[`src_f]),
                  .src1(control3[`src_f]),
                  .valid0(control2[`valid_f]),
                  .valid1(control3[`valid_f]),
                  .portno0(2'b10),
                  .portno1(2'b11),
                  .age_h(ha01),
                  .age_l(la01),
                  .src_h(hs01),
                  .src_l(ls01),
                  .valid_h(hv01),
                  .valid_l(lv01),
                  .portno_h(hp01),
                  .portno_l(lp01));

    // Stage 1
    pc_cell cmp10(.age0(ha00),
                  .age1(ha01),
                  .src0(hs00),
                  .src1(hs01),
                  .valid0(hv00),
                  .valid1(hv01),
                  .portno0(hp00),
                  .portno1(hp01),
                  .age_h(ha10),
                  .age_l(la10),
                  .src_h(hs10),
                  .src_l(ls10),
                  .valid_h(hv10),
                  .valid_l(lv10),
                  .portno_h(priority0),
                  .portno_l(lp10));
    pc_cell cmp11(.age0(la00),
                  .age1(la01),
                  .src0(ls00),
                  .src1(ls01),
                  .valid0(lv00),
                  .valid1(lv01),
                  .portno0(lp00),
                  .portno1(lp01),
                  .age_h(ha11),
                  .age_l(la11),
                  .src_h(hs11),
                  .src_l(ls11),
                  .valid_h(hv11),
                  .valid_l(lv11),
                  .portno_h(hp11),
                  .portno_l(priority3));

    // Stage 2
    pc_cell cmp20(.age0(la10),
                  .age1(ha11),
                  .src0(ls10),
                  .src1(hs11),
                  .valid0(lv10),
                  .valid1(hv11),
                  .portno0(lp10),
                  .portno1(hp11),
                  .age_h(ha20),
                  .age_l(la20),
                  .src_h(hs20),
                  .src_l(ls20),
                  .valid_h(hv20),
                  .valid_l(lv20),
                  .portno_h(priority1),
                  .portno_l(priority2));

endmodule


/****
 * @brief This is a small cell to compare two incoming requests
 *        and discern which one has higher priority first based
 *        on age and then based on the source destination.
 *        We do oldest first and in the case of a tie we do
 *        highest source node address.
 *        The entire module is combinational
 *
 ****/
module pc_cell (
    input       `age_w  age0,
    input       `age_w  age1,
    input       `src_w  src0,
    input       `src_w  src1,
    input               valid0,
    input               valid1,
    input       [1:0]   portno0,
    input       [1:0]   portno1,
    output      `age_w  age_h,
    output      `age_w  age_l,
    output      `src_w  src_h,
    output      `src_w  src_l,
    output              valid_h,
    output              valid_l,
    output      [1:0]   portno_h,
    output      [1:0]   portno_l);

    wire winner;

    assign winner = (valid0 && ~valid1) ? 1'b0 :
                    (~valid0 && valid1) ? 1'b1 :
                    (~valid0 && ~valid1) ? 1'b1 : 
                    (age0 > age1) ? 1'b0 :
                    (age0 < age1) ? 1'b1 :
                    (src0 > src1) ? 1'b0 :
                    (src0 < src1) ? 1'b1 : 1'b1; 
    
    assign portno_h = (winner == 1'b1) ? portno1 : portno0;
    assign portno_l = (winner == 1'b1) ? portno0 : portno1;
    assign valid_h = (winner == 1'b1) ? valid1 : valid0;
    assign valid_l = (winner == 1'b1) ? valid0: valid1;
    assign age_h = (winner == 1'b1) ? age1 : age0;
    assign age_l = (winner == 1'b1) ? age0 : age1;
    assign src_h = (winner == 1'b1) ? src1 : src0;
    assign src_l = (winner == 1'b1) ? src0 : src1;

endmodule

