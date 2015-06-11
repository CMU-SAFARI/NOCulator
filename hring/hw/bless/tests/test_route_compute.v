`include "route_compute.v"


module test_route_compute ();
    reg [12:0] inc;
    reg [1:0] addrx;
    reg [1:0] addry;
    reg [1:0] addrx_max;
    reg [1:0] addry_max;
    wire [3:0] rmatrix;
    reg clock;

    reg [2:0] test;

    RouteCompute rc(inc, addrx, addry, addrx_max, addry_max, clock, rmatrix);

    always
        #10 clock = ~clock;

    always @(negedge clock) begin
        $display("Bits:   |V|SRC |DST |AGE |");
        $display("Input:  |%b|%b|%b|%b|", inc[12], inc[11:8], 
                                          inc[7:4], inc[3:0]);
        $display("Dirs    |N|S|E|W|");
        $display("Matrix  |%b|%b|%b|%b|", rmatrix[0], rmatrix[1], 
                                          rmatrix[2], rmatrix[3]);
        $display("");
    end

    initial begin
        clock = 0;
        inc = 13'h1000;
        addrx = 2'b01;
        addry = 2'b01;
        addrx_max = 2'b11;
        addry_max = 2'b11;

        @(negedge clock)
        $display("West and North");
        inc = 13'b1_1010_0000_0000;

        @(negedge clock)
        $display("Equal and North");
        inc = 13'b1_1010_0100_0000;

        @(negedge clock)
        $display("Equal and North/South");
        inc = 13'b1_1010_0111_0000;

        @(negedge clock)
        $display("West and Equal");
        inc = 13'b1_1010_0001_0000;

        @(negedge clock)
        $display("East/West and Equal");
        inc = 13'b1_1010_1101_0000;

        @(negedge clock)
        $display("East/West and North");
        inc = 13'b1_1010_1100_0000;

        @(negedge clock)
        $display("West and North/South");
        inc = 13'b1_1010_0011_0000;

        //@(negedge clock)
        //$display("South and West");
        //inc = 13'b1_1010_1100_0000;

        @(negedge clock)
        $display("Equal and Equal");
        inc = 13'b1_1010_0101_0000;

        @(negedge clock)
        $finish;
    end
endmodule
