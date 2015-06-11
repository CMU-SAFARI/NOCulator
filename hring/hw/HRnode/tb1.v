`include "defines.v"
`include "HRnode.v"
`timescale 1ns/1ps
/*module HRnode
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
);*/

module tb(
   );

  wire ack0, ack1;
  
  reg clk, rst;
  
  reg `control_w flit0c, flit1c, flitl0, flitl1;
  
  wire `control_w port0_co, port1_co, portl0_co, portl1_co;

  HRnode r(
            .clk(clk),
            .rst(rst),
            
            .port0_i(flit0c), .port0_o(port0_co),
            .port1_i(flit1c), .port1_o(port1_co),
            .port0_local_i(flitl0), .port0_local_o(portl0_co),
            .port1_local_i(flitl1), .port1_local_o(portl1_co),

            .portl0_ack(ack0), .portl1_ack(ack1)
            );

  initial begin
//$set_toggle_region(tb.r);
//$toggle_start();

    clk = 0;
    rst = 0;
	flit0c = 144'h0123456789abcdef0123456789abcdef1851;
    flit1c = 144'h0;
    flitl1 = 144'h0;
    flitl0 = 144'h0;

#1;
clk = 1;
#1;
clk = 0;
	
       $display("clk = %d\n, port0 %04x\n, port1 %04x\n, portl0_co %04x\n, portl1_co %04x\n, portl0_ack %04x\n, portl1_ack %04x\n",
        clk, port0_co, port1_co, portl0_co, portl1_co, ack0, ack1);

//$toggle_stop();
//$toggle_report("./calf_backward_1.saif", 1.0e-9, "tb.r");
//$finish;

  end

endmodule
