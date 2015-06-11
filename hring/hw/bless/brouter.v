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
    reg     `control_w  port0_c1, port1_c1, port2_c1, port3_c1, port4_c1;
    reg     `data_w     port0_d1, port1_d1, port2_d1, port3_d1, port4_d1;
    wire    `control_w  port0_c2, port1_c2, port2_c2, port3_c2, port4_c2;
    reg     `data_w     port0_d2, port1_d2, port2_d2, port3_d2, port4_d2;

    // Routing Matrices
    wire    `rmatrix_w  rmatrix0, rmatrix1, rmatrix2, rmatrix3, rmatrix4;

    reg `rmatrix_w rmatrix0_r, rmatrix1_r, rmatrix2_r, rmatrix3_r, rmatrix4_r;

    // Final Route
    wire    `routecfg_w route_config;

    /************* STAGE 1 *************/
    // Route Computation
    RouteCompute rc0(.control_in(port0_cin),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .rst(rst),
                     .resource_go(resource_go0),
                     .rmatrix(rmatrix0));

    RouteCompute rc1(.control_in(port1_cin),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .rst(rst),
                     .resource_go(resource_go1),
                     .rmatrix(rmatrix1));

    RouteCompute rc2(.control_in(port2_cin),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .rst(rst),
                     .resource_go(resource_go2),
                     .rmatrix(rmatrix2));

    RouteCompute rc3(.control_in(port3_cin),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .rst(rst),
                     .resource_go(resource_go3),
                     .rmatrix(rmatrix3));

    RouteCompute rc4(.control_in(port4_cin),
                     .addrx(addrx),
                     .addry(addry),
                     .addrx_max(max_addrx),
                     .addry_max(max_addry),
                     .clk(clk),
                     .rst(rst),
                     .resource_go(),
                     .rmatrix(rmatrix4));
   
    // Registers for the data/control for the first stage
    always @ (posedge clk) begin
        port0_c1 <= port0_cin;
        port1_c1 <= port1_cin;
        port2_c1 <= port2_cin;
        port3_c1 <= port3_cin;
        port4_c1 <= port4_cin;
        port0_d1 <= port0_di;
        port1_d1 <= port1_di;
        port2_d1 <= port2_di;
        port3_d1 <= port3_di;
        port4_d1 <= port4_di;

        $display("RC in: %04x %04x %04x %04x", port0_cin, port1_cin, port2_cin, port3_cin);
        $display("RC out: %04x %04x %04x %04x", rmatrix0, rmatrix1, rmatrix2, rmatrix3);

        rmatrix0_r <= rmatrix0;
        rmatrix1_r <= rmatrix1;
        rmatrix2_r <= rmatrix2;
        rmatrix3_r <= rmatrix3;
        rmatrix4_r <= rmatrix4;
    end

    // Let the resource know stuff is incoming
    assign all_valid =  port0_cin[`valid_f] & 
                        port1_cin[`valid_f] & 
                        port2_cin[`valid_f] &
                        port3_cin[`valid_f];
    
    assign port4_ready =  ~(all_valid) |
                            resource_go0 |
                            resource_go1 |
                            resource_go2 |
                            resource_go3;

    /******** Stage 2 ********/
    arbitor arb(.rmatrix0(rmatrix0_r),
                .rmatrix1(rmatrix1_r),
                .rmatrix2(rmatrix2_r),
                .rmatrix3(rmatrix3_r),
                .rmatrix4(rmatrix4_r),
                .control0_in(port0_c1),
                .control1_in(port1_c1),
                .control2_in(port2_c1),
                .control3_in(port3_c1),
                .control4_in(port4_c1),
                .clk(clk),
                .rst(rst),
                .control0_out(port0_c2),
                .control1_out(port1_c2),
                .control2_out(port2_c2),
                .control3_out(port3_c2),
                .control4_out(port4_c2),
                .route_config(route_config));

    // Registers for data/control for stage 2
    always @ (posedge clk) begin
        port0_d2 <= port0_d1;
        port1_d2 <= port1_d1;
        port2_d2 <= port2_d1;
        port3_d2 <= port3_d1;
        port4_d2 <= port4_d1;

        $display("ARB out: %04x %04x %04x %04x (route %x)", port0_c2, port1_c2, port2_c2, port3_c2, route_config);
    end

    /*********** Stage 3 **********/
    crossbar xbar(.control0_in(port0_c2),
                  .control1_in(port1_c2),
                  .control2_in(port2_c2),
                  .control3_in(port3_c2),
                  .control4_in(port4_c2),
                  .data0_in(port0_d2),
                  .data1_in(port1_d2),
                  .data2_in(port2_d2),
                  .data3_in(port3_d2),
                  .data4_in(port4_d2),
                  .route_config(route_config),
                  .clk(clk),
                  .rst(rst),
                  .control0_out(port0_co),
                  .control1_out(port1_co),
                  .control2_out(port2_co),
                  .control3_out(port3_co),
                  .control4_out(port4_co),
                  .data0_out(port0_do),
                  .data1_out(port1_do),
                  .data2_out(port2_do),
                  .data3_out(port3_do),
                  .data4_out(port4_do));
    
endmodule


