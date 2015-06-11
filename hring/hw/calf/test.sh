#!/bin/sh
COMMON="brouter.v defines.v ejector.v injector.v route_compute.v sortnet.v xbar.v"

for x in 0 1 2 3 4; do
    iverilog $COMMON tb$x.v
    echo --------
    echo $x
    echo --------
    ./a.out
done

rm a.out
