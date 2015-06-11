`include "defines.v"
`include "clockBoundary.v"
`timescale 1ns/1ps

module tb(
    );

  reg `control_w port0;

  reg clk, rst;

  wire `control_w port0_co;

  clockBoundary r(
            .clk(clk),
            .rst(rst),

            .port0_ci(port0), .port0_co(port0_co)
            );

  initial begin
//$set_toggle_region(tb.r);
//$toggle_start();

    clk = 0;
    rst = 0;
	port0 = 128'h0;
//	port0 = 144'h0123456789abcdef0123456789abcdef;

#1;
clk = 1;
#1;
clk = 0;

    $display("port0_co %04x\n",
        port0_co);

#1;
clk = 1;
#1;
clk = 0;

    $display("port0_co %04x\n",
        port0_co);


//$toggle_stop();
//$toggle_report("./calf_backward_0.saif", 1.0e-9, "tb.r");
//$finish;

  end

endmodule
