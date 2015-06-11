`include "defines.v"

module data_buf(   
    input       `data_w data0_in,
    input       `data_w data1_in,
    input       `data_w data2_in,
    input       `data_w data3_in,
    input       `data_w data4_in,
    input               clk,
    output  reg `data_w data0_out,
    output  reg `data_w data1_out,
    output  reg `data_w data2_out,
    output  reg `data_w data3_out,
    output  reg `data_w data4_out);

    always @(posedge clk) begin
        data0_out <= data0_in;
        data1_out <= data1_in;
        data2_out <= data2_in;
        data3_out <= data3_in;
        data4_out <= data4_in;
    end
endmodule
