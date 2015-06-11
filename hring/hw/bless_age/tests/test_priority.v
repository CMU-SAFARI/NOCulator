`include "priority_comp.v"

module test_priority();
    reg [12:0] control0;
    reg [12:0] control1;
    reg [12:0] control2;
    reg [12:0] control3;
    wire [1:0]  p0, p1, p2, p3;

    priority_comparator pc(control0, control1, control2, control3,
                           p0, p1, p2, p3);

    initial begin
        $monitor("Control0 |%b|%b|%b|%b\nControl1 |%b|%b|%b|%b\nControl2 |%b|%b|%b|%b\nControl3 |%b|%b|%b|%b\nPort0: %h\nPort1: %h\nPort2: %h\nPort3: %h\n", control0[12], control0[11:8], control0[7:4], control0[3:0], control1[12], control1[11:8], control1[7:4], control1[3:0], control2[12], control2[11:8], control2[7:4], control2[3:0], control3[12], control3[11:8], control3[7:4], control3[3:0], p0, p1, p2, p3);
        
       
        $display("0,1,2,3");
        control0 = {1'b1, 4'h4, 4'h0, 4'h4};
        control1 = {1'b1, 4'h3, 4'h0, 4'h3};
        control2 = {1'b1, 4'h2, 4'h0, 4'h2};
        control3 = {1'b1, 4'h1, 4'h0, 4'h1};
        #20; 

        $display("2,1,3,0");
        control0 = {1'b1, 4'h4, 4'h0, 4'h1};
        control1 = {1'b1, 4'h3, 4'h0, 4'h3};
        control2 = {1'b1, 4'h2, 4'h0, 4'h5};
        control3 = {1'b1, 4'h1, 4'h0, 4'h2};
        #20; 

        $display("1,2,0,3");
        control0 = {1'b1, 4'h4, 4'h0, 4'h2};
        control1 = {1'b1, 4'h3, 4'h0, 4'h5};
        control2 = {1'b1, 4'h2, 4'h0, 4'h3};
        control3 = {1'b1, 4'h1, 4'h0, 4'h1};
        #20; 

        $display("2,1,3,0");
        control0 = {1'b1, 4'h4, 4'h0, 4'h2};
        control1 = {1'b1, 4'h3, 4'h0, 4'h4};
        control2 = {1'b1, 4'h2, 4'h0, 4'h9};
        control3 = {1'b1, 4'h1, 4'h0, 4'h4};
        #20; 

        $display("3,2,0,1");
        control0 = {1'b1, 4'h4, 4'h0, 4'h3};
        control1 = {1'b1, 4'h3, 4'h0, 4'h1};
        control2 = {1'b1, 4'h2, 4'h0, 4'h5};
        control3 = {1'b1, 4'h1, 4'h0, 4'hA};
        #20; 
        
        $display("3,0,1,2");
        control0 = {1'b1, 4'h4, 4'h0, 4'h3};
        control1 = {1'b1, 4'h3, 4'h0, 4'h1};
        control2 = {1'b0, 4'h2, 4'h0, 4'h5};
        control3 = {1'b1, 4'h1, 4'h0, 4'hA};
        #20; 

        $display("3,0,2,1");
        control0 = {1'b1, 4'h4, 4'h0, 4'h3};
        control1 = {1'b0, 4'h3, 4'h0, 4'h1};
        control2 = {1'b0, 4'h2, 4'h0, 4'h5};
        control3 = {1'b1, 4'h1, 4'h0, 4'hA};
        #20; 




        $finish;
    end
endmodule
*/
/*
module test_pc_cell();
    reg [12:0]  control0;
    reg [12:0]  control1;
    reg [2:0]   portno1;
    reg [2:0]   portno2;

    wire [12:0] control_h;
    wire [12:0] control_l;
    wire [1:0]  portno_h;
    wire [1:0]  portno_l;

    pc_cell pc(control0, control1, portno1, portno2,
                     control_h, control_l, portno_h, portno_l);

    
    initial begin
        $monitor("Control1: %b|%b|%b|%b\nControl2: %b|%b|%b|%b\nControlH: %b|%b|%b|%b\nControlL: %b|%b|%b|%b\nPort1: %h\nPort2: %h\nPortH: %h\nPortL: %h\n", 
              control0[12], control0[11:8],
              control0[7:4], control0[3:0], control1[12],
              control1[11:8], control1[7:4], control1[3:0],
              control_h[12], control_h[11:8], control_h[7:4],
              control_h[3:0], control_l[12], control_l[11:8],
              control_l[7:4], control_l[3:0], portno1, portno2,
              portno_h, portno_l);
        

        control0 = {1'b1, 4'h0, 4'h0, 4'h3};
        control1 = {1'b1, 4'h1, 4'h0, 4'h2};
        portno1 = 2'b00;
        portno2 = 2'b01;
    
        #10;

        control0 = {1'b1, 4'h0, 4'h0, 4'h1};
        control1 = {1'b1, 4'h1, 4'h0, 4'h9};
        portno1 = 2'b00;
        portno2 = 2'b01;
        
        #10;
 
        control0 = {1'b1, 4'h0, 4'h0, 4'h3};
        control1 = {1'b1, 4'h1, 4'h0, 4'h3};
        portno1 = 2'b00;
        portno2 = 2'b01;

        #10;
  
        control0 = {1'b1, 4'h3, 4'h0, 4'h3};
        control1 = {1'b1, 4'h1, 4'h0, 4'h3};
        portno1 = 2'b00;
        portno2 = 2'b01;

        #10;      
        
        control0 = {1'b0, 4'h3, 4'h0, 4'h3};
        control1 = {1'b1, 4'h1, 4'h0, 4'h3};
        portno1 = 2'b00;
        portno2 = 2'b01;

        #10;      
        
        control0 = {1'b1, 4'h3, 4'h0, 4'h3};
        control1 = {1'b0, 4'h1, 4'h0, 4'h3};
        portno1 = 2'b00;
        portno2 = 2'b01;

        #10;      
        
        control0 = {1'b0, 4'h3, 4'h0, 4'h3};
        control1 = {1'b0, 4'h1, 4'h0, 4'h3};
        portno1 = 2'b00;
        portno2 = 2'b01;

        #10;      
        $finish;
    
    end
endmodule
