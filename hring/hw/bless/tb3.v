`include "defines.v"
`timescale 1ns/1ps

module tb(
    input `control_w port0_ci,
    input `data_w port0_di,
    output `control_w port0_co,
    output `data_w port0_do,

    input `control_w port1_ci,
    input `data_w port1_di,
    output `control_w port1_co,
    output `data_w port1_do,

    input `control_w port2_ci,
    input `data_w port2_di,
    output `control_w port2_co,
    output `data_w port2_do,

    input `control_w port3_ci,
    input `data_w port3_di,
    output `control_w port3_co,
    output `data_w port3_do,

    input `control_w port4_ci,
    input `data_w port4_di,
    output `control_w port4_co,
    output `data_w port4_do

    );

  wire injrd, injack;

  reg clk, rst;

  reg `control_w flit0c;
  reg `data_w flit0d;

  reg `control_w flit1c;
  reg `data_w flit1d;

  reg `control_w flit2c;
  reg `data_w flit2d;


  brouter r(
            .clk(clk),
            .rst(rst),
            
            .port0_ci(flit0c), .port0_co(port0_co),
            .port1_ci(flit1c), .port1_co(port1_co),
            .port2_ci(flit2c), .port2_co(port2_co),
            .port3_ci(port3_ci), .port3_co(port3_co),
            .port4_ci(port4_ci), .port4_co(port4_co),

            .port0_di(flit0d), .port0_do(port0_do),
            .port1_di(flit1d), .port1_do(port1_do),
            .port2_di(flit2d), .port2_do(port2_do),
            .port3_di(port3_di), .port3_do(port3_do),
            .port4_di(port4_di), .port4_do(port4_do),

            .port4_ready(injrd)
            );

  initial begin
    clk = 0;
    rst = 0;

    flit0c = 22'h200001;
    flit0d = 128'h0123456789abcdef0123456789abcdef;

    flit1c = 22'h200802;
    flit1d = 128'h0123456789abcdef0123456789abcdef;

    flit2c = 22'h200c03;
    flit2d = 128'h0123456789abcdef0123456789abcdef;



#1;
clk = 1;
#1;
clk = 0;

    flit0c = 22'h0;
    flit0d = 128'h0;

    $display("port0 %04x, port1 %04x, port2 %04x, port3 %04x, port4 %04x\n",
        port0_co, port1_co, port2_co, port3_co, port4_co);
    $display("port0 %16x, port1 %16x, port2 %16x, port3 %16x, port4 %16x\n",
        port0_do, port1_do, port2_do, port3_do, port4_do);

#1;
clk = 1;
#1;
clk = 0;

    $display("port0 %04x, port1 %04x, port2 %04x, port3 %04x, port4 %04x\n",
        port0_co, port1_co, port2_co, port3_co, port4_co);
    $display("port0 %16x, port1 %16x, port2 %16x, port3 %16x, port4 %16x\n",
        port0_do, port1_do, port2_do, port3_do, port4_do);

#1;
clk = 1;
#1;
clk = 0;

    $display("port0 %04x, port1 %04x, port2 %04x, port3 %04x, port4 %04x\n",
        port0_co, port1_co, port2_co, port3_co, port4_co);
    $display("port0 %16x, port1 %16x, port2 %16x, port3 %16x, port4 %16x\n",
        port0_do, port1_do, port2_do, port3_do, port4_do);


  end

endmodule
