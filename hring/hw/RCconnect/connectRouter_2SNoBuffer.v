`include "defines.v"

module connectRouter_nobuffer
#(parameter     addr = 4'b0000) //No. 0 connect router, port 0
(   
    input       `control_w  port_in,
    input 		`control_w	inj,
    input 					bfull,

    input                   clk,
    input                   rst,
    output 		`control_w	port_out,
    output 					accept,
    output 		`control_w  eject,
	output 					push
	
);

	wire `control_w port_0;
  	assign port_0 = (rst) ? `control_n'd0 : port_in;
	wire bfull;	
	wire productive;
	wire push;
	wire pop;
	wire `control_w bout;
	wire [2:0] bsize;

	
    /************* STAGE 1 *************/	
	
    reg `control_w port_r_0, port_r_1;
    wire `control_w port_1, port_2;
    
    always @(posedge clk) begin
        port_r_0 <= port_0;
    end

	assign productive = (port_r_0[`dest_f] < 4 || port_r_0[`dest_f] > 11)? 0 : 1;
	assign push = productive && ~bfull;
	assign eject = port_r_0;
	assign port_1 = push ? 0 : port_r_0;
	
	assign accept = (~port_1[`valid_f] & inj[`valid_f]);
	assign port_2 = accept ? inj : port_1;
	
	 	
    /*********** STAGE 2 ***************/
    always @ (posedge clk) begin
    	port_r_1 <= port_2;
    end
    assign port_out = port_r_1;
endmodule

