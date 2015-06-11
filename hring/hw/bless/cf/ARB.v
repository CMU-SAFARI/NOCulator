module ARB(n, s, e, w, i, nh, sh, eh, wh, ih, ctln, ctls, ctle, ctlw, ctli);

`include "config.vh"
  
  input [4:0] n, s, e, w, i;
  input [HOPBITS-1:0] nh, sh, eh, wh, ih;
  output [4:0] ctln, ctls, ctle, ctlw, ctli;

endmodule