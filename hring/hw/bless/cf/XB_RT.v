module XB_RT(ctln, ctls, ctle, ctlw, ctli,
             srcdst_n, srcdst_s, srcdst_e, srcdst_w, srcdst_i,
             hop_n, hop_s, hop_e, hop_w, hop_i,
             out_srcdst_n, out_srcdst_s, out_srcdst_e, out_srcdst_w, out_srcdst_i,
             out_hop_n, out_hop_s, out_hop_e, out_hop_w, out_hop_i);

`include "config.vh"
  
  input [4:0] ctln, ctls, ctle, ctlw, ctli;

  input [HOPBITS-1:0] hop_n, hop_s, hop_e, hop_w, hop_i;
  output [HOPBITS-1:0] out_hop_n, out_hop_s, out_hop_e, out_hop_w, out_hop_i;

  input [ADDRBITS2-1:0] srcdst_n, srcdst_s, srcdst_e, srcdst_w, srcdst_i;
  output [ADDRBITS2-1:0] out_srcdst_n, out_srcdst_s, out_srcdst_e, out_srcdst_w, out_srcdst_i;
  
endmodule