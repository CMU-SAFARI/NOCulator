#! /usr/bin/env python

import sys
import math

#Command line arguments
file_name = sys.argv[1]         # The output file name
datawidth = int(sys.argv[2])    # Width of the data part of the flit
agewidth = int(sys.argv[3])     # Width of the age field
seqwidth = int(sys.argv[4])     # Width of the sequence field
maxaddrx = int(sys.argv[5])     # Maximum x address
maxaddry = int(sys.argv[6])     # Maximum y address

addrx_bits = int(math.floor(math.log(maxaddrx, 2)) + 1)
addry_bits = int(math.floor(math.log(maxaddry, 2)) + 1)

print("addrx_bits" + str(addrx_bits))
print("addry_bits" + str(addry_bits))

link_bits = addrx_bits * 2 + addry_bits * 2 + agewidth + seqwidth + 1
age_l = 0
age_h = agewidth - 1
desty_l = age_h + 1
desty_h = desty_l + addry_bits - 1
destx_l = desty_h + 1
destx_h = destx_l + addrx_bits - 1
src_l = destx_h + 1
src_h = src_l + addrx_bits + addry_bits - 1
seq_l = src_h + 1
seq_h = seq_l + seqwidth - 1
valid = seq_h + 1

addry_l = 0
addry_h = addry_l + addry_bits - 1
addrx_l = addry_h + 1
addrx_h = addrx_l + addrx_bits - 1

# Open the file for writing, it will erase all old contents
f = open(file_name, 'w')

######## Start writing to the file #########
# Generate the header
f.write("/**\n")
f.write(" * defines.v\n *\n")
f.write(" * @author Kevin Woo <kwoo@cmu.edu>\n *\n")
f.write(" * These are the parameters for the router. Please use the script\n")
f.write(" * to generate this file as it will be more reliable\n")
f.write(" */\n\n")

f.write("// Bitwidth of the routing matrix. Should not change these unless\n")
f.write("// there are more than 4 ports to the router not including the resource\n")
f.write("`define rmatrix_n\t\t4\t// Number of bits in the rmatrix\n")
f.write("`define rmatrix_w\t\t[`rmatrix_n-1:0]\n")
f.write("`define rmatrix_north\t0\t// Bit position of \"north\"\n")
f.write("`define rmatrix_south\t1\t// Bit position of \"south\"\n")
f.write("`define rmatrix_east\t2\t// Bit position of \"east\"\n")
f.write("`define rmatrix_west\t3\t// Bit position of \"west\"\n")
f.write("`define rmatrix_ns\t1:0\t// South, North field\n")
f.write("`define rmatrix_ew\t3:2\t// West, East field\n")
f.write("\n")

f.write("// Bitwidth of the assignment matrix. Should not change unless there\n")
f.write("// are more than 6 input/output ports to the router including the resource\n")
f.write("`define amatrix_n\t5\t// Number of bits in the amatrix\n")
f.write("`define amatrix_w\t[`amatrix_n-1:0]\n")
f.write("\n")

f.write("// Bitwidth of the control line between each router. Fields are:\n")
f.write("// [Valid][Seq][Src][Dest][Age]\n")
f.write("`define control_n\t" + str(link_bits) + "\t// Number of bits in control field\n")
f.write("`define control_w\t[" + str(link_bits-1) + ":0]\n")
f.write("`define valid_f\t" + str(valid) + "\n")
f.write("`define seq_f\t" + str(seq_h) + ":" + str(seq_l) + "\t// Bit position of sequence number\n")
f.write("`define seq_n\t" + str(seqwidth) + "\n")
f.write("`define src_f\t" + str(src_h) + ":" + str(src_l) + "\t// Bit position of source\n")
f.write("`define src_w\t[" + str(src_h - src_l) + ":0]\n")
f.write("`define dest_f\t" + str(destx_h) + ":" + str(desty_l) + "\n")
f.write("`define destx_f\t" + str(destx_h) + ":" + str(destx_l) + "\n")
f.write("`define desty_f\t" + str(desty_h) + ":" + str(desty_l) + "\n")
f.write("`define age_f\t" + str(age_h) + ":" + str(age_l) + "\n")
f.write("`define age_w\t[" + str(age_h) + ":" + str(age_l) + "]\n")
f.write("`define age_n\t" + str(agewidth) + "\n")
f.write("\n")

f.write("// These define the address bit widths\n")
f.write("`define addr_n\t" + str(addrx_bits + addry_bits) + "\t// Number of bits in an address\n")
f.write("`define addrx_f\t" + str(addrx_h) + ":" + str(addrx_l) + "\t// Bit position of the X field in the address\n")
f.write("`define addrx_w\t[" + str(addrx_bits - 1) + ":0]\t// Bit width of the X field in the address\n")
f.write("`define addry_f\t" + str(addry_h) + ":" + str(addry_l) + "\t// Bit position of the Y field in the address\n")
f.write("`define addry_w\t[" + str(addry_bits - 1) + ":0]\t// Bit width of the Y field in the address\n")
f.write("`define addrx_max\t" + str(addrx_bits) + "'d" + str(maxaddrx) + "\n")
f.write("`define addry_max\t" + str(addry_bits) + "'d" + str(maxaddry) + "\n")
f.write("\n")

f.write("// Width of the data portion of the router interconnect\n")
f.write("`define data_n\t" + str(datawidth) + "\n")
f.write("`define data_w\t[" + str(datawidth-1) + ":0]\n")
f.write("\n")

f.write("// These define the route configuration signal between the arbitor and\n")
f.write("// crossbar. Do not change these unless you change the number of ports\n")
f.write("// in the router. This configuration assmes 5 I/O ports.\n")
f.write("`define routecfg_bn\t3\t// Number of bits each port uses in the config\n")
f.write("`define routecfg_n\t15\t// Nuber of bits in the route config\n")
f.write("`define routecfg_w\t[14:0]\n")
f.write("`define routecfg_4\t14:12\t// Field for port 4 (resource)\n")
f.write("`define routecfg_3\t11:9\t// Field for port 3 (west)\n")
f.write("`define routecfg_2\t8:6\t// Field for port 2 (east)\n")
f.write("`define routecfg_1\t5:3\t// Field for port 1 (south)\n")
f.write("`define routecfg_0\t2:0\t// Field for port 0 (north)\n")

