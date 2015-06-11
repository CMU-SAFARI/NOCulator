`include "crossbar.v"

module test_crossbar ();
    reg [12:0] control0_in;
    reg [12:0] control1_in;
    reg [12:0] control2_in;
    reg [12:0] control3_in;
    reg [12:0] control4_in;
    reg [7:0] data0_in; 
    reg [7:0] data1_in; 
    reg [7:0] data2_in; 
    reg [7:0] data3_in; 
    reg [7:0] data4_in; 
    reg [14:0] route_config;
    reg clk;


    wire [12:0] control0_out; 
    wire [12:0] control1_out; 
    wire [12:0] control2_out; 
    wire [12:0] control3_out; 
    wire [12:0] control4_out; 
    wire [7:0] data0_out;
    wire [7:0] data1_out;
    wire [7:0] data2_out;
    wire [7:0] data3_out;
    wire [7:0] data4_out;

    crossbar xbar(control0_in,
                  control1_in,
                  control2_in,
                  control3_in,
                  control4_in,
                  data0_in,
                  data1_in,
                  data2_in,
                  data3_in,
                  data4_in,
                  route_config,
                  clk,
                  control0_out,
                  control1_out,
                  control2_out,
                  control3_out,
                  control4_out,
                  data0_out,
                  data1_out,
                  data2_out,
                  data3_out,
                  data4_out);

    always begin
        #5
        clk = ~clk;
    end

    always @(posedge clk) begin
        $display("|Ctrl|Data|");
        $display("|%h| %h | (Input 1)", control0_in, data0_in);
        $display("|%h| %h | (Input 2)", control1_in, data1_in);
        $display("|%h| %h | (Input 3)", control2_in, data2_in);
        $display("|%h| %h | (Input 4)", control3_in, data3_in);
        $display("|%h| %h | (Input 5)", control4_in, data4_in);
        $display("-------------------");
        $display("|%h| %h | (Output1)", control0_out, data0_out);
        $display("|%h| %h | (Output2)", control1_out, data1_out);
        $display("|%h| %h | (Output3)", control2_out, data2_out);
        $display("|%h| %h | (Output4)", control3_out, data3_out);
        $display("|%h| %h | (Output5)", control4_out, data4_out);
        $display(" ");
    end
    initial begin
        clk = 0;
        control0_in = 13'h1AAA;
        control1_in = 13'h1BBB;
        control2_in = 13'h1CCC;
        control3_in = 13'h1DDD;
        control4_in = 13'h1EEE;
        data0_in = 8'h00;
        data1_in = 8'h11;
        data2_in = 8'h22;
        data3_in = 8'h33;
        data4_in = 8'h44;
        route_config = 15'h0000;

        @(negedge clk)

        route_config = 15'b000_000_000_000_000;
        @(negedge clk);
        
        $display("1->1");
        $display("2->1");
        $display("3->1");
        $display("4->1");
        $display("5->1");

        route_config = 15'b001_001_001_001_001;
        @(negedge clk);
        
        $display("1->2");
        $display("2->2");
        $display("3->2");
        $display("4->2");
        $display("5->2");
        
        route_config = 15'b010_010_010_010_010;
        @(negedge clk);
        
        $display("1->3");
        $display("2->3");
        $display("3->3");
        $display("4->3");
        $display("5->3");
        
        route_config = 15'b011_011_011_011_011;
        @(negedge clk);
        
        $display("1->4");
        $display("2->4");
        $display("3->4");
        $display("4->4");
        $display("5->4");
        
        route_config = 15'b100_100_100_100_100;
        @(negedge clk);
        
        $display("1->5");
        $display("2->5");
        $display("3->5");
        $display("4->5");
        $display("5->5");
         
        route_config = 15'b100_011_010_001_000;
        @(negedge clk);
        
        $display("1->1");
        $display("2->2");
        $display("3->3");
        $display("4->4");
        $display("5->5");
         
        route_config = 15'b000_001_010_011_100;
        @(negedge clk);
        
        $display("1->5");
        $display("2->4");
        $display("3->3");
        $display("4->2");
        $display("5->1");
        
        route_config = 15'b001_100_011_000_010;
        @(negedge clk);
        
        $display("1->3");
        $display("2->1");
        $display("3->4");
        $display("4->5");
        $display("5->2");
       
        route_config = 15'b111_111_111_111_111;
        @(negedge clk);
         
        $display("1->x");
        $display("2->x");
        $display("3->x");
        $display("4->x");
        $display("5->x");
       
        @(negedge clk);

        $finish;
        
        
    end

endmodule
