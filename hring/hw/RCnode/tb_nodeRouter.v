`include "defines.v"
`include "nodeRouter.v"
`timescale 1ns/1ps
/*
    input       `control_w  port0_ci,
    input       `control_w  port1_ci,
    input       `control_w  portl0_ci,
    input       `control_w  portl1_ci,   
    output                  portl0_ack,
    output 					portl1_ack,
    input                   clk,
    input                   rst,
    output      `control_w  port0_co,
    output      `control_w  port1_co,
    output      `control_w  portl0_co,
    output      `control_w  portl1_co,   
*/
module tb(
   );

  wire ack0, ack1;
  
  reg clk, rst;
  
  reg `control_w flit0c, flit1c, flitl0, flitl1;
  
  wire `control_w port0_co, port1_co, portl0_co, portl1_co;

  nodeRouter r(
            .clk(clk),
            .rst(rst),
            
            .port0_ci(flit0c), .port0_co(port0_co),
            .port1_ci(flit1c), .port1_co(port1_co),
            .portl0_ci(flitl0), .portl0_co(portl0_co),
            .portl1_ci(flitl1), .portl1_co(portl1_co),

            .portl0_ack(ack0), .portl1_ack(ack1)
            );

  initial begin
//$set_toggle_region(tb.r);
//$toggle_start();

    clk = 0;
    rst = 0;

    flit0c = 144'h0aaaaaaaaaabcdef0123456789abcdef1852; // MSHR 1; valid / seq 0; source 5; dest 7
	flit1c = 144'h0aaaaaaaaaabcdef00000000000bcdef1857;
	flitl0 = 144'h0a00000000000000000000000000000f1853;
	flitl1 = 144'h011111111111111111111111111111111857;
#1;
clk = 1;
#1;
clk = 0;

    //flit1c = 144'h0123456789abcdef0123456789abcdef1852;
    flit0c = 144'h0;
    flit1c = 144'h0;
    $display("clk = %d\n, port0 %04x\n, port1 %04x\n, portl0_co %04x\n, portl1_co %04x\n, portl0_ack %04x\n, portl1_ack %04x\n",
        clk, port0_co, port1_co, portl0_co, portl1_co, ack0, ack1);
#1;
clk = 1;
#1;
clk = 0;

    //flit1c = 144'h0123456789abcdef0123456789abcdef1852;
	flitl0 = 144'h0;
    flitl1 = 144'h0;
    $display("clk = %d\n, port0 %04x\n, port1 %04x\n, portl0_co %04x\n, portl1_co %04x\n, portl0_ack %04x\n, portl1_ack %04x\n",
        clk, port0_co, port1_co, portl0_co, portl1_co, ack0, ack1);
        
#1;
clk = 1;
#1;
clk = 0;

    //flit1c = 144'h0123456789abcdef0123456789abcdef1852;

    $display("clk = %d\n, port0 %04x\n, port1 %04x\n, portl0_co %04x\n, portl1_co %04x\n, portl0_ack %04x\n, portl1_ack %04x\n",
        clk, port0_co, port1_co, portl0_co, portl1_co, ack0, ack1);

//$toggle_stop();
//$toggle_report("./calf_backward_1.saif", 1.0e-9, "tb.r");
//$finish;

  end

endmodule
