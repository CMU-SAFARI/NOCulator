`include "defines.v"

module HRbridge16
#(parameter     addr = 4'b0000) //No. 0 connect router, port 0
(   
    input       `control_w  port_l0_i,
	input 		`control_w  port_l1_i,
	input 		`control_w 	port_g0_i,
	input 		`control_w 	port_g1_i,

	output      `control_w  port_l0_o,
	output 		`control_w  port_l1_o,
	output 		`control_w 	port_g0_o,
	output 		`control_w 	port_g1_o,

	input 		`control_w 	FIFO_l0_i,
	input		`control_w 	FIFO_l1_i,
	input		`control_w  FIFO_g0_i,
	input		`control_w  FIFO_g1_i,

	output 		`control_w 	FIFO_l0_o,
	output		`control_w 	FIFO_l1_o,
	output		`control_w  FIFO_g0_o,
	output		`control_w  FIFO_g1_o,

	input 					bfull_l0_i,
    input 					bfull_l1_i,
	input 					bfull_g0_i,
    input 					bfull_g1_i,

    input                   clk,
    input                   rst,
	
	output 					deQ_l0_o,
	output 					deQ_l1_o,
	output 					deQ_g0_o,
	output 					deQ_g1_o,
	
	output 					enQ_l0_o,
	output 					enQ_l1_o,
	output 					enQ_g0_o,
	output 					enQ_g1_o
);

	wire `control_w port_l0_0, port_l1_0, port_g0_0, port_g1_0;
	assign port_l0_0 = rst ? `control_n'h0 : port_l0_i;
	assign port_l1_0 = rst ? `control_n'h0 : port_l1_i;
	assign port_g0_0 = rst ? `control_n'h0 : port_g0_i;
	assign port_g1_0 = rst ? `control_n'h0 : port_g1_i;

	// global logic
	reg localRR;
	reg [1:0] globalRR;
	
	initial begin
		localRR = 0;
		globalRR = 0;
	end

    /************* STAGE 1 *************/	
	
    reg `control_w port_l0_r, port_l1_r, port_g0_r, port_g1_r;
	wire `control_w port_l0_1, port_l1_1, port_g0_1, port_g1_1;
    always @(posedge clk) begin
        port_l0_r <= port_l0_0;
		port_l1_r <= port_l1_0;
        port_g0_r <= port_g0_0;
		port_g1_r <= port_g1_0;
    end
	wire prod_l0, prod_l1, prod_g0, prod_g1, prod_g2, prod_g3; // if the coming signal productve
	assign prod_l0 = port_l0_r[`valid_f] & (port_l0_r[`dest_f] >= 4);
	assign prod_l1 = port_l1_r[`valid_f] & (port_l1_r[`dest_f] >= 4);
	assign prod_g0 = port_g0_r[`valid_f] & (port_g0_r[`dest_f] < 4);
	assign prod_g1 = port_g1_r[`valid_f] & (port_g1_r[`dest_f] < 4);

	wire req_swap_l0_g0 = prod_l0 & bfull_l0_i & prod_g0 & bfull_g0_i;
	wire req_swap_l0_g1 = prod_l0 & bfull_l0_i & prod_g1 & bfull_g1_i;
	wire req_swap_l1_g0 = prod_l1 & bfull_l1_i & prod_g0 & bfull_g0_i;
	wire req_swap_l1_g1 = prod_l1 & bfull_l1_i & prod_g1 & bfull_g1_i;

	wire swap_l0_g0 = req_swap_l0_g0;
	wire swap_l0_g1 = ~swap_l0_g0 & req_swap_l0_g1;
	wire swap_l1_g0 = ~swap_l0_g0 & req_swap_l1_g0;
	wire swap_l1_g1 = ~swap_l1_g0 & ~swap_l0_g1 & req_swap_l1_g1;
	wire swap_l0 = swap_l0_g0 | swap_l0_g1;
	wire swap_l1 = swap_l1_g0 | swap_l1_g1;
	wire swap_g0 = swap_l0_g0 | swap_l1_g0;
	wire swap_g1 = swap_l0_g1 | swap_l1_g1;

	// data into the buffer
	assign FIFO_l0_o = port_l0_r;
	assign FIFO_l1_o = port_l1_r;
	assign FIFO_g0_o = port_g0_r;
	assign FIFO_g1_o = port_g1_r;

	assign enQ_l0_o = (prod_l0 & ~bfull_l0_i) | swap_l0_g0 | swap_l0_g1;
	assign enQ_l1_o = (prod_l1 & ~bfull_l1_i) | swap_l1_g0 | swap_l1_g1;
	assign enQ_g0_o = (prod_g0 & ~bfull_g0_i) | swap_l0_g0 | swap_l1_g0;
	assign enQ_g1_o = (prod_g1 & ~bfull_g1_i) | swap_l0_g1 | swap_l1_g1;

	// data from buffer to output
	// data from local buffer tp global output
	wire pref_l0 = (FIFO_l0_i[`dest_f] >= 4 && FIFO_l0_i[`dest_f] <= 9)? 0 : 1;
	wire pref_l1 = (FIFO_l1_i[`dest_f] >= 4 && FIFO_l1_i[`dest_f] <= 9)? 0 : 1;

	wire req_inj_l0_g0 = (~pref_l0 & (~port_g0_r[`valid_f] | enQ_g0_o) & ~swap_l0 & ~swap_g0 & FIFO_l0_i[`valid_f]) | swap_l0_g0;
	wire req_inj_l0_g1 = (pref_l0 & (~port_g1_r[`valid_f] | enQ_g1_o) & ~swap_l0 & ~swap_g1 & FIFO_l0_i[`valid_f]) | swap_l0_g1;
	
	wire req_inj_l1_g0 = (~pref_l1 & (~port_g0_r[`valid_f] | enQ_g0_o) & ~swap_l1 & ~swap_g0 & FIFO_l1_i[`valid_f]) | swap_l1_g0;
	wire req_inj_l1_g1 = (pref_l1 & (~port_g1_r[`valid_f] | enQ_g1_o) & ~swap_l1 & ~swap_g1 & FIFO_l1_i[`valid_f]) | swap_l1_g1;

	wire inj_l0_g0 = req_inj_l0_g0;
	wire inj_l0_g1 = req_inj_l0_g1 & ~inj_l0_g0;
	wire inj_l1_g0 = req_inj_l1_g0 & ~inj_l0_g0;
	wire inj_l1_g1 = req_inj_l1_g1 & ~inj_l0_g1 & ~inj_l1_g0;
	


	assign port_g0_1 = inj_l0_g0? FIFO_l0_i : (inj_l1_g0? FIFO_l1_i : (enQ_g0_o? 0 : port_g0_r));
	assign port_g1_1 = inj_l0_g1? FIFO_l0_i : (inj_l1_g1? FIFO_l1_i : (enQ_g1_o? 0 : port_g1_r));
	assign deQ_l0_o = inj_l0_g0 | inj_l0_g1;
	assign deQ_l1_o = inj_l1_g0 | inj_l1_g1;
	// data from global buffer to local output
	wire pref_g0 = (FIFO_g0_i[`dest_f] == 0 || FIFO_g0_i[`dest_f] == 3)? 0 : 1;
	wire pref_g1 = (FIFO_g1_i[`dest_f] == 0 || FIFO_g1_i[`dest_f] == 3)? 0 : 1;

	wire req_inj_g0_l0 = (~pref_g0 & (~port_l0_r[`valid_f] | enQ_l0_o) & ~swap_l0 & ~swap_g0 & FIFO_g0_i[`valid_f]) | swap_l0_g0;
	wire req_inj_g1_l0 = (~pref_g1 & (~port_l0_r[`valid_f] | enQ_l0_o) & ~swap_l0 & ~swap_g1 & FIFO_g1_i[`valid_f]) | swap_l0_g1;

	wire req_inj_g0_l1 = (pref_g0 & (~port_l1_r[`valid_f] | enQ_l1_o) & ~swap_l1 & ~swap_g0 & FIFO_g0_i[`valid_f]) | swap_l1_g0;
	wire req_inj_g1_l1 = (pref_g1 & (~port_l1_r[`valid_f] | enQ_l1_o) & ~swap_l1 & ~swap_g1 & FIFO_g1_i[`valid_f]) | swap_l1_g1;

	wire inj_g0_l0 = req_inj_g0_l0;
	wire inj_g1_l0 = req_inj_g1_l0 & ~inj_g0_l0;
	wire inj_g0_l1 = req_inj_g0_l1;
	wire inj_g1_l1 = req_inj_g1_l1 & ~inj_g0_l1;

	assign port_l0_1 = inj_g0_l0? FIFO_g0_i :
		(inj_g1_l0? FIFO_g1_i : 
			(enQ_l0_o? 0 : port_l0_r));
	assign port_l1_1 = inj_g0_l1? FIFO_g0_i :
		(inj_g1_l1? FIFO_g1_i : 
			(enQ_l1_o? 0 : port_l1_r));

	assign deQ_g0_o = inj_g0_l0 | inj_g0_l1;
	assign deQ_g1_o = inj_g1_l0 | inj_g1_l1;

	 	
    /*********** STAGE 2 ***************/
    reg `control_w port_l0_r1, port_l1_r1, port_g0_r1, port_g1_r1;
    always @ (posedge clk) begin
    	port_l0_r1 <= port_l0_1;
    	port_l1_r1 <= port_l1_1;
    	port_g0_r1 <= port_g0_1;
    	port_g1_r1 <= port_g1_1;
    end
   	assign port_l0_o = port_l0_r1;
    assign port_l1_o = port_l1_r1;
    assign port_g0_o = port_g0_r1;
    assign port_g1_o = port_g1_r1;

endmodule

