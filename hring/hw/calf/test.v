module test();
  reg clk;
  initial clk = 0;
  always begin
    clk = 1;
    #1;
    clk = 0;
    #1;
  end
  reg [15:0] pseudorand;
  always @(posedge clk)
    pseudorand <= { pseudorand[14:0], pseudorand[7] ^ pseudorand[3] };
  initial pseudorand = 0;

  always @(posedge clk) begin
    $display("pseuorand: %d", pseudorand);
  end

endmodule
