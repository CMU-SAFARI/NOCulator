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
    input       [1:0]       rr,
    output      [1:0]       priority0,
    output      [1:0]       priority1,
    output      [1:0]       priority2,
    output      [1:0]       priority3);

    wire    `control_w  hc00, hc01, hc10, hc11, hc20,   // Control forwarding
                        lc00, lc01, lc10, lc11, lc20;   //     between rounds
    wire    [1:0]       hp00, hp01, hp10, hp11, hp20,   // Port number forwarding
                        lp00, lp01, lp10, lp11, lp20;   //     between rounds
    
    // Stage 0
    pc_cell cmp00(.c0_in(control0),
                  .c1_in(control1),
                  .portno0(2'b00),
                  .portno1(2'b01),
                  .rr(rr),
                  .ch_out(hc00),
                  .cl_out(lc00),
                  .portno_h(hp00),
                  .portno_l(lp00));
    pc_cell cmp01(.c0_in(control2),
                  .c1_in(control3),
                  .portno0(2'b10),
                  .portno1(2'b11),
                  .rr(rr),
                  .ch_out(hc01),
                  .cl_out(lc01),
                  .portno_h(hp01),
                  .portno_l(lp01));

    // Stage 1
    pc_cell cmp10(.c0_in(hc00),
                  .c1_in(hc01),
                  .portno0(hp00),
                  .portno1(hp01),
                  .rr(rr),
                  .ch_out(hc10),
                  .cl_out(lc10),
                  .portno_h(priority0),
                  .portno_l(lp10));
    pc_cell cmp11(.c0_in(lc00),
                  .c1_in(lc01),
                  .portno0(lp00),
                  .portno1(lp01),
                  .rr(rr),
                  .ch_out(hc11),
                  .cl_out(lc11),
                  .portno_h(hp11),
                  .portno_l(priority3));

    // Stage 2
    pc_cell cmp20(.c0_in(lc10),
                  .c1_in(hc11),
                  .portno0(lp10),
                  .portno1(hp11),
                  .rr(rr),
                  .ch_out(hc20),
                  .cl_out(lc20),
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
    input       `control_w  c0_in,      // Control field of first
    input       `control_w  c1_in,      // Control field of second
    input       [1:0]       portno0,    // Port number of first
    input       [1:0]       portno1,    // Port number of second
    input       [1:0]       rr,         // Current round robin round
    output      `control_w  ch_out,     // Control field of higher 
    output      `control_w  cl_out,     // Control field of lower
    output      [1:0]       portno_h,   // Port number of higher
    output      [1:0]       portno_l);  // Port number of lower

    wire        valid0, valid1;
    wire        gold0, gold1;
    wire `seq_w seq0, seq1;
    wire        winner;

    assign valid0 = c0_in[`valid_f];
    assign valid1 = c1_in[`valid_f];
    assign gold0 = c0_in[`gold_f];
    assign gold1 = c1_in[`gold_f];
    assign seq0 = c0_in[`seq_f];
    assign seq1 = c1_in[`seq_f];

    assign winner = (valid0 && ~valid1) ? 1'b0 :    // Only 1st is valid
                    (~valid0 && valid1) ? 1'b1 :    // Only 2nd is valid
                    (~valid0 && ~valid1) ? 1'b1 :   // Both are invalid
                    (gold0 && ~gold1) ? 1'b0 :      // Only 1st is gold
                    (~gold0 && gold1) ? 1'b1 :      // Only 2nd is gold
                    (gold0 && gold1) ?             // Both are gold
                        ((seq0 < seq1) ? 1'b0 :          // Both gold, 1st is lower
                         (seq0 > seq1) ? 1'b1 : 1'b1) :  // Both gold, 2nd is lower
                    ((rr - portno0) < (rr - portno1)) ? 1'b0 : 1'b1;
    
    // Output the winner and loser of this round of arbitration
    assign portno_h = (winner == 1'b1) ? portno1 : portno0;
    assign portno_l = (winner == 1'b1) ? portno0 : portno1;
    assign ch_out = (winner == 1'b1) ? c1_in : c0_in;
    assign cl_out = (winner == 1'b1) ? c0_in : c1_in; 

endmodule

