`include "brouter_4x4.v"
`include "defines.v"

module test_brouter_4x4();
    reg    `control_w   b0_ci, b1_ci, b2_ci, b3_ci,
                        b4_ci, b5_ci, b6_ci, b7_ci,
                        b8_ci, b9_ci, ba_ci, bb_ci,
                        bc_ci, bd_ci, be_ci, bf_ci;
                       
    wire    `control_w  b0_co, b1_co, b2_co, b3_co,
                        b4_co, b5_co, b6_co, b7_co,
                        b8_co, b9_co, ba_co, bb_co,
                        bc_co, bd_co, be_co, bf_co;

    reg    `data_w      b0_di, b1_di, b2_di, b3_di,
                        b4_di, b5_di, b6_di, b7_di,
                        b8_di, b9_di, ba_di, bb_di,
                        bc_di, bd_di, be_di, bf_di;
                        
    wire    `data_w     b0_do, b1_do, b2_do, b3_do,
                        b4_do, b5_do, b6_do, b7_do,
                        b8_do, b9_do, ba_do, bb_do,
                        bc_do, bd_do, be_do, bf_do;

    wire                b0_r, b1_r, b2_r, b3_r,
                        b4_r, b5_r, b6_r, b7_r, 
                        b8_r, b9_r, ba_r, bb_r,
                        bc_r, bd_r, be_r, bf_r;
    reg rst; 
    reg clk;

    // Instantiate Network
    brouter_4x4 br
               (b0_ci, b1_ci, b2_ci, b3_ci, b4_ci, b5_ci, b6_ci, b7_ci,
                b8_ci, b9_ci, ba_ci, bb_ci, bc_ci, bd_ci, be_ci, bf_ci,
                b0_di, b1_di, b2_di, b3_di, b4_di, b5_di, b6_di, b7_di, 
                b8_di, b9_di, ba_di, bb_di, bc_di, bd_di, be_di, bf_di,
                clk, rst,
                b0_co, b1_co, b2_co, b3_co, b4_co, b5_co, b6_co, b7_co,
                b8_co, b9_co, ba_co, bb_co, bc_co, bd_co, be_co, bf_co,
                b0_do, b1_do, b2_do, b3_do, b4_do, b5_do, b6_do, b7_do,
                b8_do, b9_do, ba_do, bb_do, bc_do, bd_do, be_do, bf_do,
                b0_r, b1_r, b2_r, b3_r, b4_r, b5_r, b6_r, b7_r, 
                b8_r, b9_r, ba_r, bb_r, bc_r, bd_r, be_r, bf_r);

    // Clock
    always begin
        #10;
        clk = ~clk;
    end

    // Prints out resource port for each router
    task print_port(
        input   [15:0]      n,
        input   `control_w  c,
        input   `data_w     d,
        input               r);

        begin
            $display("B%h: |%h|%b|%b|%b|%b|%b|%b|", n, r, c[`valid_f], 
                    c[`seq_f], c[`src_f], c[`dest_f], c[`age_f], d);
        end
    endtask

    always @(posedge clk) begin
        $display("OUT:   |R|V|SEQ|SRC |DEST|AGE |  DATA  |");
        print_port(0, b0_co, b0_do, b0_r);
        print_port(1, b1_co, b1_do, b1_r);
        print_port(2, b2_co, b2_do, b2_r);
        print_port(3, b3_co, b3_do, b3_r);
        print_port(4, b4_co, b4_do, b4_r);
        print_port(5, b5_co, b5_do, b5_r);
        print_port(6, b6_co, b6_do, b6_r);
        print_port(7, b7_co, b7_do, b7_r);
        print_port(8, b8_co, b8_do, b8_r);
        print_port(9, b9_co, b9_do, b9_r);
        print_port(10, ba_co, ba_do, ba_r);
        print_port(11, bb_co, bb_do, bb_r);
        print_port(12, bc_co, bc_do, bc_r);
        print_port(13, bd_co, bd_do, bd_r);
        print_port(14, be_co, be_do, be_r);
        print_port(15, bf_co, bf_do, bf_r);
        $display("");
    end


    initial begin
        clk = 0;
        rst = 1;
        //North links
        b0_ci = {1'b1, `seq_n'd3, `addr_n'd0, `addr_n'd3, `age_n'd0};
        b0_di = `data_n'd0;
        b1_ci = {1'b1, `seq_n'd3, `addr_n'd1, `addr_n'd0, `age_n'd0};
        b1_di = `data_n'd1;
        b2_ci = {1'b1, `seq_n'd3, `addr_n'd2, `addr_n'd1, `age_n'd0};
        b2_di = `data_n'd2;
        b3_ci = {1'b1, `seq_n'd3, `addr_n'd3, `addr_n'd2, `age_n'd0};
        b3_di = `data_n'd3;
        b4_ci = {1'b1, `seq_n'd3, `addr_n'd4, `addr_n'd7, `age_n'd0};
        b4_di = `data_n'd4;
        b5_ci = {1'b1, `seq_n'd3, `addr_n'd5, `addr_n'd4, `age_n'd0};
        b5_di = `data_n'd5;
        b6_ci = {1'b1, `seq_n'd3, `addr_n'd6, `addr_n'd5, `age_n'd0};
        b6_di = `data_n'd6;
        b7_ci = {1'b1, `seq_n'd3, `addr_n'd7, `addr_n'd6, `age_n'd0};
        b7_di = `data_n'd7;
        b8_ci = {1'b1, `seq_n'd3, `addr_n'd8, `addr_n'd11, `age_n'd0};
        b8_di = `data_n'd8;
        b9_ci = {1'b1, `seq_n'd3, `addr_n'd9, `addr_n'd8, `age_n'd0};
        b9_di = `data_n'd9;
        ba_ci = {1'b1, `seq_n'd3, `addr_n'd10, `addr_n'd9, `age_n'd0};
        ba_di = `data_n'd10;
        bb_ci = {1'b1, `seq_n'd3, `addr_n'd11, `addr_n'd10, `age_n'd0};
        bb_di = `data_n'd11;
        bc_ci = {1'b1, `seq_n'd3, `addr_n'd12, `addr_n'd15, `age_n'd0};
        bc_di = `data_n'd12;
        bd_ci = {1'b1, `seq_n'd3, `addr_n'd13, `addr_n'd12, `age_n'd0};
        bd_di = `data_n'd13;
        be_ci = {1'b1, `seq_n'd3, `addr_n'd14, `addr_n'd13, `age_n'd0};
        be_di = `data_n'd14;
        bf_ci = {1'b1, `seq_n'd3, `addr_n'd15, `addr_n'd14, `age_n'd0};
        bf_di = `data_n'd15;

        // South Links
        /*
        b0_ci = {1'b1, `seq_n'd3, `addr_n'd0, `addr_n'd1, `age_n'd0};
        b0_di = `data_n'd0;
        b1_ci = {1'b1, `seq_n'd3, `addr_n'd1, `addr_n'd2, `age_n'd0};
        b1_di = `data_n'd1;
        b2_ci = {1'b1, `seq_n'd3, `addr_n'd2, `addr_n'd3, `age_n'd0};
        b2_di = `data_n'd2;
        b3_ci = {1'b1, `seq_n'd3, `addr_n'd3, `addr_n'd0, `age_n'd0};
        b3_di = `data_n'd3;
        b4_ci = {1'b1, `seq_n'd3, `addr_n'd4, `addr_n'd5, `age_n'd0};
        b4_di = `data_n'd4;
        b5_ci = {1'b1, `seq_n'd3, `addr_n'd5, `addr_n'd6, `age_n'd0};
        b5_di = `data_n'd5;
        b6_ci = {1'b1, `seq_n'd3, `addr_n'd6, `addr_n'd7, `age_n'd0};
        b6_di = `data_n'd6;
        b7_ci = {1'b1, `seq_n'd3, `addr_n'd7, `addr_n'd4, `age_n'd0};
        b7_di = `data_n'd7;
        b8_ci = {1'b1, `seq_n'd3, `addr_n'd8, `addr_n'd9, `age_n'd0};
        b8_di = `data_n'd8;
        b9_ci = {1'b1, `seq_n'd3, `addr_n'd9, `addr_n'd10, `age_n'd0};
        b9_di = `data_n'd9;
        ba_ci = {1'b1, `seq_n'd3, `addr_n'd10, `addr_n'd11, `age_n'd0};
        ba_di = `data_n'd10;
        bb_ci = {1'b1, `seq_n'd3, `addr_n'd11, `addr_n'd8, `age_n'd0};
        bb_di = `data_n'd11;
        bc_ci = {1'b1, `seq_n'd3, `addr_n'd12, `addr_n'd13, `age_n'd0};
        bc_di = `data_n'd12;
        bd_ci = {1'b1, `seq_n'd3, `addr_n'd13, `addr_n'd14, `age_n'd0};
        bd_di = `data_n'd13;
        be_ci = {1'b1, `seq_n'd3, `addr_n'd14, `addr_n'd15, `age_n'd0};
        be_di = `data_n'd14;
        bf_ci = {1'b1, `seq_n'd3, `addr_n'd15, `addr_n'd12, `age_n'd0};
        bf_di = `data_n'd15;
        */ 
        
        // East Links
        /*
        b0_ci = {1'b1, `seq_n'd3, `addr_n'd0, `addr_n'd4, `age_n'd0};
        b0_di = `data_n'd0;
        b1_ci = {1'b1, `seq_n'd3, `addr_n'd1, `addr_n'd5, `age_n'd0};
        b1_di = `data_n'd1;
        b2_ci = {1'b1, `seq_n'd3, `addr_n'd2, `addr_n'd6, `age_n'd0};
        b2_di = `data_n'd2;
        b3_ci = {1'b1, `seq_n'd3, `addr_n'd3, `addr_n'd7, `age_n'd0};
        b3_di = `data_n'd3;
        b4_ci = {1'b1, `seq_n'd3, `addr_n'd4, `addr_n'd8, `age_n'd0};
        b4_di = `data_n'd4;
        b5_ci = {1'b1, `seq_n'd3, `addr_n'd5, `addr_n'd9, `age_n'd0};
        b5_di = `data_n'd5;
        b6_ci = {1'b1, `seq_n'd3, `addr_n'd6, `addr_n'd10, `age_n'd0};
        b6_di = `data_n'd6;
        b7_ci = {1'b1, `seq_n'd3, `addr_n'd7, `addr_n'd11, `age_n'd0};
        b7_di = `data_n'd7;
        b8_ci = {1'b1, `seq_n'd3, `addr_n'd8, `addr_n'd12, `age_n'd0};
        b8_di = `data_n'd8;
        b9_ci = {1'b1, `seq_n'd3, `addr_n'd9, `addr_n'd13, `age_n'd0};
        b9_di = `data_n'd9;
        ba_ci = {1'b1, `seq_n'd3, `addr_n'd10, `addr_n'd14, `age_n'd0};
        ba_di = `data_n'd10;
        bb_ci = {1'b1, `seq_n'd3, `addr_n'd11, `addr_n'd15, `age_n'd0};
        bb_di = `data_n'd11;
        bc_ci = {1'b1, `seq_n'd3, `addr_n'd12, `addr_n'd0, `age_n'd0};
        bc_di = `data_n'd12;
        bd_ci = {1'b1, `seq_n'd3, `addr_n'd13, `addr_n'd1, `age_n'd0};
        bd_di = `data_n'd13;
        be_ci = {1'b1, `seq_n'd3, `addr_n'd14, `addr_n'd2, `age_n'd0};
        be_di = `data_n'd14;
        bf_ci = {1'b1, `seq_n'd3, `addr_n'd15, `addr_n'd3, `age_n'd0};
        bf_di = `data_n'd15;
        */        

        // West Links
        /*
        b0_ci = {1'b1, `seq_n'd3, `addr_n'd0, `addr_n'd12, `age_n'd0};
        b0_di = `data_n'd0;
        b1_ci = {1'b1, `seq_n'd3, `addr_n'd1, `addr_n'd13, `age_n'd0};
        b1_di = `data_n'd1;
        b2_ci = {1'b1, `seq_n'd3, `addr_n'd2, `addr_n'd14, `age_n'd0};
        b2_di = `data_n'd2;
        b3_ci = {1'b1, `seq_n'd3, `addr_n'd3, `addr_n'd15, `age_n'd0};
        b3_di = `data_n'd3;
        b4_ci = {1'b1, `seq_n'd3, `addr_n'd4, `addr_n'd0, `age_n'd0};
        b4_di = `data_n'd4;
        b5_ci = {1'b1, `seq_n'd3, `addr_n'd5, `addr_n'd1, `age_n'd0};
        b5_di = `data_n'd5;
        b6_ci = {1'b1, `seq_n'd3, `addr_n'd6, `addr_n'd2, `age_n'd0};
        b6_di = `data_n'd6;
        b7_ci = {1'b1, `seq_n'd3, `addr_n'd7, `addr_n'd3, `age_n'd0};
        b7_di = `data_n'd7;
        b8_ci = {1'b1, `seq_n'd3, `addr_n'd8, `addr_n'd4, `age_n'd0};
        b8_di = `data_n'd8;
        b9_ci = {1'b1, `seq_n'd3, `addr_n'd9, `addr_n'd5, `age_n'd0};
        b9_di = `data_n'd9;
        ba_ci = {1'b1, `seq_n'd3, `addr_n'd10, `addr_n'd6, `age_n'd0};
        ba_di = `data_n'd10;
        bb_ci = {1'b1, `seq_n'd3, `addr_n'd11, `addr_n'd7, `age_n'd0};
        bb_di = `data_n'd11;
        bc_ci = {1'b1, `seq_n'd3, `addr_n'd12, `addr_n'd8, `age_n'd0};
        bc_di = `data_n'd12;
        bd_ci = {1'b1, `seq_n'd3, `addr_n'd13, `addr_n'd9, `age_n'd0};
        bd_di = `data_n'd13;
        be_ci = {1'b1, `seq_n'd3, `addr_n'd14, `addr_n'd10, `age_n'd0};
        be_di = `data_n'd14;
        bf_ci = {1'b1, `seq_n'd3, `addr_n'd15, `addr_n'd11, `age_n'd0};
        bf_di = `data_n'd15;
        */
        // Reset Overhead
        @(negedge clk);
        rst = 0;
        @(negedge clk);
        @(negedge clk);

        // Cycle 1
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);

        // Cycle 2
        @(negedge clk);
        @(negedge clk);
        @(negedge clk);

        // Cycle 3
        @(negedge clk);


        $finish;
    end

endmodule
