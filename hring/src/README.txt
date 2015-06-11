Command-line options for different router designs:

#########
# Torus
#########
-torus true

Note: If one wants to maintain the same bisection bandwidth as the baseline mesh
network, double the packet size (i.e., -router.dataPacketSize 8 -router.maxPacketSize
8).

#########
# Flattened Butterfly
#########
-fbfly true

#########
# CHIPPER
#########
-router.algorithm DR_FLIT_SWITCHED_CALF -edge_loop true -meshEjectTrial 2

meshEjectTrial is the width of ejection ports.


#########
# VC-Buffered
#########
-router.algorithm DR_AFC -afc_force true -afc_force_buffered true
-meshEjectTrial 2

#########
# HiRD
#########

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


#########
# Hierarchical Buffered Ring, infinite buffers:
#########
-topology BufRingNetworkMulti -bufrings_n 8 -bufrings_levels 3 -bufrings_inf_credit true

bufrings_n: number of virtual networks
bufrings_levels: number of hierarchies
bufrings_inf_credit: ideal buffers (no credits/no capacity limits

#########
# Hierarchical Buffered Ring, finite buffers:
#########
-topology BufRingNetworkMulti -bufrings_n 8 -bufrings_levels 3 -bufrings_inf_credit false -bufrings_L2G 8 -bufrings_G2L 4 -bufrings_localbuf 4 -bufrings_globalbuf 8


