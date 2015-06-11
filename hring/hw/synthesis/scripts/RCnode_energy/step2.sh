#!/bin/bash

. ../setup_bash

for x in 0 1 2; do

rm -rf simlib
vlib simlib
vmap work simlib

cp ../../../RingClustered/defines.v .
vlog ../../../RingClustered/defines.v
vlog ../../../RingClustered/ejector.v
vlog ../../../RingClustered/injector.v
vlog ../../../RingClustered/nodeRouter.v
vlog ../../../RingClustered/tb_node$x.v
rm -f defines.v

vsim -c -do step2.do

done
