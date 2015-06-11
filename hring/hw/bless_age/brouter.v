`include "defines.v"

module brouter
#(parameter     addr = 4'b0101)
(   
    input       `control_w  port0_ci,
    input       `control_w  port1_ci,
    input       `control_w  port2_ci,
    input       `control_w  port3_ci,
    input       `control_w  port4_ci,
    input       `data_w     port0_di,
    input       `data_w     port1_di,
    input       `data_w     port2_di,
    input       `data_w     port3_di,
    input       `data_w     port4_di,
    input                   clk,
    input                   rst,
    output      `control_w  port0_co,
    output      `control_w  port1_co,
    output      `control_w  port2_co,
    output      `control_w  port3_co,
    output      `control_w  port4_co,
    output      `data_w     port0_do,
    output      `data_w     port1_do,
    output      `data_w     port2_do,
    output      `data_w     port3_do,
    output      `data_w     port4_do,
    output                  port4_ready);

    // Config
    wire    `addrx_w    addrx, max_addrx;
    wire    `addry_w    addry, max_addry;
    assign addrx = addr[`addrx_f];   // This nodes x address
    assign addry = addr[`addry_f];   // This nodes y address
    assign max_addrx = `addrx_max;
    assign max_addry = `addry_max;

    // Input wires for reset
    wire    `control_w  port0_cin, port1_cin, port2_cin, port3_cin, port4_cin;

    assign port0_cin = (rst) ? `control_n'd0 : port0_ci;
    assign port1_cin = (rst) ? `control_n'd0 : port1_ci;
    assign port2_cin = (rst) ? `control_n'd0 : port2_ci;
    assign port3_cin = (rst) ? `control_n'd0 : port3_ci;
    assign port4_cin = (rst) ? `control_n'd0 : port4_ci;
    
    // Resource Ready Wires
    wire    all_valid;
    wire    resource_go0, resource_go1, resource_go2, resource_go3;

    // Cross Stage Wires
    wire    `control_w  port0_c1, port1_c1, port2_c1, port3_c1, port4_c1;
    wire    `data_w     port0_d1, port1_d1, port2_d1, port3_d1, port4_d1;
    wire    `control_w  port0_c2, port1_c2, port2_c2, port3_c2, port4_c2;
    wire    `data_w     port0_d2, port1_d2, port2_d2, port3_d2, port4_d2;

    // Routing Matrices
    wire    `rmatrix_w  rmatrix0, rmatrix1, rmatrix2, rmatrix3, rmatrix4;

    // Final Route
    wire    `routecfg_w route_config;

    /************* STAGE 1 *************/
    reg `control_w port0_r, port1_r, port2_r, port3_r, port4_r;
    always @(posedge clk) begin
        port0_r <= port0_cin;
        port1_r <= port1_cin;
        port2_r <= port2_cin;
        port3_r <= port3_cin;
        port4_r <= port4_cin;
    end

    // Route Computation
    RouteCompute rc0(.control_in(port0_r),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .resource_go(resource_go0),
                     .rmatrix(rmatrix0));

    RouteCompute rc1(.control_in(port1_r),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .resource_go(resource_go1),
                     .rmatrix(rmatrix1));

    RouteCompute rc2(.control_in(port2_r),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .resource_go(resource_go2),
                     .rmatrix(rmatrix2));

    RouteCompute rc3(.control_in(port3_r),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .resource_go(resource_go3),
                     .rmatrix(rmatrix3));

    RouteCompute rc4(.control_in(port4_r),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .resource_go(),
                     .rmatrix(rmatrix4));
    
    age_incr age_s1 (.control0_in(port0_r),
                     .control1_in(port1_r),
                     .control2_in(port2_r),
                     .control3_in(port3_r),
                     .control4_in(port4_r),
                     .control4_ready(port4_ready),
                     .clk(clk),
                     .control0_out(port0_c1),
                     .control1_out(port1_c1),
                     .control2_out(port2_c1),
                     .control3_out(port3_c1),
                     .control4_out(port4_c1));

    //always @(*) $display("RC out: %x %x %x %x (rmat %x %x %x %x)", port0_c1, port1_c1, port2_c1, port3_c1, rmatrix0, rmatrix1, rmatrix2, rmatrix3);
    
   
    assign all_valid =  port0_cin[`valid_f] & 
                        port1_cin[`valid_f] & 
                        port2_cin[`valid_f] &
                        port3_cin[`valid_f];
    
    assign port4_ready =  ~(all_valid) |
                            resource_go0 |
                            resource_go1 |
                            resource_go2 |
                            resource_go3;

    data_buf data_s0(.data0_in(port0_di),
                     .data1_in(port1_di),
                     .data2_in(port2_di),
                     .data3_in(port3_di),
                     .data4_in(port4_di),
                     .clk(clk),
                     .data0_out(port0_d1),
                     .data1_out(port1_d1),
                     .data2_out(port2_d1),
                     .data3_out(port3_d1),
                     .data4_out(port4_d1));
 

    /******** Stage 2 ********/
    wire `routecfg_w route_config_unbuf;
    arbitor arb(.rmatrix0(rmatrix0),
                .rmatrix1(rmatrix1),
                .rmatrix2(rmatrix2),
                .rmatrix3(rmatrix3),
                .rmatrix4(rmatrix4),
                .control0_in(port0_c1),
                .control1_in(port1_c1),
                .control2_in(port2_c1),
                .control3_in(port3_c1),
                .control4_in(port4_c1),
                .clk(clk),
                .route_config_unbuf(route_config_unbuf),
                .route_config(route_config));

    ctl_xt cx(
        .control0_in(port0_c1),
        .control1_in(port1_c1),
        .control2_in(port2_c1),
        .control3_in(port3_c1),
        .control4_in(port4_c1),
        .route_config(route_config_unbuf),
        .clk(clk),
        .control0_out(port0_co),
        .control1_out(port1_co),
        .control2_out(port2_co),
        .control3_out(port3_co),
        .control4_out(port4_co));


    data_buf data_s1(.data0_in(port0_d1),
                     .data1_in(port1_d1),
                     .data2_in(port2_d1),
                     .data3_in(port3_d1),
                     .data4_in(port4_d1),
                     .clk(clk),
                     .data0_out(port0_d2),
                     .data1_out(port1_d2),
                     .data2_out(port2_d2),
                     .data3_out(port3_d2),
                     .data4_out(port4_d2));
 
    
    /*********** Stage 3 **********/
    crossbar xbar(.data0_in(port0_d2),
                  .data1_in(port1_d2),
                  .data2_in(port2_d2),
                  .data3_in(port3_d2),
                  .data4_in(port4_d2),
                  .route_config(route_config),
                  .clk(clk),
                  .data0_out(port0_do),
                  .data1_out(port1_do),
                  .data2_out(port2_do),
                  .data3_out(port3_do),
                  .data4_out(port4_do));
    
endmodule


