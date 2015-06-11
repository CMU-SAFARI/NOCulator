`include "defines.v"

module brouter_4x4(
    input   `control_w  b0000_ci,
    input   `control_w	b0001_ci,
    input   `control_w	b0010_ci,
    input   `control_w  b0011_ci,
    input   `control_w  b0100_ci,
    input   `control_w	b0101_ci,
    input   `control_w	b0110_ci,
    input   `control_w  b0111_ci,
    input   `control_w	b1000_ci,
    input   `control_w	b1001_ci,
    input   `control_w	b1010_ci,
    input   `control_w  b1011_ci,
    input   `control_w  b1100_ci,
    input   `control_w  b1101_ci,
    input   `control_w  b1110_ci,
    input   `control_w  b1111_ci,
    input   `data_w	    b0000_di,
    input   `data_w	    b0001_di,
    input   `data_w 	b0010_di,
    input   `data_w     b0011_di,
    input   `data_w	    b0100_di,
    input   `data_w	    b0101_di,
    input   `data_w	    b0110_di,
    input   `data_w     b0111_di,
    input   `data_w	    b1000_di,
    input   `data_w	    b1001_di,
    input   `data_w     b1010_di,
    input   `data_w     b1011_di,
    input   `data_w     b1100_di,
    input   `data_w     b1101_di,
    input   `data_w     b1110_di,
    input   `data_w     b1111_di,
    input               clk,
    input               rst,
    output  `control_w  b0000_co,
    output  `control_w	b0001_co,
    output  `control_w	b0010_co,
    output  `control_w  b0011_co,
    output  `control_w  b0100_co,
    output  `control_w	b0101_co,
    output  `control_w	b0110_co,
    output  `control_w  b0111_co,
    output  `control_w	b1000_co,
    output  `control_w	b1001_co,
    output  `control_w	b1010_co,
    output  `control_w  b1011_co,
    output  `control_w  b1100_co,
    output  `control_w  b1101_co,
    output  `control_w  b1110_co,
    output  `control_w  b1111_co,
    output  `data_w     b0000_do,
    output  `data_w     b0001_do,
    output  `data_w 	b0010_do,
    output  `data_w     b0011_do,
    output  `data_w     b0100_do,
    output  `data_w     b0101_do,
    output  `data_w     b0110_do,
    output  `data_w     b0111_do,
    output  `data_w     b1000_do,
    output  `data_w     b1001_do,
    output  `data_w     b1010_do,
    output  `data_w     b1011_do,
    output  `data_w     b1100_do,
    output  `data_w     b1101_do,
    output  `data_w     b1110_do,
    output  `data_w     b1111_do,
    output       	    b0000_r,
    output       	    b0001_r,
    output            	b0010_r,
    output              b0011_r,
    output       	    b0100_r,
    output       	    b0101_r,
    output       	    b0110_r,
    output              b0111_r,
    output       	    b1000_r,
    output       	    b1001_r,
    output       	    b1010_r,
    output              b1011_r,
    output              b1100_r,
    output              b1101_r,
    output              b1110_r,
    output              b1111_r);

    wire    `control_w  c01, c10, c12, c21, c23, c32, c03, c30,     // Rows
                        c45, c54, c56, c65, c67, c76, c47, c74,
                        c89, c98, c9a, ca9, cab, cba, cb8, c8b,
                        ccd, cdc, ced, cde, cef, cfe, ccf, cfc,
                        c04, c40, c48, c84, c8c, cc8, c0c, cc0,     // Cols
                        c15, c51, c59, c95, c9d, cd9, c1d, cd1,
                        c26, c62, c6a, ca6, cae, cea, ce2, c2e,
                        c37, c73, c7b, cb7, cbf, cfb, cf3, c3f;

    wire    `data_w     d01, d10, d12, d21, d23, d32, d03, d30,     // Rows
                        d45, d54, d56, d65, d67, d76, d47, d74,
                        d89, d98, d9a, da9, dab, dba, db8, d8b,
                        dcd, ddc, ded, dde, def, dfe, dcf, dfc,
                        d04, d40, d48, d84, d8c, dc8, d0c, dc0,     // Cols
                        d15, d51, d59, d95, d9d, dd9, d1d, dd1,
                        d26, d62, d6a, da6, dae, dea, de2, d2e,
                        d37, d73, d7b, db7, dbf, dfb, df3, d3f;

    

    brouter #(4'b0000) br0000
                   (.port0_ci(c30),
                    .port0_di(d30),
                    .port0_co(c03),
                    .port0_do(d03),
                    .port1_ci(c10),
                    .port1_di(d10),
                    .port1_co(c01),
                    .port1_do(d01),
                    .port2_ci(c40),
                    .port2_di(d40),
                    .port2_co(c04),
                    .port2_do(d04),
                    .port3_ci(cc0),
                    .port3_di(dc0),
                    .port3_co(c0c),
                    .port3_do(d0c),
                    .port4_ci(b0000_ci),
                    .port4_di(b0000_di),
                    .port4_co(b0000_co),
                    .port4_do(b0000_do),
                    .port4_ready(b0000_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b0001) br0001
                   (.port0_ci(c01),
                    .port0_di(d01),
                    .port0_co(c10),
                    .port0_do(d10),
                    .port1_ci(c21),
                    .port1_di(d21),
                    .port1_co(c12),
                    .port1_do(d12),
                    .port2_ci(c51),
                    .port2_di(d51),
                    .port2_co(c15),
                    .port2_do(d15),
                    .port3_ci(cd1),
                    .port3_di(dd1),
                    .port3_co(c1d),
                    .port3_do(d1d),
                    .port4_ci(b0001_ci),
                    .port4_di(b0001_di),
                    .port4_co(b0001_co),
                    .port4_do(b0001_do),
                    .port4_ready(b0001_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b0010) br0010 
                   (.port0_ci(c12),
                    .port0_di(d12),
                    .port0_co(c21),
                    .port0_do(d21),
                    .port1_ci(c32),
                    .port1_di(d32),
                    .port1_co(c23),
                    .port1_do(d23),
                    .port2_ci(c62),
                    .port2_di(d62),
                    .port2_co(c26),
                    .port2_do(d26),
                    .port3_ci(ce2),
                    .port3_di(de2),
                    .port3_co(c2e),
                    .port3_do(d2e),
                    .port4_ci(b0010_ci),
                    .port4_di(b0010_di),
                    .port4_co(b0010_co),
                    .port4_do(b0010_do),
                    .port4_ready(b0010_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b0011) br0011
                   (.port0_ci(c23),
                    .port0_di(d23),
                    .port0_co(c32),
                    .port0_do(d32),
                    .port1_ci(c03),
                    .port1_di(d03),
                    .port1_co(c30),
                    .port1_do(d30),
                    .port2_ci(c73),
                    .port2_di(d73),
                    .port2_co(c37),
                    .port2_do(d37),
                    .port3_ci(cf3),
                    .port3_di(df3),
                    .port3_co(c3f),
                    .port3_do(d3f),
                    .port4_ci(b0011_ci),
                    .port4_di(b0011_di),
                    .port4_co(b0011_co),
                    .port4_do(b0011_do),
                    .port4_ready(b0011_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b0100) br0100
                   (.port0_ci(c74),
                    .port0_di(d74),
                    .port0_co(c47),
                    .port0_do(d47),
                    .port1_ci(c54),
                    .port1_di(d54),
                    .port1_co(c45),
                    .port1_do(d45),
                    .port2_ci(c84),
                    .port2_di(d84),
                    .port2_co(c48),
                    .port2_do(d48),
                    .port3_ci(c04),
                    .port3_di(d04),
                    .port3_co(c40),
                    .port3_do(d40),
                    .port4_ci(b0100_ci),
                    .port4_di(b0100_di),
                    .port4_co(b0100_co),
                    .port4_do(b0100_do),
                    .port4_ready(b0100_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b0101) br0101 
                   (.port0_ci(c45),
                    .port0_di(d45),
                    .port0_co(c54),
                    .port0_do(d54),
                    .port1_ci(c65),
                    .port1_di(d65),
                    .port1_co(c56),
                    .port1_do(d56),
                    .port2_ci(c95),
                    .port2_di(d95),
                    .port2_co(c59),
                    .port2_do(d59),
                    .port3_ci(c15),
                    .port3_di(d15),
                    .port3_co(c51),
                    .port3_do(d51),
                    .port4_ci(b0101_ci),
                    .port4_di(b0101_di),
                    .port4_co(b0101_co),
                    .port4_do(b0101_do),
                    .port4_ready(b0101_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b0110) br0110
                   (.port0_ci(c56),
                    .port0_di(d56),
                    .port0_co(c65),
                    .port0_do(d65),
                    .port1_ci(c76),
                    .port1_di(d76),
                    .port1_co(c67),
                    .port1_do(d67),
                    .port2_ci(ca6),
                    .port2_di(da6),
                    .port2_co(c6a),
                    .port2_do(d6a),
                    .port3_ci(c26),
                    .port3_di(d26),
                    .port3_co(c62),
                    .port3_do(d62),
                    .port4_ci(b0110_ci),
                    .port4_di(b0110_di),
                    .port4_co(b0110_co),
                    .port4_do(b0110_do),
                    .port4_ready(b0110_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b0111) br0111
                   (.port0_ci(c67),
                    .port0_di(d67),
                    .port0_co(c76),
                    .port0_do(d76),
                    .port1_ci(c47),
                    .port1_di(d47),
                    .port1_co(c74),
                    .port1_do(d74),
                    .port2_ci(cb7),
                    .port2_di(db7),
                    .port2_co(c7b),
                    .port2_do(d7b),
                    .port3_ci(c37),
                    .port3_di(d37),
                    .port3_co(c73),
                    .port3_do(d73),
                    .port4_ci(b0111_ci),
                    .port4_di(b0111_di),
                    .port4_co(b0111_co),
                    .port4_do(b0111_do),
                    .port4_ready(b0111_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b1000) br1000 
                   (.port0_ci(cb8),
                    .port0_di(db8),
                    .port0_co(c8b),
                    .port0_do(d8b),
                    .port1_ci(c98),
                    .port1_di(d98),
                    .port1_co(c89),
                    .port1_do(d89),
                    .port2_ci(cc8),
                    .port2_di(dc8),
                    .port2_co(c8c),
                    .port2_do(d8c),
                    .port3_ci(c48),
                    .port3_di(d48),
                    .port3_co(c84),
                    .port3_do(d84),
                    .port4_ci(b1000_ci),
                    .port4_di(b1000_di),
                    .port4_co(b1000_co),
                    .port4_do(b1000_do),
                    .port4_ready(b1000_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b1001) br1001 
                   (.port0_ci(c89),
                    .port0_di(d89),
                    .port0_co(c98),
                    .port0_do(d98),
                    .port1_ci(ca9),
                    .port1_di(da9),
                    .port1_co(c9a),
                    .port1_do(d9a),
                    .port2_ci(cd9),
                    .port2_di(dd9),
                    .port2_co(c9d),
                    .port2_do(d9d),
                    .port3_ci(c59),
                    .port3_di(d59),
                    .port3_co(c95),
                    .port3_do(d95),
                    .port4_ci(b1001_ci),
                    .port4_di(b1001_di),
                    .port4_co(b1001_co),
                    .port4_do(b1001_do),
                    .port4_ready(b1001_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b1010) br1010 
                   (.port0_ci(c9a),
                    .port0_di(d9a),
                    .port0_co(ca9),
                    .port0_do(da9),
                    .port1_ci(cba),
                    .port1_di(dba),
                    .port1_co(cab),
                    .port1_do(dab),
                    .port2_ci(cea),
                    .port2_di(dea),
                    .port2_co(cae),
                    .port2_do(dae),
                    .port3_ci(c6a),
                    .port3_di(d6a),
                    .port3_co(ca6),
                    .port3_do(da6),
                    .port4_ci(b1010_ci),
                    .port4_di(b1010_di),
                    .port4_co(b1010_co),
                    .port4_do(b1010_do),
                    .port4_ready(b1010_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b1011) br1011
                   (.port0_ci(cab),
                    .port0_di(dab),
                    .port0_co(cba),
                    .port0_do(dba),
                    .port1_ci(c8b),
                    .port1_di(d8b),
                    .port1_co(cb8),
                    .port1_do(db8),
                    .port2_ci(cfb),
                    .port2_di(dfb),
                    .port2_co(cbf),
                    .port2_do(dbf),
                    .port3_ci(c7b),
                    .port3_di(d7b),
                    .port3_co(cb7),
                    .port3_do(db7),
                    .port4_ci(b1011_ci),
                    .port4_di(b1011_di),
                    .port4_co(b1011_co),
                    .port4_do(b1011_do),
                    .port4_ready(b1011_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b1100) br1100
                   (.port0_ci(cfc),
                    .port0_di(dfc),
                    .port0_co(ccf),
                    .port0_do(dcf),
                    .port1_ci(cdc),
                    .port1_di(ddc),
                    .port1_co(ccd),
                    .port1_do(dcd),
                    .port2_ci(c0c),
                    .port2_di(d0c),
                    .port2_co(cc0),
                    .port2_do(dc0),
                    .port3_ci(c8c),
                    .port3_di(d8c),
                    .port3_co(cc8),
                    .port3_do(dc8),
                    .port4_ci(b1100_ci),
                    .port4_di(b1100_di),
                    .port4_co(b1100_co),
                    .port4_do(b1100_do),
                    .port4_ready(b1100_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b1101) br1101
                   (.port0_ci(ccd),
                    .port0_di(dcd),
                    .port0_co(cdc),
                    .port0_do(ddc),
                    .port1_ci(ced),
                    .port1_di(ded),
                    .port1_co(cde),
                    .port1_do(dde),
                    .port2_ci(c1d),
                    .port2_di(d1d),
                    .port2_co(cd1),
                    .port2_do(dd1),
                    .port3_ci(c9d),
                    .port3_di(d9d),
                    .port3_co(cd9),
                    .port3_do(dd9),
                    .port4_ci(b1101_ci),
                    .port4_di(b1101_di),
                    .port4_co(b1101_co),
                    .port4_do(b1101_do),
                    .port4_ready(b1101_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b1110) br1110
                   (.port0_ci(cde),
                    .port0_di(dde),
                    .port0_co(ced),
                    .port0_do(ded),
                    .port1_ci(cfe),
                    .port1_di(dfe),
                    .port1_co(cef),
                    .port1_do(def),
                    .port2_ci(c2e),
                    .port2_di(d2e),
                    .port2_co(ce2),
                    .port2_do(de2),
                    .port3_ci(cae),
                    .port3_di(dae),
                    .port3_co(cea),
                    .port3_do(dea),
                    .port4_ci(b1110_ci),
                    .port4_di(b1110_di),
                    .port4_co(b1110_co),
                    .port4_do(b1110_do),
                    .port4_ready(b1110_r),
                    .clk(clk),
                    .rst(rst));
    brouter #(4'b1111) br1111
                   (.port0_ci(cef),
                    .port0_di(def),
                    .port0_co(cfe),
                    .port0_do(dfe),
                    .port1_ci(ccf),
                    .port1_di(dcf),
                    .port1_co(cfc),
                    .port1_do(dfc),
                    .port2_ci(c3f),
                    .port2_di(d3f),
                    .port2_co(cf3),
                    .port2_do(df3),
                    .port3_ci(cbf),
                    .port3_di(dbf),
                    .port3_co(cfb),
                    .port3_do(dfb),
                    .port4_ci(b1111_ci),
                    .port4_di(b1111_di),
                    .port4_co(b1111_co),
                    .port4_do(b1111_do),
                    .port4_ready(b1111_r),
                    .clk(clk),
                    .rst(rst));

endmodule