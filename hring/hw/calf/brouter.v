`include "defines.v"

module brouter
#(parameter     addr = 4'b0101)
(   
    input       `control_w  port0_ci,
    input       `control_w  port1_ci,
    input       `control_w  port2_ci,
    input       `control_w  port3_ci,
    input       `control_w  port4_ci,
    output                  port4_ack,
    input                   clk,
    input                   rst,
    output      `control_w  port0_co,
    output      `control_w  port1_co,
    output      `control_w  port2_co,
    output      `control_w  port3_co,
    output      `control_w  port4_co,
    output                  port4_ready);

    // Config
    wire    `addrx_w    addrx, max_addrx;
    wire    `addry_w    addry, max_addry;
    assign addrx = addr[`addrx_f];   // This nodes x address
    assign addry = addr[`addry_f];   // This nodes y address
    assign max_addrx = `addrx_max;
    assign max_addry = `addry_max;

    // inputs
    wire `control_w port0_c_0, port1_c_0, port2_c_0, port3_c_0, port4_c_0;

    assign port0_c_0 = (rst) ? `control_n'd0 : port0_ci;
    assign port1_c_0 = (rst) ? `control_n'd0 : port1_ci;
    assign port2_c_0 = (rst) ? `control_n'd0 : port2_ci;
    assign port3_c_0 = (rst) ? `control_n'd0 : port3_ci;
    assign port4_c_0 = (rst) ? `control_n'd0 : port4_ci;

    /************* STAGE 1 *************/

    reg `control_w port0_c_0_r, port1_c_0_r, port2_c_0_r, port3_c_0_r;
    always @(posedge clk) begin
        port0_c_0_r <= port0_c_0;
        port1_c_0_r <= port1_c_0;
        port2_c_0_r <= port2_c_0;
        port3_c_0_r <= port3_c_0;
    end

    wire `steer_w port0_c_1, port1_c_1, port2_c_1, port3_c_1;
    wire `steer_w port0_c_2, port1_c_2, port2_c_2, port3_c_2;
    wire `steer_w port4_c_2;

    ejector ej(
            .addr(addr),
            .c0(port0_c_0_r),
            .c1(port1_c_0_r),
            .c2(port2_c_0_r),
            .c3(port3_c_0_r),
            .c0_o(port0_c_1),
            .c1_o(port1_c_1),
            .c2_o(port2_c_1),
            .c3_o(port3_c_1),
            .c_out(port4_c_2));
    injector ij(
            .c0(port0_c_1),
            .c1(port1_c_1),
            .c2(port2_c_1),
            .c3(port3_c_1),
            .c0_o(port0_c_2),
            .c1_o(port1_c_2),
            .c2_o(port2_c_2),
            .c3_o(port3_c_2),
            .c_in(port4_ci),
            .used(port4_ack));

    /********** STAGE 2 *****************/

    reg `steer_w port0_c_2_r, port1_c_2_r, port2_c_2_r, port3_c_2_r, port4_c_2_r;
    always @(posedge clk) begin
        port0_c_2_r <= port0_c_2;
        port1_c_2_r <= port1_c_2;
        port2_c_2_r <= port2_c_2;
        port3_c_2_r <= port3_c_2;
    end

    wire `steer_w port0_c_3, port1_c_3, port2_c_3, port3_c_3;

    // Route Computation
    wire `rmatrix_w rmatrix0, rmatrix1, rmatrix2, rmatrix3;

    RouteCompute rc0(.control_in(port0_c_2_r `control_w ),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .rmatrix(rmatrix0));

    RouteCompute rc1(.control_in(port1_c_2_r `control_w ),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .rmatrix(rmatrix1));

    RouteCompute rc2(.control_in(port2_c_2_r `control_w ),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .rmatrix(rmatrix2));

    RouteCompute rc3(.control_in(port3_c_2_r `control_w ),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .rmatrix(rmatrix3));
  
    sortnet sn(
            .clk(clk),
            .rmatrix0(rmatrix0),
            .rmatrix1(rmatrix1),
            .rmatrix2(rmatrix2),
            .rmatrix3(rmatrix3),
            .control0_in(port0_c_2_r),
            .control1_in(port1_c_2_r),
            .control2_in(port2_c_2_r),
            .control3_in(port3_c_2_r),
            .control0_out(port0_c_3),
            .control1_out(port1_c_3),
            .control2_out(port2_c_3),
            .control3_out(port3_c_3));

    wire `steer_w port4_c_3 = port4_c_2_r;
    
    /******************* STAGE 3 ***********************/
    reg `steer_w port0_c_3_r, port1_c_3_r, port2_c_3_r, port3_c_3_r, port4_c_3_r;
    always @(posedge clk) begin
        port0_c_3_r <= port0_c_3;
        port1_c_3_r <= port1_c_3;
        port2_c_3_r <= port2_c_3;
        port3_c_3_r <= port3_c_3;
    end

    assign port0_co = port0_c_3_r;
    assign port1_co = port1_c_3_r;
    assign port2_co = port2_c_3_r;
    assign port3_co = port3_c_3_r;
    assign port4_co = port4_c_2;

endmodule
