module XB(ctln, ctls, ctle, ctlw, ctli,
          data_n, data_s, data_e, data_w, data_i,
          out_data_n, out_data_s, out_data_e, out_data_w, out_data_i);

`include "config.vh"

  input [4:0] ctln, ctls, ctle, ctlw, ctli;
  input [LINKWIDTH-1:0] data_n, data_s, data_e, data_w, data_i;
  output [LINKWIDTH-1:0] out_data_n, out_data_s, out_data_e, out_data_w, out_data_i;

endmodule