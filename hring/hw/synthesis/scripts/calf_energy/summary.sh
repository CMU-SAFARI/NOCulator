#!/bin/sh
(for x in 0 1 2 3 4; do echo -n $x, ; grep "Total Dynamic Power" report/calf_power_$x.report | cut -d= -f2 | cut -d\( -f1 ; done) > SUMMARY.txt
