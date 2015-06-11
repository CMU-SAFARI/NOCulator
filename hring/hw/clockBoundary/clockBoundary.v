`include "defines.v"

module clockBoundary
#(parameter     addr = 4'b0101)
(   
    input       `control_w  port0_ci,
    input                   clk,
    input                   rst,
    output      `control_w  port0_co
);
  	reg `steer_w port0_r;
    always @(posedge clk) begin
        port0_r <= port0_ci;
    end

    assign port0_co = port0_r;

endmodule
