`include "defines.v"
`include "connectRouter_2SNoBuffer.v"
`timescale 1ns/1ps


module tb_connectRouters(
);

  
  reg clk, rst;
  
  reg `control_w port_in, inj;
  
  reg bfull;
  
  wire `control_w port_out, eject;
  
  wire accept, push;
  

  connectRouter_nobuffer r(
            .clk(clk),
            .rst(rst),
            .port_in(port_in),
            .inj(inj),
            .port_out(port_out),
            .accept(accept),
            .bfull(bfull),
			.eject(eject),
			.push(push)
            );

  initial begin
//$set_toggle_region(tb.r);
//$toggle_start();

    clk = 0;
    rst = 0;
	bfull = 0;

    port_in = 144'h011111111111111111111111111111111854; // MSHR 1; valid / seq 0; source 5; dest 7
//	inj 	= 144'h0000000000000000000000000000000fffff;
	inj = 144'h0;

#1;
clk = 1;
#1;
clk = 0;
	port_in = 144'h0;
	inj 	= 144'h0000000000000000000000000000000fffff;

    //flit1c = 144'h0123456789abcdef0123456789abcdef1852;
    $display("clk = 0\n, port_out %04x\n, accept %04x\n, eject %04x\n, push %04x\n",
        port_out, accept, eject, push);
#1;
clk = 1;
#1;
clk = 0;
	inj = 144'h0;
    //flit1c = 144'h0123456789abcdef0123456789abcdef1852;
    $display("clk = 1\n, port_out %04x\n, accept %04x\n, eject %04x\n, push %04x\n",
        port_out, accept, eject, push);  

//$toggle_stop();
//$toggle_report("./calf_backward_1.saif", 1.0e-9, "tb.r");
//$finish;

  end

endmodule
