/*
`include "arbitor.v"

module test_arbitor_cell();
     reg [2:0] priority;
     reg [3:0] rmatrix0, rmatrix1, rmatrix2, rmatrix3, rmatrix4;
     reg valid0, valid1, valid2, valid3, valid4;
     reg [4:0] amatrix_o;
     reg [14:0] rco;
     wire [4:0] amatrix_n;
     wire [14:0] rcn;

     arbitor_cell ac(priority, rmatrix0, rmatrix1, rmatrix2, rmatrix3, rmatrix4,
                     valid0, valid1, valid2, valid3, valid4,
                     amatrix_o, rco, amatrix_n, rcn);

    initial begin
        $monitor("Priority: %d\n           |N|S|E|W|R|V|\nrmatrix0:  |%b|%b|%b|%b| |%b|\nrmatrix1:  |%b|%b|%b|%b| |%b|\nrmatrix2:  |%b|%b|%b|%b| |%b|\nrmatrix3:  |%b|%b|%b|%b| |%b|\nrmatrix4:  |%b|%b|%b|%b| |%b|\namatrix_o: |%b|%b|%b|%b|%b|\namatrix_n: |%b|%b|%b|%b|%b|\nrco: |%b|%b|%b|%b|%b|\nrcn: |%b|%b|%b|%b|%b|\n",
                  priority, rmatrix0[0], rmatrix0[1], rmatrix0[2], rmatrix0[3], valid0,
                  rmatrix1[0], rmatrix1[1], rmatrix1[2], rmatrix1[3], valid1,
                  rmatrix2[0], rmatrix2[1], rmatrix2[2], rmatrix2[3], valid2,
                  rmatrix3[0], rmatrix3[1], rmatrix3[2], rmatrix3[3], valid3,
                  rmatrix4[0], rmatrix4[1], rmatrix4[2], rmatrix4[3], valid4,
                  amatrix_o[0], amatrix_o[1], amatrix_o[2], amatrix_o[3], 
                  amatrix_o[4], amatrix_n[0], amatrix_n[1], amatrix_n[2],
                  amatrix_n[3], amatrix_n[4], rco[14:12], rco[11:9], rco[8:6],
                  rco[5:3], rco[2:0], rcn[14:12], rcn[11:9], rcn[8:6], rcn[5:3],
                  rcn[2:0]);

        $display("Output to port 1 from port 0");
        priority = 3'b000;
        rmatrix0 = 4'b1010;
        rmatrix1 = 4'b1010;
        rmatrix2 = 4'b1010;
        rmatrix3 = 4'b1010;
        rmatrix4 = 4'b1010;
        valid0 = 1'b1;
        valid1 = 1'b1;
        valid2 = 1'b1;
        valid3 = 1'b1;
        valid4 = 1'b1;
        amatrix_o = 5'b00000;
        rco = 14'd0;
        #10;

        $display("Output to port 0 from port 3");
        priority = 3'b011;
        rmatrix0 = 4'b0101;
        rmatrix1 = 4'b0101;
        rmatrix2 = 4'b0101;
        rmatrix3 = 4'b0101;
        rmatrix4 = 4'b1010;
        valid0 = 1'b1;
        valid1 = 1'b1;
        valid2 = 1'b1;
        valid3 = 1'b1;
        valid4 = 1'b1;
        amatrix_o = 5'b00000;
        rco = 14'd0;
        #10;

        $display("Output to port 3 from port 1");
        priority = 3'b001;
        rmatrix0 = 4'b0100;
        rmatrix1 = 4'b1100;
        rmatrix2 = 4'b0100;
        rmatrix3 = 4'b0100;
        rmatrix4 = 4'b1010;
        valid0 = 1'b1;
        valid1 = 1'b1;
        valid2 = 1'b1;
        valid3 = 1'b1;
        valid4 = 1'b1;
        amatrix_o = 5'b00100;
        rco = 14'd0;
        #10;

        $display("Output to port 3 from port 2");
        priority = 3'b010;
        rmatrix0 = 4'b0100;
        rmatrix1 = 4'b1100;
        rmatrix2 = 4'b1111;
        rmatrix3 = 4'b0100;
        rmatrix4 = 4'b1010;
        valid0 = 1'b1;
        valid1 = 1'b1;
        valid2 = 1'b1;
        valid3 = 1'b1;
        valid4 = 1'b1;
        amatrix_o = 5'b10111;
        rco = 14'd0;
        #10;
        
        $display("Invalid");
        priority = 3'b111;
        rmatrix0 = 4'b0100;
        rmatrix1 = 4'b1100;
        rmatrix2 = 4'b1111;
        rmatrix3 = 4'b0100;
        rmatrix4 = 4'b1010;
        valid0 = 1'b1;
        valid1 = 1'b1;
        valid2 = 1'b1;
        valid3 = 1'b1;
        valid4 = 1'b1;
        amatrix_o = 5'b10101;
        rco = 14'h0aaa;
        #10;
        
        $display("Misroute");
        priority = 3'b010;
        rmatrix0 = 4'b0100;
        rmatrix1 = 4'b1100;
        rmatrix2 = 4'b0001;
        rmatrix3 = 4'b0100;
        rmatrix4 = 4'b1010;
        valid0 = 1'b1;
        valid1 = 1'b1;
        valid2 = 1'b1;
        valid3 = 1'b1;
        valid4 = 1'b1;
        amatrix_o = 5'b00001;
        rco = 14'd0;
        #10;
        
        $display("Invalid Line");
        priority = 3'b010;
        rmatrix0 = 4'b0100;
        rmatrix1 = 4'b1100;
        rmatrix2 = 4'b0001;
        rmatrix3 = 4'b0100;
        rmatrix4 = 4'b1010;
        valid0 = 1'b1;
        valid1 = 1'b1;
        valid2 = 1'b0;
        valid3 = 1'b1;
        valid4 = 1'b1;
        amatrix_o = 5'b00010;
        rco = 14'd0;
        #10;
    end

endmodule
*/
