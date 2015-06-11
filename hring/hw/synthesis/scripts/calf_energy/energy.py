#!/usr/bin/env python

powers = map(lambda x: float(x.split()[1]) / 1000, open('SUMMARY.txt').readlines())
time = 3 * (2e-9)

baseline_energy = powers[0] * time

nj = 1000*1000*1000

print nj * baseline_energy, 'nJ'
for n in [1,2,3,4]:
    print nj * (powers[n] - powers[0]) * time, 'nJ'
