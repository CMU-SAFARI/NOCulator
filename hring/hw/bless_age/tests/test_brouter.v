`include "brouter.v"
`include "defines.v"


module test_brouter();
    reg `control_w port0_ci, port1_ci, port2_ci, port3_ci, port4_ci;
    reg `control_w port0_ci1, port1_ci1, port2_ci1, port3_ci1, port4_ci1;
    reg `control_w port0_ci2, port1_ci2, port2_ci2, port3_ci2, port4_ci2;
    reg `control_w port0_ci3, port1_ci3, port2_ci3, port3_ci3, port4_ci3;
    reg `data_w port0_di, port1_di, port2_di, port3_di, port4_di;
    reg `data_w port0_di1, port1_di1, port2_di1, port3_di1, port4_di1;
    reg `data_w port0_di2, port1_di2, port2_di2, port3_di2, port4_di2;
    reg `data_w port0_di3, port1_di3, port2_di3, port3_di3, port4_di3;
    reg clk, rst;

    wire `control_w port0_co, port1_co, port2_co, port3_co, port4_co;
    wire `data_w port0_do, port1_do, port2_do, port3_do, port4_do;
    wire port4_ready;

    reg `routecfg_w rc3;
    reg `rmatrix_w  port0_r2, port1_r2, port2_r2, port3_r2, port4_r2;
    reg `rmatrix_w  port0_r3, port1_r3, port2_r3, port3_r3, port4_r3;
    reg port4_ready_r1, port4_ready_r2, port4_ready_r3;
    reg all_valid_r1, all_valid_r2, all_valid_r3;
    
    brouter br(port0_ci, port1_ci, port2_ci, port3_ci, port4_ci,
               port0_di, port1_di, port2_di, port3_di, port4_di, clk, rst,
               port0_co, port1_co, port2_co, port3_co, port4_co,
               port0_do, port1_do, port2_do, port3_do, port4_do, port4_ready);

    always begin
        #10;
        clk = ~clk;
    end

    always @(posedge clk) begin
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
        $display("----------------------------------");
        $display("  OUT: |V|SEQ|SRC |DEST|AGE |  DATA  |");
        $display("Port0: |%b|%b|%b|%b|%b|%b|", port0_co[`valid_f], 
                 port0_co[`seq_f], port0_co[`src_f], port0_co[`dest_f], 
                 port0_co[`age_f], port0_do);
        $display("Port1: |%b|%b|%b|%b|%b|%b|", port1_co[`valid_f], 
                 port1_co[`seq_f], port1_co[`src_f], port1_co[`dest_f], 
                 port1_co[`age_f], port1_do);
        $display("Port2: |%b|%b|%b|%b|%b|%b|", port2_co[`valid_f], 
                 port2_co[`seq_f], port2_co[`src_f], port2_co[`dest_f], 
                 port2_co[`age_f], port2_do);
        $display("Port3: |%b|%b|%b|%b|%b|%b|", port3_co[`valid_f], 
                 port3_co[`seq_f], port3_co[`src_f], port3_co[`dest_f], 
                 port3_co[`age_f], port3_do);
        $display("Port4: |%b|%b|%b|%b|%b|%b|", port4_co[`valid_f], 
                 port4_co[`seq_f], port4_co[`src_f], port4_co[`dest_f], 
                 port4_co[`age_f], port4_do); 
        $display("Route Compute: |%b|%b|%b|%b|%b|", rc3[`routecfg_4],
                 rc3[`routecfg_3], rc3[`routecfg_2], rc3[`routecfg_1], 
                 rc3[`routecfg_0]);
        $display("         |V|N|S|E|W|R|");
        $display("rmatrix0 |%b|%b|%b|%b|%b|%b|", port0_ci3[`valid_f], 
                 port0_r3[0], port0_r3[1], port0_r3[2], port0_r3[3], ~|port0_r3);
        $display("rmatrix1 |%b|%b|%b|%b|%b|%b|", port1_ci3[`valid_f], 
                 port1_r3[0], port1_r3[1], port1_r3[2], port1_r3[3], ~|port1_r3);
        $display("rmatrix2 |%b|%b|%b|%b|%b|%b|", port2_ci3[`valid_f], 
                 port2_r3[0], port2_r3[1], port2_r3[2], port2_r3[3], ~|port2_r3);
        $display("rmatrix3 |%b|%b|%b|%b|%b|%b|", port3_ci3[`valid_f], 
                 port3_r3[0], port3_r3[1], port3_r3[2], port3_r3[3], ~|port3_r3);
        $display("rmatrix4 |%b|%b|%b|%b|%b|%b|", port4_ci3[`valid_f], 
                 port4_r3[0], port4_r3[1], port4_r3[2], port4_r3[3], ~|port4_r3);
        $display("Ready: %b", port4_ready_r3);
        $display("All valid: %b", all_valid_r3);
        $display("");
    end

    always @(posedge clk) begin
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
        port4_ready_r3 <= port4_ready_r2;
        all_valid_r3 <= all_valid_r2;
        
        port0_ci2 <= port0_ci1;
        port1_ci2 <= port1_ci1;
        port2_ci2 <= port2_ci1;
        port3_ci2 <= port3_ci1;
        port4_ci2 <= port4_ci1;
        port4_ready_r2 <= port4_ready_r1;
        all_valid_r2 <= all_valid_r1;
        
        port0_ci1 <= port0_ci;
        port1_ci1 <= port1_ci;
        port2_ci1 <= port2_ci;
        port3_ci1 <= port3_ci;
        port4_ci1 <= port4_ci;
        port4_ready_r1 <= port4_ready;
        all_valid_r1 <= br.all_valid;
        
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
    end

    initial begin
        clk = 0;
        rst = 0;
        port0_ci = {1'b1, `seq_n'd3, `addr_n'd0, `addr_n'b0001, `age_n'd0};
        port0_di = `data_n'h00;
        port1_ci = {1'b1, `seq_n'd3, `addr_n'd1, `addr_n'b0100, `age_n'd0};
        port1_di = `data_n'h01;
        port2_ci = {1'b1, `seq_n'd3, `addr_n'd2, `addr_n'b0011, `age_n'd0};
        port2_di = `data_n'h02; 
        port3_ci = {1'b1, `seq_n'd3, `addr_n'd3, `addr_n'b1100, `age_n'd0};
        port3_di = `data_n'h03;
        port4_ci = {1'b1, `seq_n'd3, `addr_n'd4, `addr_n'h1, `age_n'd0};
        port4_di = `data_n'h04;

        @(negedge clk);
        
        port0_ci = {1'b1, `seq_n'd3, `addr_n'b0111, `addr_n'b1011, `age_n'd1};
        port0_di = `data_n'h00;
        port1_ci = {1'b1, `seq_n'd3, `addr_n'b1001, `addr_n'b1000, `age_n'd1};
        port1_di = `data_n'h01;
        port2_ci = {1'b0, `seq_n'd3, `addr_n'b1000, `addr_n'b0011, `age_n'd1};
        port2_di = `data_n'h02; 
        port3_ci = {1'b1, `seq_n'd3, `addr_n'b1000, `addr_n'b0011, `age_n'd1};
        port3_di = `data_n'h03;
        port4_ci = {1'b1, `seq_n'd3, `addr_n'b0000, `addr_n'b0110, `age_n'd0};
        port4_di = `data_n'h04;
        
        @(negedge clk);
        
        port0_ci = {1'b1, `seq_n'd3, `addr_n'b0111, `addr_n'b1011, `age_n'd1};
        port0_di = `data_n'h00;
        port1_ci = {1'b1, `seq_n'd3, `addr_n'b1001, `addr_n'b1000, `age_n'd1};
        port1_di = `data_n'h01;
        port2_ci = {1'b0, `seq_n'd3, `addr_n'b1000, `addr_n'b0011, `age_n'd1};
        port2_di = `data_n'h02; 
        port3_ci = {1'b0, `seq_n'd3, `addr_n'b1000, `addr_n'b0011, `age_n'd1};
        port3_di = `data_n'h03;
        port4_ci = {1'b1, `seq_n'd3, `addr_n'b0000, `addr_n'b0110, `age_n'd0};
        port4_di = `data_n'h04;
        
        @(negedge clk);
        $display("N = 3, S = 2, E = 1, W = 0, R = X, Ready = 0");
        
        port0_ci = {1'b1, `seq_n'd3, `addr_n'b0001, `addr_n'b1111, `age_n'd9};
        port0_di = `data_n'h00;
        port1_ci = {1'b1, `seq_n'd3, `addr_n'b0010, `addr_n'b0000, `age_n'd9};
        port1_di = `data_n'h01;
        port2_ci = {1'b1, `seq_n'd3, `addr_n'b0011, `addr_n'b1111, `age_n'd9};
        port2_di = `data_n'h02; 
        port3_ci = {1'b1, `seq_n'd3, `addr_n'b0100, `addr_n'b0000, `age_n'd9};
        port3_di = `data_n'h03;
        port4_ci = {1'b1, `seq_n'd3, `addr_n'b0000, `addr_n'b0110, `age_n'd0};
        port4_di = `data_n'h04;
        
        @(negedge clk);
        $display("N = 1, S = 3, E = 0, W = 4, R = X, Ready = 1");
        
        port0_ci = {1'b1, `seq_n'd3, `addr_n'b0001, `addr_n'b1111, `age_n'd10};
        port0_di = `data_n'h00;
        port1_ci = {1'b1, `seq_n'd3, `addr_n'b0010, `addr_n'b0000, `age_n'd11};
        port1_di = `data_n'h01;
        port2_ci = {1'b1, `seq_n'd3, `addr_n'b0011, `addr_n'b1111, `age_n'd9};
        port2_di = `data_n'h02; 
        port3_ci = {1'b1, `seq_n'd3, `addr_n'b0100, `addr_n'b0000, `age_n'd9};
        port3_di = `data_n'h03;
        port4_ci = {1'b1, `seq_n'd3, `addr_n'b0000, `addr_n'b0110, `age_n'd0};
        port4_di = `data_n'h04;
        
        @(negedge clk);
        $display("N = 1, S = 0, E = 4, W = X, R = X, Ready = 1");
        
        port0_ci = {1'b0, `seq_n'd3, `addr_n'b0001, `addr_n'b1111, `age_n'd10};
        port0_di = `data_n'h00;
        port1_ci = {1'b0, `seq_n'd3, `addr_n'b0010, `addr_n'b0000, `age_n'd11};
        port1_di = `data_n'h01;
        port2_ci = {1'b0, `seq_n'd3, `addr_n'b0011, `addr_n'b1111, `age_n'd9};
        port2_di = `data_n'h02; 
        port3_ci = {1'b0, `seq_n'd3, `addr_n'b0100, `addr_n'b0000, `age_n'd9};
        port3_di = `data_n'h03;
        port4_ci = {1'b0, `seq_n'd3, `addr_n'b0000, `addr_n'b0110, `age_n'd0};
        port4_di = `data_n'h04;
        
        @(negedge clk);
        $display("N = 3, S = 2, E = 0, W = 1, R = X, Ready = 0");
        
        port0_ci = {1'b1, `seq_n'd3, `addr_n'b0001, `addr_n'b0000, `age_n'd10};
        port0_di = `data_n'h00;
        port1_ci = {1'b1, `seq_n'd3, `addr_n'b0010, `addr_n'b0000, `age_n'd11};
        port1_di = `data_n'h01;
        port2_ci = {1'b1, `seq_n'd3, `addr_n'b0011, `addr_n'b0000, `age_n'd9};
        port2_di = `data_n'h02; 
        port3_ci = {1'b1, `seq_n'd3, `addr_n'b0100, `addr_n'b0000, `age_n'd9};
        port3_di = `data_n'h03;
        port4_ci = {1'b1, `seq_n'd3, `addr_n'b0000, `addr_n'b0110, `age_n'd0};
        port4_di = `data_n'h04;
        
        @(negedge clk);
        $display("N = 1, S = 0, E = 2, W = 3, R = X, Ready = 0");

        port0_ci = {1'b1, `seq_n'd3, `addr_n'b0001, `addr_n'b0101, `age_n'd10};
        port0_di = `data_n'h00;
        port1_ci = {1'b1, `seq_n'd3, `addr_n'b0010, `addr_n'b0101, `age_n'd11};
        port1_di = `data_n'h01;
        port2_ci = {1'b1, `seq_n'd3, `addr_n'b0011, `addr_n'b0101, `age_n'd9};
        port2_di = `data_n'h02;
        port3_ci = {1'b1, `seq_n'd3, `addr_n'b0100, `addr_n'b0101, `age_n'd9};
        port3_di = `data_n'h03;
        port4_ci = {1'b1, `seq_n'd3, `addr_n'b0000, `addr_n'b0110, `age_n'd0};
        port4_di = `data_n'h04;
        
        @(negedge clk);
        $display("N = I, S = I, E = I, W = I , R = I, Ready = 1");
        
        port0_ci = {1'b0, `seq_n'd3, `addr_n'b0001, `addr_n'b0101, `age_n'd10};
        port0_di = `data_n'h00;
        port1_ci = {1'b0, `seq_n'd3, `addr_n'b0010, `addr_n'b0101, `age_n'd11};
        port1_di = `data_n'h01;
        port2_ci = {1'b0, `seq_n'd3, `addr_n'b0011, `addr_n'b0101, `age_n'd9};
        port2_di = `data_n'h02;
        port3_ci = {1'b0, `seq_n'd3, `addr_n'b0100, `addr_n'b0101, `age_n'd9};
        port3_di = `data_n'h03;
        port4_ci = {1'b1, `seq_n'd3, `addr_n'b0000, `addr_n'b0110, `age_n'd0};
        port4_di = `data_n'h04;
        
        @(negedge clk);
        $display("N = 1, S = 3, E = 2, W = 0, R = X, Ready = 0");
        
        @(negedge clk);
        $display("N = 0, S = 3, E = 2, W = 4, R = 1, Ready = 1");
        
        @(negedge clk);
        $display("N = X, S = 4, E = X, W = X, R = X, Ready = 1");
        
        @(negedge clk);

        $finish;
    end

endmodule
