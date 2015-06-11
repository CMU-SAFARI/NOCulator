`include "brouter.v"
`include "defines.v"

module test_brouter_2x2();
    wire `control_w port01_c, port10_c, port02_c, port20_c,
                    port13_c, port31_c, port23_c, port32_c,
                    port01t_c, port10t_c, port02t_c, port20t_c,
                    port13t_c, port31t_c, port23t_c, port32t_c,
                    port0r_c, port1r_c, port2r_c, port3r_c;
                    
    reg `control_w  portr0_c, portr1_c, portr2_c, portr3_c;
                    
    wire `data_w    port01_d, port10_d, port02_d, port20_d,
                    port13_d, port31_d, port23_d, port32_d,
                    port01t_d, port10t_d, port02t_d, port20t_d,
                    port13t_d, port31t_d, port23t_d, port32t_d,
                    port0r_d, port1r_d, port2r_d, port3r_d;
                    
    reg `data_w     portr0_d, portr1_d, portr2_d, portr3_d;

    wire `routecfg_w    rm0, rm1, rm2, rm3, rm4;
    wire vrm4, vrm3, vrm2, vrm1, vrm0;
    wire `amatrix_w am0;
    wire `amatrix_w am1;
    wire `amatrix_w am2;
    wire `amatrix_w am3;
    wire `amatrix_w am4;
    wire [3:0] p0, p1, p2, p3;

    assign vrm4 = br10.port4_ci[`valid_f];
    assign vrm3 = br10.port3_ci[`valid_f];
    assign vrm2 = br10.port2_ci[`valid_f];
    assign vrm1 = br10.port1_ci[`valid_f];
    assign vrm0 = br10.port0_ci[`valid_f];
    assign rm0 = br10.rmatrix0;
    assign rm1 = br10.rmatrix1;
    assign rm2 = br10.rmatrix2;
    assign rm3 = br10.rmatrix3;
    assign rm4 = br10.rmatrix4;
    assign am0 = br10.arb.amatrix1;
    assign am1 = br10.arb.amatrix2;
    assign am2 = br10.arb.amatrix3;
    assign am3 = br10.arb.amatrix4;
    assign am4 = br10.arb.amatrix5;
    assign p0 = br10.arb.p0;
    assign p1 = br10.arb.p1;
    assign p2 = br10.arb.p2;
    assign p3 = br10.arb.p3;

    reg rst; 
    reg clk;

    //wire `control_w port0_co, port1_co, port2_co, port3_co, port4_co;
    //wire `data_w port0_do, port1_do, port2_do, port3_do, port4_do;

    wire `routecfg_w rc3;
    wire `routecfg_w rc2;
    wire `routecfg_w rc1;
    wire `routecfg_w rc0;
    

    assign rc3 = br11.route_config;
    assign rc2 = br10.route_config;
    assign rc1 = br01.route_config;
    assign rc0 = br00.route_config;
    //reg `rmatrix_w  port0_r2, port1_r2, port2_r2, port3_r2, port4_r2;
    //reg `rmatrix_w  port0_r3, port1_r3, port2_r3, port3_r3, port4_r3;

    brouter #(2'b00) br00
             (port10t_c, port10_c, port20_c, port20t_c, portr0_c,
              port10t_d, port10_d, port20_d, port20t_d, portr0_d, clk, rst,
              port01t_c, port01_c, port02_c, port02t_c, port0r_c,
              port01t_d, port01_d, port02_d, port02t_d, port0r_d);

    brouter #(2'b01) br01
             (port01_c, port01t_c, port31_c, port31t_c, portr1_c,
              port01_d, port01t_d, port31_d, port31t_d, portr1_d, clk, rst,
              port10_c, port10t_c, port13_c, port13t_c, port1r_c,
              port10_d, port10t_d, port13_d, port13t_d, port1r_d);

    brouter #(2'b10) br10
             (port32t_c, port32_c, port02t_c, port02_c, portr2_c,
              port32t_d, port32_d, port02t_d, port02_d, portr2_d, clk, rst,
              port23t_c, port23_c, port20t_c, port20_c, port2r_c,
              port23t_d, port23_d, port20t_d, port20_d, port2r_d);
    
    brouter #(2'b11) br11
             (port23_c, port23t_c, port13t_c, port13_c, portr3_c,
              port23_d, port23t_d, port13t_d, port13_d, portr3_d, clk, rst,
              port32_c, port32t_c, port31t_c, port31_c, port3r_c,
              port32_d, port32t_d, port31t_d, port31_d, port3r_d);
    always begin
        #10;
        clk = ~clk;
    end

    always @(posedge clk) begin
        /*
        $display("   IN: |V|SEQ|SRC |DEST|AGE |  DATA  |");
        $display("Port0: |%b|%b|%b|%b|%b|%b|", port0_ci3[`valid_f], 
                 port0_ci3[`seq_f], port0_ci3[`src_f], port0_ci3[`dest_f], 
                 port0_ci3[`age_f], port0_di3);
        $display("Port1: |%b|%b|%b|%b|%b|%b|", port1_ci3[`valid_f], 
                 port1_ci3[`seq_f], port1_ci3[`src_f], port1_ci3[`dest_f], 
                 port1_ci3[`age_f], port1_di3);
        $display("Port2: |%b|%b|%b|%b|%b|%b|", port2_ci3[`valid_f], 
                 port2_ci3[`seq_f], port2_ci3[`src_f], port2_ci3[`dest_f], 
                 port2_ci3[`age_f], port2_di3);
        $display("Port3: |%b|%b|%b|%b|%b|%b|", port3_ci3[`valid_f], 
                 port3_ci3[`seq_f], port3_ci3[`src_f], port3_ci3[`dest_f], 
                 port3_ci3[`age_f], port3_di3);
        $display("Port4: |%b|%b|%b|%b|%b|%b|", port4_ci3[`valid_f], 
                 port4_ci3[`seq_f], port4_ci3[`src_f], port4_ci3[`dest_f], 
                 port4_ci3[`age_f], port4_di3);
        */
        $display("BR00------------------------------");
        $display("  OUT: |V|SEQ|SRC |DEST|AGE |  DATA  |");
        $display("Port0: |%b|%b|%b|%b|%b|%b|", port01t_c[`valid_f], 
                 port01t_c[`seq_f], port01t_c[`src_f], port01t_c[`dest_f], 
                 port01t_c[`age_f], port01t_d);
        $display("Port1: |%b|%b|%b|%b|%b|%b|", port01_c[`valid_f], 
                 port01_c[`seq_f], port01_c[`src_f], port01_c[`dest_f], 
                 port01_c[`age_f], port01_d);
        $display("Port2: |%b|%b|%b|%b|%b|%b|", port02_c[`valid_f], 
                 port02_c[`seq_f], port02_c[`src_f], port02_c[`dest_f], 
                 port02_c[`age_f], port02_d);
        $display("Port3: |%b|%b|%b|%b|%b|%b|", port02t_c[`valid_f], 
                 port02t_c[`seq_f], port02t_c[`src_f], port02t_c[`dest_f], 
                 port02t_c[`age_f], port02t_d);
        $display("Port4: |%b|%b|%b|%b|%b|%b|", port0r_c[`valid_f], 
                 port0r_c[`seq_f], port0r_c[`src_f], port0r_c[`dest_f], 
                 port0r_c[`age_f], port0r_d); 
        $display("BR01------------------------------");
        $display("  OUT: |V|SEQ|SRC |DEST|AGE |  DATA  |");
        $display("Port0: |%b|%b|%b|%b|%b|%b|", port10_c[`valid_f], 
                 port10_c[`seq_f], port10_c[`src_f], port10_c[`dest_f], 
                 port10_c[`age_f], port10_d);
        $display("Port1: |%b|%b|%b|%b|%b|%b|", port10t_c[`valid_f], 
                 port10t_c[`seq_f], port10t_c[`src_f], port10t_c[`dest_f], 
                 port10t_c[`age_f], port10t_d);
        $display("Port2: |%b|%b|%b|%b|%b|%b|", port13_c[`valid_f], 
                 port13_c[`seq_f], port13_c[`src_f], port13_c[`dest_f], 
                 port13_c[`age_f], port13_d);
        $display("Port3: |%b|%b|%b|%b|%b|%b|", port13t_c[`valid_f], 
                 port13t_c[`seq_f], port13t_c[`src_f], port13t_c[`dest_f], 
                 port13t_c[`age_f], port13t_d);
        $display("Port4: |%b|%b|%b|%b|%b|%b|", port1r_c[`valid_f], 
                 port1r_c[`seq_f], port1r_c[`src_f], port1r_c[`dest_f], 
                 port1r_c[`age_f], port1r_d); 
        $display("BR10------------------------------");
        $display("  OUT: |V|SEQ|SRC |DEST|AGE |  DATA  |");
        $display("Port0: |%b|%b|%b|%b|%b|%b|", port23t_c[`valid_f], 
                 port23t_c[`seq_f], port23t_c[`src_f], port23t_c[`dest_f], 
                 port23t_c[`age_f], port23t_d);
        $display("Port1: |%b|%b|%b|%b|%b|%b|", port23_c[`valid_f], 
                 port23_c[`seq_f], port23_c[`src_f], port23_c[`dest_f], 
                 port23_c[`age_f], port23_d);
        $display("Port2: |%b|%b|%b|%b|%b|%b|", port20t_c[`valid_f], 
                 port20t_c[`seq_f], port20t_c[`src_f], port20t_c[`dest_f], 
                 port20t_c[`age_f], port20t_d);
        $display("Port3: |%b|%b|%b|%b|%b|%b|", port20_c[`valid_f], 
                 port20_c[`seq_f], port20_c[`src_f], port20_c[`dest_f], 
                 port20_c[`age_f], port20_d);
        $display("Port4: |%b|%b|%b|%b|%b|%b|", port2r_c[`valid_f], 
                 port2r_c[`seq_f], port2r_c[`src_f], port2r_c[`dest_f], 
                 port2r_c[`age_f], port2r_d); 
        $display("BR11------------------------------");
        $display("  OUT: |V|SEQ|SRC |DEST|AGE |  DATA  |");
        $display("Port0: |%b|%b|%b|%b|%b|%b|", port32_c[`valid_f], 
                 port32_c[`seq_f], port32_c[`src_f], port32_c[`dest_f], 
                 port32_c[`age_f], port32_d);
        $display("Port1: |%b|%b|%b|%b|%b|%b|", port32t_c[`valid_f], 
                 port32t_c[`seq_f], port32t_c[`src_f], port32t_c[`dest_f], 
                 port32t_c[`age_f], port32t_d);
        $display("Port2: |%b|%b|%b|%b|%b|%b|", port31t_c[`valid_f], 
                 port31t_c[`seq_f], port31t_c[`src_f], port31t_c[`dest_f], 
                 port31t_c[`age_f], port31t_d);
        $display("Port3: |%b|%b|%b|%b|%b|%b|", port31_c[`valid_f], 
                 port31_c[`seq_f], port31_c[`src_f], port31_c[`dest_f], 
                 port31_c[`age_f], port31_d);
        $display("Port4: |%b|%b|%b|%b|%b|%b|", port3r_c[`valid_f], 
                 port3r_c[`seq_f], port3r_c[`src_f], port3r_c[`dest_f], 
                 port3r_c[`age_f], port3r_d); 
        
        $display("Route Comp 00: |%b|%b|%b|%b|%b|", rc0[`routecfg_4],
                 rc0[`routecfg_3], rc0[`routecfg_2], rc0[`routecfg_1], 
                 rc0[`routecfg_0]);
        $display("Route Comp 01: |%b|%b|%b|%b|%b|", rc1[`routecfg_4],
                 rc1[`routecfg_3], rc1[`routecfg_2], rc1[`routecfg_1], 
                 rc1[`routecfg_0]);
        $display("Route Comp 10: |%b|%b|%b|%b|%b|", rc2[`routecfg_4],
                 rc2[`routecfg_3], rc2[`routecfg_2], rc2[`routecfg_1], 
                 rc2[`routecfg_0]);
        $display("Route Comp 11: |%b|%b|%b|%b|%b|", rc3[`routecfg_4],
                 rc3[`routecfg_3], rc3[`routecfg_2], rc3[`routecfg_1], 
                 rc3[`routecfg_0]);
        /*
        $display("         |V|N|S|E|W|R|");
        $display("rmatrix0 |%b|%b|%b|%b|%b|%b|", vrm0, 
                 rm0[0], rm0[1], rm0[2], rm0[3], ~|rm0);
        $display("rmatrix1 |%b|%b|%b|%b|%b|%b|", vrm1, 
                 rm1[0], rm1[1], rm1[2], rm1[3], ~|rm1);
        $display("rmatrix2 |%b|%b|%b|%b|%b|%b|", vrm2, 
                 rm2[0], rm2[1], rm2[2], rm2[3], ~|rm2);
        $display("rmatrix3 |%b|%b|%b|%b|%b|%b|", vrm3, 
                 rm3[0], rm3[1], rm3[2], rm3[3], ~|rm3);
        $display("rmatrix4 |%b|%b|%b|%b|%b|%b|", vrm4, 
                 rm4[0], rm4[1], rm4[2], rm4[3], ~|rm4);
        $display("amatrix1   |%b|%b|%b|%b|%b|", 
                 am0[0], am0[1], am0[2], am0[3], am0[4]);
        $display("amatrix2   |%b|%b|%b|%b|%b|", 
                 am1[0], am1[1], am1[2], am1[3], am1[4]);
        $display("amatrix3   |%b|%b|%b|%b|%b|", 
                 am2[0], am2[1], am2[2], am2[3], am2[4]);
        $display("amatrix4   |%b|%b|%b|%b|%b|", 
                 am3[0], am3[1], am3[2], am3[3], am3[4]);
        $display("amatrix5   |%b|%b|%b|%b|%b|", 
                 am4[0], am4[1], am4[2], am4[3], am4[4]);
        $display("p0: %b", p0);        
        $display("p1: %b", p1);        
        $display("p2: %b", p2);        
        $display("p3: %b", p3);        
        */
        $display("");
    end

    always @(posedge clk) begin
        /* 
        // Route Configuration
        rc3 <= br.arb.route_config;
        
        // Routing Matrix
        port0_r3 <= port0_r2;
        port1_r3 <= port1_r2;
        port2_r3 <= port2_r2;
        port3_r3 <= port3_r2;
        port4_r3 <= port4_r2;

        port0_r2 <= br.arb.rmatrix0;
        port1_r2 <= br.arb.rmatrix1;
        port2_r2 <= br.arb.rmatrix2;
        port3_r2 <= br.arb.rmatrix3;
        port4_r2 <= br.arb.rmatrix4;

        // Control
        port0_ci3 <= port0_ci2;
        port1_ci3 <= port1_ci2;
        port2_ci3 <= port2_ci2;
        port3_ci3 <= port3_ci2;
        port4_ci3 <= port4_ci2;
        
        port0_ci2 <= port0_ci1;
        port1_ci2 <= port1_ci1;
        port2_ci2 <= port2_ci1;
        port3_ci2 <= port3_ci1;
        port4_ci2 <= port4_ci1;

        port0_ci1 <= port0_ci;
        port1_ci1 <= port1_ci;
        port2_ci1 <= port2_ci;
        port3_ci1 <= port3_ci;
        port4_ci1 <= port4_ci;
        
        // Data
        port0_di3 <= port0_di2;
        port1_di3 <= port1_di2;
        port2_di3 <= port2_di2;
        port3_di3 <= port3_di2;
        port4_di3 <= port4_di2;
        
        port0_di2 <= port0_di1;
        port1_di2 <= port1_di1;
        port2_di2 <= port2_di1;
        port3_di2 <= port3_di1;
        port4_di2 <= port4_di1;

        port0_di1 <= port0_di;
        port1_di1 <= port1_di;
        port2_di1 <= port2_di;
        port3_di1 <= port3_di;
        port4_di1 <= port4_di;
        */
    end

    initial begin
        clk = 0;
        rst = 1;
        portr0_c = {1'b1, `seq_n'd3, `addr_n'd0, `addr_n'd1, `age_n'd0};
        portr0_d = `data_n'd0;
        portr1_c = {1'b0, `seq_n'd1, `addr_n'd1, `addr_n'd2, `age_n'd0};
        portr1_d = `data_n'd1;
        portr2_c = {1'b0, `seq_n'd3, `addr_n'd2, `addr_n'd0, `age_n'd0};
        portr2_d = `data_n'd2; 
        portr3_c = {1'b1, `seq_n'd3, `addr_n'd3, `addr_n'd1, `age_n'd0};
        portr3_d = `data_n'd3;

        @(negedge clk);
        rst = 0;
        
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);


        $finish;
    end

endmodule
*/
