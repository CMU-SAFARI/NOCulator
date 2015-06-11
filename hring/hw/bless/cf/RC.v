

module RC(ID, active, srcdst, desired);

`include "config.vh"

  input[ADDRBITS2-1:0] ID, srcdst;
  input active;
  output [4:0] desired;

  assign desired[0] = active && (srcdst[3:2] > ID[3:2]);
  assign desired[1] = active && (srcdst[3:2] < ID[3:2]);
  assign desired[2] = active && (srcdst[1:0] > ID[1:0]);
  assign desired[3] = active && (srcdst[1:0] < ID[1:0]);
  assign desired[4] = active && (srcdst == ID);

endmodule