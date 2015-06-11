#!/bin/sh
COMMON="brouter.v defines.v arbitor.v crossbar.v priority_comp.v route_compute.v"

for x in 0 1 2 3 4; do
    iverilog $COMMON tb$x.v tb_wrapper.v
    echo --------
    echo $x
    echo --------
    ./a.out
done

rm a.out
