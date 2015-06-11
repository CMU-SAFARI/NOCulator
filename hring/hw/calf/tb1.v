`include "defines.v"
`timescale 1ns/1ps

module tb(
   );

  wire injrd, injack;

  reg clk, rst;

  reg `control_w flit0c;

  wire `control_w port0_co, port1_co, port2_co, port3_co, port4_co;

  brouter r(
            .clk(clk),
            .rst(rst),
            
            .port0_ci(flit0c), .port0_co(port0_co),
            .port1_ci(144'h0), .port1_co(port1_co),
            .port2_ci(144'h0), .port2_co(port2_co),
            .port3_ci(144'h0), .port3_co(port3_co),
            .port4_ci(144'h0), .port4_co(port4_co),

            .port4_ready(injrd), .port4_ack(injack)
            );

  initial begin
$set_toggle_region(tb.r);
$toggle_start();

    clk = 0;
    rst = 0;

    flit0c = 144'h0123456789abcdef0123456789abcdef1857; // MSHR 1; valid / seq 0; source 5; dest 7

#1;
clk = 1;
#1;
clk = 0;

    flit0c = 144'h0;

    $display("clk = %d port0 %04x, port1 %04x, port2 %04x, port3 %04x, port4 %04x\n",
        clk, port0_co, port1_co, port2_co, port3_co, port4_co);

#1;
clk = 1;
#1;
clk = 0;


    $display("port0 %04x, port1 %04x, port2 %04x, port3 %04x, port4 %04x\n",
        port0_co, port1_co, port2_co, port3_co, port4_co);

#1;
clk = 1;
#1;
clk = 0;

    $display("port0 %04x, port1 %04x, port2 %04x, port3 %04x, port4 %04x\n",
        port0_co, port1_co, port2_co, port3_co, port4_co);

$toggle_stop();
$toggle_report("./calf_backward_1.saif", 1.0e-9, "tb.r");
$finish;

  end

endmodule