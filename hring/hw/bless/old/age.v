`include "defines.v"

module age_incr(   
    input       `control_w  control0_in,
    input       `control_w  control1_in,
    input       `control_w  control2_in,
    input       `control_w  control3_in,
    input       `control_w  control4_in,
    input                   control4_ready,
    input                   clk,
    output  reg `control_w  control0_out,
    output  reg `control_w  control1_out,
    output  reg `control_w  control2_out,
    output  reg `control_w  control3_out,
    output  reg `control_w  control4_out);

    
    wire [`age_f] age0, age1, age2, age3, age4;

    // Update the age
    assign age0 = control0_in[`age_f] + 1;
    assign age1 = control1_in[`age_f] + 1;
    assign age2 = control2_in[`age_f] + 1;
    assign age3 = control3_in[`age_f] + 1;
    assign age4 = control4_in[`age_f] + 1;

    always @(posedge clk) begin
        control0_out <= {control0_in[`control_n-1:`age_n], age0};
        control1_out <= {control1_in[`control_n-1:`age_n], age1};
        control2_out <= {control2_in[`control_n-1:`age_n], age2};
        control3_out <= {control3_in[`control_n-1:`age_n], age3};

        // We need to be able to kill the resource if it is not valid
        if (control4_ready == 1'b0) begin
            control4_out <= {1'b0, control4_in[`control_n-2:`age_n], age4};
        end else begin
            control4_out <= {control4_in[`control_n-1:`age_n], age4};
        end 
    end
endmodule
