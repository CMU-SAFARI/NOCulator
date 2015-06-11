module INC(in, out);

`include "config.vh"

  input [HOPBITS-1:0] in;
  output [HOPBITS-1:0] out;

  assign out = in + 1;

endmodule