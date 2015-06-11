`include "defines.v"

// Do not touch these otherwise the implementation will break
`define dir_east    1'b1
`define dir_west    1'b0
`define dir_north   1'b0
`define dir_south   1'b1
`define wrap_offset 1'b1

module RouteCompute(
    input       `control_w  control_in, 
    input       `addrx_w    addrx,
    input       `addry_w    addry,
    input       `addrx_w    addrx_max,
    input       `addry_w    addry_max,
    input                   clk,
    input                   rst,
    output                  resource_go,
    output      `rmatrix_w  rmatrix);

    // Break out control signals
    wire                valid;
    wire    `addrx_w    destx;
    wire    `addry_w    desty;
    wire    `age_w      age;

    /******************* 
     * Control In Format
     * [12]     Valid
     * [11:8]   Source
     * [7:4]    Dest
     * [3:0]    Age
     *******************/
    assign valid = control_in[`valid_f];
    assign destx = control_in[`destx_f];
    assign desty = control_in[`desty_f];
    assign age = control_in[`age_f];


    // Tell resource that we are not sending to it
    assign resource_go = ((addrx == destx) && (addry == desty)) ? 1'b1 : 1'b0;

    // Temporary registers for calculations
    reg     `addrx_w    resultx;
    reg     `addry_w    resulty;

    // Assign our actual output
    assign rmatrix = {resultx, resulty};

    always @ (posedge clk) begin
        if (rst) begin
            resultx <= 0;
            resulty <= 0;
        end else if (valid) begin
            /*************** 
             * rmatrix format
             * |West|East|South|North|
             * | 3  | 2  | 1   |  0  |
             * A result of 0 indicates that we are at our destination
             ***************/
            // We are at our destination
            if ((destx == addrx)  && (desty == addry)) begin
                resultx <= 2'b00;
                resulty <= 2'b00;
            end else begin
                // Choose which direction to take in the x direction
                if (destx > addrx) begin
                    resultx <= 2'b01;        // East
                end else if (destx < addrx) begin
                    resultx <= 2'b10;        // West
                end

                if (desty > addry) begin
                    resulty <= 2'b10;        // South
                end else if (desty < addry) begin
                    resulty <= 2'b01;        // North
                end
            end
        // Invalid
        end else begin
            resultx <= 2'b00;
            resulty <= 2'b00;
        end
    end
endmodule

