`include "defines.v"

module arbitor (
    input       `rmatrix_w  rmatrix0,
    input       `rmatrix_w  rmatrix1,
    input       `rmatrix_w  rmatrix2,
    input       `rmatrix_w  rmatrix3,
    input       `rmatrix_w  rmatrix4,
    input       `control_w  control0_in,
    input       `control_w  control1_in,
    input       `control_w  control2_in,
    input       `control_w  control3_in,
    input       `control_w  control4_in,
    input                   clk,
    output  reg `routecfg_w route_config,
    output `routecfg_w route_config_unbuf);

   
    // Priority Comparator 
    wire    [1:0]   p0, p1, p2, p3;
   
    // We only use the bottom 2 bits since the upper
    // bit on priority is reserved for the resource
    priority_comparator priorc(.control0(control0_in),
                               .control1(control1_in),
                               .control2(control2_in),
                               .control3(control3_in),
                               .priority0(p0),
                               .priority1(p1),
                               .priority2(p2),
                               .priority3(p3));
  
    // Arbitor Tree
    wire    `amatrix_w      amatrix1, amatrix2, amatrix3, 
                            amatrix4, amatrix5;
    wire    `routecfg_w     rc1, rc2, rc3, rc4, rc5;
    
    arbitor_cell prior0(.priority({1'b0, p0}),
                        .rmatrix0(rmatrix0),
                        .rmatrix1(rmatrix1),
                        .rmatrix2(rmatrix2),
                        .rmatrix3(rmatrix3),
                        .rmatrix4(rmatrix4),
                        .valid0(control0_in[`valid_f]),
                        .valid1(control1_in[`valid_f]),
                        .valid2(control2_in[`valid_f]),
                        .valid3(control3_in[`valid_f]),
                        .valid4(control4_in[`valid_f]),
                        .amatrix_o(`amatrix_n'd0),
                        .rco(`routecfg_n'h7FFF),
                        .amatrix_n(amatrix1),
                        .rcn(rc1));
   
    arbitor_cell prior1(.priority({1'b0, p1}),
                        .rmatrix0(rmatrix0),
                        .rmatrix1(rmatrix1),
                        .rmatrix2(rmatrix2),
                        .rmatrix3(rmatrix3),
                        .rmatrix4(rmatrix4),
                        .valid0(control0_in[`valid_f]),
                        .valid1(control1_in[`valid_f]),
                        .valid2(control2_in[`valid_f]),
                        .valid3(control3_in[`valid_f]),
                        .valid4(control4_in[`valid_f]),
                        .amatrix_o(amatrix1),
                        .rco(rc1),
                        .amatrix_n(amatrix2),
                        .rcn(rc2));

    arbitor_cell prior2(.priority({1'b0, p2}),
                        .rmatrix0(rmatrix0),
                        .rmatrix1(rmatrix1),
                        .rmatrix2(rmatrix2),
                        .rmatrix3(rmatrix3),
                        .rmatrix4(rmatrix4),
                        .valid0(control0_in[`valid_f]),
                        .valid1(control1_in[`valid_f]),
                        .valid2(control2_in[`valid_f]),
                        .valid3(control3_in[`valid_f]),
                        .valid4(control4_in[`valid_f]),
                        .amatrix_o(amatrix2),
                        .rco(rc2),
                        .amatrix_n(amatrix3),
                        .rcn(rc3));

    arbitor_cell prior3(.priority({1'b0, p3}),
                        .rmatrix0(rmatrix0),
                        .rmatrix1(rmatrix1),
                        .rmatrix2(rmatrix2),
                        .rmatrix3(rmatrix3),
                        .rmatrix4(rmatrix4),
                        .valid0(control0_in[`valid_f]),
                        .valid1(control1_in[`valid_f]),
                        .valid2(control2_in[`valid_f]),
                        .valid3(control3_in[`valid_f]),
                        .valid4(control4_in[`valid_f]),
                        .amatrix_o(amatrix3),
                        .rco(rc3),
                        .amatrix_n(amatrix4),
                        .rcn(rc4));
    
    arbitor_cell prior4(.priority(3'b100),
                        .rmatrix0(rmatrix0),
                        .rmatrix1(rmatrix1),
                        .rmatrix2(rmatrix2),
                        .rmatrix3(rmatrix3),
                        .rmatrix4(rmatrix4),
                        .valid0(control0_in[`valid_f]),
                        .valid1(control1_in[`valid_f]),
                        .valid2(control2_in[`valid_f]),
                        .valid3(control3_in[`valid_f]),
                        .valid4(control4_in[`valid_f]),
                        .amatrix_o(amatrix4),
                        .rco(rc4),
                        .amatrix_n(amatrix5),
                        .rcn(rc5));

    assign route_config_unbuf = rc5;

    always @(posedge clk) begin
        route_config <= rc5; 
    end
    
endmodule

module arbitor_cell(
    input   [2:0]       priority,
    input   `rmatrix_w  rmatrix0,
    input   `rmatrix_w  rmatrix1,
    input   `rmatrix_w  rmatrix2,
    input   `rmatrix_w  rmatrix3,
    input   `rmatrix_w  rmatrix4,
    input               valid0,
    input               valid1,
    input               valid2,
    input               valid3,
    input               valid4,
    input   `amatrix_w  amatrix_o,
    input   `routecfg_w rco,
    output  `amatrix_w  amatrix_n,
    output  `routecfg_w rcn);

    wire    `rmatrix_w  smatrix;    // The route matrix we will use
    wire    [2:0]       selected;   // Selected port to use
    wire                svalid;
    
    // Select the current priorities routing matrix
    assign smatrix = (priority == `rmatrix_n'd0) ? rmatrix0 :
                     (priority == `rmatrix_n'd1) ? rmatrix1 :
                     (priority == `rmatrix_n'd2) ? rmatrix2 :
                     (priority == `rmatrix_n'd3) ? rmatrix3 : 
                     (priority == `rmatrix_n'd4) ? rmatrix4 : `rmatrix_n'bZ;

    // Select which valid line to use
    assign svalid = (priority == `rmatrix_n'd0) ? valid0 :
                    (priority == `rmatrix_n'd1) ? valid1 :
                    (priority == `rmatrix_n'd2) ? valid2 :
                    (priority == `rmatrix_n'd3) ? valid3 :
                    (priority == `rmatrix_n'd4) ? valid4 : 1'b0;
    
    // Select the route to take. Prioritize X directions over Y directions
    // 0 = North
    // 1 = South
    // 2 = East
    // 3 = West
    // 4 = Resource
    // 7 = Invalid
    assign selected = (svalid == 1'b0) ? 3'd7 :
                      // At Destination
                      (~|(smatrix)  && ~amatrix_o[4]) ? 3'd4 :
                      // To Target
                      (smatrix[0] && ~amatrix_o[0]) ? 3'd0 :
                      (smatrix[1] && ~amatrix_o[1]) ? 3'd1 :
                      (smatrix[2] && ~amatrix_o[2]) ? 3'd2 :
                      (smatrix[3] && ~amatrix_o[3]) ? 3'd3 : 
                      // Misroute
                      (~amatrix_o[0]) ? 3'd0 :                
                      (~amatrix_o[1]) ? 3'd1 :
                      (~amatrix_o[2]) ? 3'd2 :
                      (~amatrix_o[3]) ? 3'd3 : 3'd7;

    // Update the route computed table
    assign rcn = (selected == 3'd0) ? {rco[`routecfg_4], rco[`routecfg_3], 
                                       rco[`routecfg_2], rco[`routecfg_1],
                                       priority} :
                 (selected == 3'd1) ? {rco[`routecfg_4], rco[`routecfg_3],
                                       rco[`routecfg_2], priority, 
                                       rco[`routecfg_0]} :
                 (selected == 3'd2) ? {rco[`routecfg_4], rco[`routecfg_3],
                                       priority,         rco[`routecfg_1],
                                       rco[`routecfg_0]} :
                 (selected == 3'd3) ? {rco[`routecfg_4], priority, 
                                       rco[`routecfg_2], rco[`routecfg_1],
                                       rco[`routecfg_0]} :
                 (selected == 3'd4) ? {priority,         rco[`routecfg_3],
                                       rco[`routecfg_2], rco[`routecfg_1],
                                       rco[`routecfg_0]} : 
                 (selected == 3'd7) ? rco : rco;

    // Update the assigned matrix
    assign amatrix_n = (selected == 3'd0) ? amatrix_o | `amatrix_n'd1 :
                       (selected == 3'd1) ? amatrix_o | `amatrix_n'd2 :
                       (selected == 3'd2) ? amatrix_o | `amatrix_n'd4 :
                       (selected == 3'd3) ? amatrix_o | `amatrix_n'd8 :
                       (selected == 3'd4) ? amatrix_o | `amatrix_n'd16: 
                       amatrix_o;
endmodule
