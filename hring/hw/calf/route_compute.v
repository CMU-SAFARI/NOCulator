`include "defines.v"

module RouteCompute(
    input       `control_w  control_in, 
    input       `addrx_w    addrx,
    input       `addry_w    addry,
    input       `addrx_w    addrx_max,
    input       `addry_w    addry_max,
    output      `rmatrix_w  rmatrix);

    // Break out control signals
    wire                valid;
    wire    `addrx_w    destx;
    wire    `addry_w    desty;

    /******************* 
     * Control In Format
     * [12]     Valid
     * [11:8]   Seq
     * [7:4]    Source
     * [3:0]    Dest
     *******************/
    assign valid = control_in[`valid_f];
    assign destx = control_in[`destx_f];
    assign desty = control_in[`desty_f];

    /*************** 
     * rmatrix format
     * |West|East|South|North|
     * | 3  | 2  | 1   |  0  |
     * A result of 0 indicates that we are at our destination
     ***************/
    assign rmatrix = 
        valid ? 
        {destx < addrx, destx > addrx, desty > addry, desty < addry} :
        5'b0000;

//    always @(rmatrix)
//        $display("routecompute: control %04x rmatrix %x", control_in, rmatrix);

endmodule

