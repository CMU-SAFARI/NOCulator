/*
`include "arbitor.v"

module arbitor_test();
    reg [3:0] rmatrix0, rmatrix1, rmatrix2, rmatrix3, rmatrix4;
    reg [12:0] control0_in, control1_in, control2_in, control3_in, control4_in;
    reg clk;
    wire [14:0] route_config;

    arbitor arb(rmatrix0, rmatrix1, rmatrix2, rmatrix3, rmatrix4,
                control0_in, control1_in, control2_in, control3_in, control4_in,
                clk, route_config);

    always begin
        clk = ~clk;
        #10;
    end

    always @(posedge clk) begin
        $display("         |V|N|S|E|W|R|");
        $display("rmatrix0 |%b|%b|%b|%b|%b|%b|", control0_in[12], rmatrix0[0], rmatrix0[1],
                    rmatrix0[2], rmatrix0[3], ~|rmatrix0);
        $display("rmatrix1 |%b|%b|%b|%b|%b|%b|", control1_in[12], rmatrix1[0], rmatrix1[1],
                    rmatrix1[2], rmatrix1[3], ~|rmatrix1);
        $display("rmatrix2 |%b|%b|%b|%b|%b|%b|", control2_in[12], rmatrix2[0], rmatrix2[1],
                    rmatrix2[2], rmatrix2[3], ~|rmatrix2);
        $display("rmatrix3 |%b|%b|%b|%b|%b|%b|", control3_in[12], rmatrix3[0], rmatrix3[1],
                    rmatrix3[2], rmatrix3[3], ~|rmatrix3);
        $display("rmatrix4 |%b|%b|%b|%b|%b|%b|", control4_in[12], rmatrix4[0], rmatrix4[1],
                    rmatrix4[2], rmatrix4[3], ~|rmatrix4);
        $display("         |V|SRC |DEST|AGE |");
        $display("control0 |%b|%b|%b|%b|", control0_in[12], control0_in[11:8], 
                    control0_in[7:4],control0_in[3:0]);
        $display("control1 |%b|%b|%b|%b|", control1_in[12], control1_in[11:8], 
                    control1_in[7:4],control1_in[3:0]);
        $display("control2 |%b|%b|%b|%b|", control2_in[12], control2_in[11:8], 
                    control2_in[7:4],control2_in[3:0]);
        $display("control3 |%b|%b|%b|%b|", control3_in[12], control3_in[11:8], 
                    control3_in[7:4],control3_in[3:0]);
        $display("control4 |%b|%b|%b|%b|", control4_in[12], control4_in[11:8], 
                    control4_in[7:4],control4_in[3:0]);
        $display("--------------------------");
        $display("         | N | S | E | W | R |");
        $display("route:   |%b|%b|%b|%b|%b|", route_config[2:0], route_config[5:3],
                 route_config[8:6], route_config[11:9], route_config[14:12]);
        $display("amatrix1 %b\namatrix2 %b\namatrix3 %b\namatrix4 %b",
                arb.amatrix1, arb.amatrix2, arb.amatrix3, arb.amatrix4);
        $display("");
    end

    initial begin
        clk = 0;
        
        rmatrix0 = 4'b0010;
        rmatrix1 = 4'b1000;
        rmatrix2 = 4'b0100;
        rmatrix3 = 4'b0001;
        rmatrix4 = 4'b0000;
        control0_in = {1'b1, 4'h0, 4'h1, 4'h1};
        control1_in = {1'b1, 4'h1, 4'h2, 4'h1};
        control2_in = {1'b1, 4'h2, 4'h3, 4'h1};
        control3_in = {1'b1, 4'h3, 4'h4, 4'h1};
        control4_in = {1'b0, 4'h4, 4'h0, 4'h0}; 
        
        @(negedge clk); 
        $display("N = 011, S = 000, E = 010, W = 001, R = 111");
        
        rmatrix0 = 4'b0110;
        rmatrix1 = 4'b1000;
        rmatrix2 = 4'b0110;
        rmatrix3 = 4'b0001;
        rmatrix4 = 4'b1110;
        control0_in = {1'b1, 4'h0, 4'h1, 4'h2};
        control1_in = {1'b0, 4'h1, 4'h2, 4'h9};
        control2_in = {1'b1, 4'h2, 4'h3, 4'h5};
        control3_in = {1'b0, 4'h3, 4'h4, 4'h1};
        control4_in = {1'b1, 4'h4, 4'h0, 4'h0};
        
        @(negedge clk); 
        $display("N = 111, S = 010, E = 000, W = 100, R = 111");
         
        rmatrix0 = 4'b0001;
        rmatrix1 = 4'b0010;
        rmatrix2 = 4'b0001;
        rmatrix3 = 4'b0010;
        rmatrix4 = 4'b1111;
        control0_in = {1'b1, 4'h0, 4'h1, 4'h5};
        control1_in = {1'b1, 4'h1, 4'h2, 4'h9};
        control2_in = {1'b1, 4'h2, 4'h3, 4'h5};
        control3_in = {1'b1, 4'h3, 4'h4, 4'h1};
        control4_in = {1'b0, 4'h4, 4'h0, 4'h0};

        @(negedge clk);
        $display("N = 010, S = 001, E = 000, W = 011, R = 111");

        rmatrix0 = 4'b0000;
        rmatrix1 = 4'b0011;
        rmatrix2 = 4'b1100;
        rmatrix3 = 4'b0010;
        rmatrix4 = 4'b0110;
        control0_in = {1'b1, 4'h0, 4'h1, 4'h2};
        control1_in = {1'b1, 4'h1, 4'h2, 4'h9};
        control2_in = {1'b1, 4'h2, 4'h3, 4'h5};
        control3_in = {1'b0, 4'h3, 4'h4, 4'h1};
        control4_in = {1'b1, 4'h4, 4'h0, 4'h0};

        @(negedge clk);
        $display("N = 001, S = 100, E = 010, W = 111, R = 000");

        rmatrix0 = 4'b1010;
        rmatrix1 = 4'b0011;
        rmatrix2 = 4'b1100;
        rmatrix3 = 4'b0010;
        rmatrix4 = 4'b0110;
        control0_in = {1'b0, 4'h0, 4'h1, 4'h2};
        control1_in = {1'b0, 4'h1, 4'h2, 4'h9};
        control2_in = {1'b0, 4'h2, 4'h3, 4'h5};
        control3_in = {1'b0, 4'h3, 4'h4, 4'h1};
        control4_in = {1'b1, 4'h4, 4'h0, 4'h0};
    

        @(negedge clk);
        $display("N = 111, S = 100, E = 111, W = 111, R = 111");    
        
        @(negedge clk);
        $finish;
    end

endmodule
*/
