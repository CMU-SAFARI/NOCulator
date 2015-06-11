set designer "Eric Chung";
set company "Carnegie Mellon University";  

########### ST65nm ############
set search_path {. ./usrlibs ./lib_code /afs/ece.cmu.edu/project/km_group/cad/pdk/ST_65/techFiles /afs/ece/support/synopsys/2004.06/share/image/usr/local/synopsys/2004.06/libraries/syn/}
set target_library {CORE65LPSVT_nom_1.10V_25C.db}
set symbol_library "CORE65LPSVT.sdb"
set synthetic_library {dw_foundation.sldb dw01.sldb dw02.sldb dw03.sldb dw04.sldb dw05.sldb dw06.sldb dw07.sldb dw08.sldb}
set link_library {"*" CORE65LPSVT_nom_1.10V_25C.db dw_foundation.sldb dw01.sldb dw02.sldb dw03.sldb dw04.sldb dw05.sldb dw06.sldb dw07.sldb dw08.sldb}
