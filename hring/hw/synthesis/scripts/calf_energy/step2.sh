#!/bin/bash

. ../setup_bash

for x in 0 1 2 3 4; do

rm -rf simlib
vlib simlib
vmap work simlib

cp ../../../calf/defines.v .
vlog ../../../calf/xbar.v
vlog ../../../calf/defines.v
vlog ../../../calf/route_compute.v
vlog ../../../calf/ejector.v
vlog ../../../calf/injector.v
vlog ../../../calf/sortnet.v
vlog ../../../calf/brouter.v
vlog ../../../calf/tb$x.v
rm -f defines.v

vsim -c -do step2.do

done
