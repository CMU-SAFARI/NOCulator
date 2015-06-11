#!/bin/bash

. ../setup_bash

for x in 0 1 2 3 4; do

rm -rf simlib
vlib simlib
vmap work simlib

cp ../../../bless_age/defines.v .
vlog ../../../bless_age/age.v
vlog ../../../bless_age/arbitor.v
vlog ../../../bless_age/crossbar.v
vlog ../../../bless_age/data.v
vlog ../../../bless_age/priority_comp.v
vlog ../../../bless_age/route_compute.v
vlog ../../../bless_age/brouter.v
vlog ../../../bless_age/tb$x.v
rm -f defines.v

vsim -c -do step2.do

done
