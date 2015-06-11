`include "defines.v"
`include "injector.v"
`include "ejector.v"

module nodeRouter
#(parameter     addr = 4'b0010)
(   
    input       `control_w  port0_ci,
    input       `control_w  port1_ci,
    input       `control_w  portl0_ci,
    input       `control_w  portl1_ci,   
    output                  portl0_ack,
    output 					portl1_ack,
    input                   clk,
    input                   rst,
    output      `control_w  port0_co,
    output      `control_w  port1_co,
    output      `control_w  portl0_co,
    output      `control_w  portl1_co
);

    // Config
    wire    `addrx_w    addrx, max_addrx;
    wire    `addry_w    addry, max_addry;
    assign addrx = addr[`addrx_f];   // This nodes x address
    assign addry = addr[`addry_f];   // This nodes y address
    assign max_addrx = `addrx_max;
    assign max_addry = `addry_max;

    // inputs
    wire `control_w port0_c_0, port1_c_0, portl0_c_0, portl1_c_0;

    assign port0_c_0 = (rst) ? `control_n'd0 : port0_ci;
    assign port1_c_0 = (rst) ? `control_n'd0 : port1_ci;
    assign portl0_c_0 = (rst) ? `control_n'd0 : portl0_ci;
    assign portl1_c_0 = (rst) ? `control_n'd0 : portl1_ci;

    /************* STAGE 1 *************/	
	
    reg `control_w port0_c_0_r, port1_c_0_r, portl0_c_0_r, portl1_c_0_r; 
    always @(posedge clk) begin
        port0_c_0_r <= port0_c_0;
        port1_c_0_r <= port1_c_0;
        portl0_c_0_r <= portl0_c_0;
        portl1_c_0_r <= portl1_c_0;        
    end

    wire `steer_w port0_c_1, port1_c_1;

    ejector ej(
            .addr(addr),
            .c0(port0_c_0_r),
            .c1(port1_c_0_r),
            .c0_o(port0_c_1),
            .c1_o(port1_c_1),
            .c_ej0(portl0_co),
            .c_ej1(portl1_co));

            
    injector ij(
            .c0(port0_c_1),
            .c1(port1_c_1),
            .c0_o(port0_co),
            .c1_o(port1_co),
            .c_in0(portl0_ci),
            .c_in1(portl1_ci),
            .ack0(portl0_ack),
            .ack1(portl1_ack));

endmodule
