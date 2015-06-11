module router(clock,
              ID,

              // input links
              in_data_n, in_data_s, in_data_e, in_data_w,
              in_srcdst_n, in_srcdst_s, in_srcdst_e, in_srcdst_w,
              in_active_n, in_active_s, in_active_e, in_active_w,
              in_hops_n, in_hops_s, in_hops_e, in_hops_w,

              // output links
              out_data_n, out_data_s, out_data_e, out_data_w,
              out_srcdst_n, out_srcdst_s, out_srcdst_e, out_srcdst_w,
              out_active_n, out_active_s, out_active_e, out_active_w,
              out_hops_n, out_hops_s, out_hops_e, out_hops_w,

              // injection port
              in_data_inj, in_srcdst_inj, in_active_inj,
              in_accepted_inj,

              // ejection port
              out_data_ej, out_srcdst_ej, out_active_ej);

`include "config.vh"

  input clock;
  input [ADDRBITS2-1:0] ID;

  input [LINKWIDTH-1:0] in_data_n, in_data_s, in_data_e, in_data_w;
  input [ADDRBITS2-1:0] in_srcdst_n, in_srcdst_s, in_srcdst_e, in_srcdst_w;
  input in_active_n, in_active_s, in_active_e, in_active_w;
  input [HOPBITS-1:0] in_hops_n, in_hops_s, in_hops_e, in_hops_w;

  output reg [LINKWIDTH-1:0] out_data_n, out_data_s, out_data_e, out_data_w;
  output reg [ADDRBITS2-1:0] out_srcdst_n, out_srcdst_s, out_srcdst_e, out_srcdst_w;
  output reg out_active_n, out_active_s, out_active_e, out_active_w;
  output reg [HOPBITS-1:0] out_hops_n, out_hops_s, out_hops_e, out_hops_w;

  input [LINKWIDTH-1:0] in_data_inj;
  input [ADDRBITS2-1:0] in_srcdst_inj;
  input in_active_inj;
  output in_accepted_inj;

  output reg [LINKWIDTH-1:0] out_data_ej;
  output reg [ADDRBITS2-1:0] out_srcdst_ej;
  output reg out_active_ej;
  
  // --------------------------------------------------------------------
  // route computation
  wire [4:0] desired_n, desired_s, desired_e, desired_w, desired_i;
  wire [HOPBITS-1:0] hop_n, hop_s, hop_e, hop_w, hop_i;
  
  RC rc_n(.ID(ID), .active(in_active_n), .srcdst(in_srcdst_n), .desired(desired_n));
  RC rc_s(.ID(ID), .active(in_active_s), .srcdst(in_srcdst_s), .desired(desired_s));
  RC rc_e(.ID(ID), .active(in_active_e), .srcdst(in_srcdst_e), .desired(desired_e));
  RC rc_w(.ID(ID), .active(in_active_w), .srcdst(in_srcdst_w), .desired(desired_w));
  RC rc_i(.ID(ID), .active(in_active_inj), .srcdst(in_srcdst_inj), .desired(desired_i));

  INC inc_n(.in(in_hops_n), .out(hop_n));
  INC inc_s(.in(in_hops_s), .out(hop_s));
  INC inc_e(.in(in_hops_e), .out(hop_e));
  INC inc_w(.in(in_hops_w), .out(hop_w));
  assign in_hops_i = 0;

  // pipeline registers
  reg [4:0] desired_n_reg, desired_s_reg, desired_e_reg, desired_w_reg, desired_i_reg;
  reg [ADDRBITS2-1:0] srcdst_n_reg, srcdst_s_reg, srcdst_e_reg, srcdst_w_reg, srcdst_i_reg;
  reg [HOPBITS-1:0] hop_n_reg, hop_s_reg, hop_e_reg, hop_w_reg;
  always @(posedge clock) begin
    desired_n_reg <= desired_n;
    desired_s_reg <= desired_s;
    desired_e_reg <= desired_e;
    desired_w_reg <= desired_w;
    desired_i_reg <= desired_i;

    srcdst_n_reg <= in_srcdst_n;
    srcdst_s_reg <= in_srcdst_s;
    srcdst_e_reg <= in_srcdst_e;
    srcdst_w_reg <= in_srcdst_w;
    srcdst_i_reg <= in_srcdst_inj;

    hop_n_reg <= hop_n;
    hop_s_reg <= hop_s;
    hop_e_reg <= hop_e;
    hop_w_reg <= hop_w;
  end
  
  // --------------------------------------------------------------------
  // arbitration

  wire [4:0] ctln, ctls, ctle, ctlw, ctli;
  wire [HOPBITS-1:0] _out_hops_n, _out_hops_s, _out_hops_e, _out_hops_w;
  wire [ADDRBITS2-1:0] _out_srcdst_n, _out_srcdst_s, _out_srcdst_e, _out_srcdst_w;
  
  ARB arb(.n(desired_n_reg), .s(desired_s_reg), .e(desired_e_reg), .w(desired_w_reg), .i(desired_i_reg),
          .nh(hop_n_reg), .sh(hop_s_reg), .eh(hop_e_reg), .wh(hop_w_reg), .ih(0),
          .ctln(ctln), .ctls(ctls), .ctle(ctle), .ctlw(ctlw), .ctli(ctli));

  // early forwarding of srcdst/hops
  XB_RT xbrt(.ctln(ctln), .ctls(ctls), .ctle(ctle), .ctlw(ctlw), .ctli(ctli),
             .srcdst_n(srcdst_n_reg), .srcdst_s(srcdst_s_reg), .srcdst_e(srcdst_e_reg), .srcdst_w(srcdst_w_reg), .srcdst_i(srcdst_i_reg),
             .hop_n(hop_n_reg), .hop_s(hop_s_reg), .hop_e(hop_e_reg), .hop_w(hop_w_reg), .hop_i(0),
             
             .out_srcdst_n(_out_srcdst_n), .out_srcdst_s(_out_srcdst_s), .out_srcdst_e(_out_srcdst_e), .out_srcdst_w(_out_srcdst_w),
             .out_hop_n(_out_hops_n), .out_hop_s(_out_hops_s), .out_hop_e(_out_hops_e), .out_hop_w(_out_hops_w));

  // pipeline registers
  reg [4:0] ctln_reg, ctls_reg, ctle_reg, ctlw_reg, ctli_reg;
  reg [LINKWIDTH-1:0] data_n_reg, data_s_reg, data_e_reg, data_w_reg, data_i_reg;
  always @(posedge clock) begin
    out_srcdst_n <= _out_srcdst_n;
    out_srcdst_s <= _out_srcdst_s;
    out_srcdst_e <= _out_srcdst_e;
    out_srcdst_w <= _out_srcdst_w;
    
    out_hops_n <= _out_hops_n;
    out_hops_s <= _out_hops_s;
    out_hops_e <= _out_hops_e;
    out_hops_w <= _out_hops_w;

    ctln_reg <= ctln;
    ctls_reg <= ctls;
    ctle_reg <= ctle;
    ctlw_reg <= ctlw;
    ctli_reg <= ctli;

    data_n_reg <= in_data_n;
    data_s_reg <= in_data_s;
    data_e_reg <= in_data_e;
    data_w_reg <= in_data_w;
    data_i_reg <= in_data_inj;
  end
  
  // -------------------------------------------------------------------
  // crossbar traversal

  wire [LINKWIDTH-1:0] _out_data_n, _out_data_s, _out_data_e, _out_data_w, _out_data_ej;

  XB xb(.ctln(ctln_reg), .ctls(ctls_reg), .ctle(ctle_reg), .ctlw(ctlw_reg), .ctli(ctli_reg),
        .data_n(data_n_reg), .data_s(data_s_reg), .data_e(data_e_reg), .data_w(data_w_reg), .data_i(data_i_reg),
        .out_data_n(_out_data_n), .out_data_s(_out_data_s), .out_data_e(_out_data_e), .out_data_w(_out_data_w), .out_data_i(_out_data_ej));

  // pipeline regs
  always @(posedge clock) begin
    out_data_n <= _out_data_n;
    out_data_s <= _out_data_s;
    out_data_e <= _out_data_e;
    out_data_w <= _out_data_w;
    out_data_ej <= _out_data_ej;
  end
  
endmodule