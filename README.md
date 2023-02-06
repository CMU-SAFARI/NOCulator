
# NOCulator

NOCulator is a network-on-chip simulator providing cycle-accurate performance models for a wide variety of networks (mesh, torus, ring, hierarchical rings, multi-rings, flattened butterfly) and routers (buffered, bufferless, Adaptive Flow Control, minBD, HiRD).

Please see below for command-line options.

# References

The following papers describe the networks already implemented by NOCulator.
Please cite the following papers if you use this simulator:

MinBD router architecture:
- Chris Fallin, Greg Nazario, Xiangyao Yu, Kevin Chang, Rachata Ausavarungnirun, Onur Mutlu.
MinBD: Minimally-Buffered Deflection Routing for Energy-Efficient Interconnect. NoCs 2012.
Paper (PDF): https://people.inf.ethz.ch/omutlu/pub/minimally-buffered-deflection-router_nocs12.pdf

HiRD router design and the hierarchical ring interconnect:
- Rachata Ausavarungnirun, Chris Fallin, Xiangyao Yu, Kevin Chang, Greg Nazario, Reetuparna Das, Gabriel H. Loh, Onur Mutlu,
"Design and Evaluation of Hierarchical Rings with Deflection Routing", SBAC-PAD 2014.
Paper (PDF): https://people.inf.ethz.ch/omutlu/pub/hierarchical-rings-with-deflection_sbacpad14.pdf

- Rachata Ausavarungnirun, Chris Fallin, Xiangyao Yu, Kevin Chang, Greg Nazario, Reetuparna Das, Gabriel Loh, and Onur Mutlu,
"A Case for Hierarchical Rings with Deflection Routing: An Energy-Efficient On-Chip Communication Substrate"
Parallel Computing (PARCO), 2016.
Paper (PDF): https://arxiv.org/pdf/1602.06005.pdf


# Usage

Command-line options for different router designs available in hring simulator:

# Torus
-torus true

Note: If one wants to maintain the same bisection bandwidth as the baseline mesh
network, double the packet size (i.e., -router.dataPacketSize 8 -router.maxPacketSize
8).

# Flattened Butterfly
-fbfly true

# CHIPPER
-router.algorithm DR_FLIT_SWITCHED_CALF -edge_loop true -meshEjectTrial 2

meshEjectTrial is the width of ejection ports.

CHIPPER is described in the following paper:

Chris Fallin, Chris Craik, and Onur Mutlu,
"CHIPPER: A Low-Complexity Bufferless Deflection Router"
Paper (PDF): https://people.inf.ethz.ch/omutlu/pub/chipper_hpca11.pdf


# VC-Buffered
-router.algorithm DR_AFC -afc_force true -afc_force_buffered true
-meshEjectTrial 2

# HiRD (Hierarchical Rings with Deflection Routing)

-topology XXXX

HR_4drop:
    4x4, each local ring has 1 bridge router to connect to the global
    ring (so totally 4 bridge routers(drops) connecting the global ring
    and its local rings)

HR_8drop:
    4x4, each local ring has 2 bridge router to connect to the global ring
    (8-bridge)

HR_16drop:
    4x4, each local ring has 4 bridge router to connect to the global ring
    (16-bridge)

HR_8_16drop:
    8x8, each local ring has 16 bridge router to connect to the global ring

HR_8_8drop:
    8x8, each local ring has 8 bridge router to connect to the global ring

HR_16_8drop:
    16x16, each local ring has 8 bridge router to connect to the global ring

HR_32_8drop:
    32x32, each local ring has 8 bridge router to connect to the global ring
    
    
HiRD is described in the following papers:

Rachata Ausavarungnirun, Chris Fallin, Xiangyao Yu, Kevin Chang, Greg Nazario, Reetuparna Das, Gabriel H. Loh, Onur Mutlu, "Design and Evaluation of Hierarchical Rings with Deflection Routing", SBAC-PAD 2014. Paper (PDF): https://people.inf.ethz.ch/omutlu/pub/hierarchical-rings-with-deflection_sbacpad14.pdf

Rachata Ausavarungnirun, Chris Fallin, Xiangyao Yu, Kevin Chang, Greg Nazario, Reetuparna Das, Gabriel Loh, and Onur Mutlu, "A Case for Hierarchical Rings with Deflection Routing: An Energy-Efficient On-Chip Communication Substrate" Parallel Computing (PARCO), 2016. Paper (PDF): https://arxiv.org/pdf/1602.06005.pdf



# Hierarchical Buffered Ring, infinite buffers:
-topology BufRingNetworkMulti -bufrings_n 8 -bufrings_levels 3 -bufrings_inf_credit true

bufrings_n: number of virtual networks
bufrings_levels: number of hierarchies
bufrings_inf_credit: ideal buffers (no credits/no capacity limits

# Hierarchical Buffered Ring, finite buffers:
-topology BufRingNetworkMulti -bufrings_n 8 -bufrings_levels 3 -bufrings_inf_credit false -bufrings_L2G 8 -bufrings_G2L 4 -bufrings_localbuf 4 -bufrings_globalbuf 8


