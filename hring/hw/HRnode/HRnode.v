`include "defines.v"

module HRnode
#(parameter     addr = 4'b0010)
(   
    input       `control_w  port0_i,
    input       `control_w  port1_i,
    input       `control_w  port0_local_i,
    input       `control_w  port1_local_i,
    output                  portl0_ack,
    output 					portl1_ack,
    input                   clk,
    input                   rst,
    output      `control_w  port0_o,
    output      `control_w  port1_o,
    output      `control_w  port0_local_o,
    output      `control_w  port1_local_o
);

    // inputs
    wire `control_w port0_0, port1_0, port0_local_0, port1_local_0;

    assign port0_0 = (rst) ? `control_n'd0 : port0_i;
    assign port1_0 = (rst) ? `control_n'd0 : port1_i;
    assign port0_local_0 = (rst) ? `control_n'd0 : port0_local_i;
    assign port1_local_0 = (rst) ? `control_n'd0 : port1_local_i;

    /************* STAGE 1 *************/	
	
    reg `control_w port0_0_r, port1_0_r;
    always @(posedge clk) begin
        port0_0_r <= port0_0;
        port1_0_r <= port1_0;
    end

	wire ej_0 = port0_0_r[`valid_f] && (port0_0_r[`dest_f] == addr);
	wire ej_1 = port1_0_r[`valid_f] && (port1_0_r[`dest_f] == addr);

    assign port0_local_o = ej_0 ? port0_0_r : 0;
    assign port1_local_o = ej_1 ? port1_0_r : 0;

	wire valid0 = ej_0 ? 0 : port0_0_r[`valid_f];
	wire valid1 = ej_1 ? 0 : port1_0_r[`valid_f];

	assign port0_o = valid0 ? port0_0_r : port0_local_i;
	assign port1_o = valid1 ? port1_0_r : port1_local_i;
	assign portl0_ack = ~valid0 & port0_local_i[`valid_f];
	assign portl1_ack = ~valid1 & port1_local_i[`valid_f];

endmodule

