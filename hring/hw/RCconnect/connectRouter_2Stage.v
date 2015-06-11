`include "defines.v"

module connectRouter
#(parameter     addr = 4'b0000) //No. 0 connect router, port 0
(   
    input       `control_w  port_in,
    input 		`control_w	inj,
    
    input                   clk,
    input                   rst,
    output 		`control_w	port_out,
    output 		`control_w	bout,
    output 					accept,
    input					can_pop,
    output 		[2:0]		size,
    output 					bfull
);

	wire `control_w port_0;
  	assign port_0 = (rst) ? `control_n'd0 : port_in;
	wire bfull;	
	wire productive;
	wire can_push;
	wire can_pop;
	wire `control_w bout;
	wire [2:0] bsize;

buffer 	bf(
		.in(port_r_0),
		.clk(clk),
		.rst(rst),
		.push(can_push),
		.pop(can_pop),
		.out(bout),
		.full(bfull),
		.bsize(size)
	);

	
    /************* STAGE 1 *************/	
	
    reg `control_w port_r_0, port_r_1;
    wire `control_w port_1, port_2;
    
    always @(posedge clk) begin
        port_r_0 <= port_0;
    end

	assign productive = (port_r_0[`dest_f] < 4 || port_r_0[`dest_f] > 11)? 0 : 1;
	assign can_push = productive && ~bfull;
	assign port_1 = can_push ? 0 : port_r_0;
	
	assign accept = (~port_1[`valid_f] & inj[`valid_f]);
	assign port_2 = accept ? inj : port_1;
	
	 	
    /*********** STAGE 2 ***************/
    always @ (posedge clk) begin
    	port_r_1 <= port_2;
    end
    assign port_out = port_r_1;
endmodule

module buffer
#(parameter 		buffersize = 8)
(
	input 		`control_w  in,
	input 					clk,
	input					rst,
	output 		`control_w	out,
	input					push,
	input					pop,	
	output					full,	
	output 		[2:0]		bsize		
);
	reg [2:0] head, tail;
	reg [2:0] size;
	integer n;
	
	reg	`control_w rf[buffersize-1:0];
	always @ rst begin
		head <= 0;
		tail <= 0;
		size <= 0;
		for (n = 0; n < 8; n = n + 1)
			rf[n] <= 0;
	end
	
	assign out = rf[head];
	assign full = (size == 7);
	assign bsize = size;
	
	always @ (posedge clk) begin
		if (push && pop) begin
			rf[tail] <= in;
			tail <= tail + 1;
			head <= head + 1;
		end
		else if (push) begin
			rf[tail] <= in;
			tail <= tail + 1;
			size <= size + 1;
		end
		else if (pop) begin
			head <= head + 1;
			size = size - 1;
		end
		else
			;
	end
endmodule
