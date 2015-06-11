###### WORKSPACE #######
file mkdir ./work
define_design_lib WORK -path ./work
 
###### TECHNOLOGY #######
source ../libtech_65nm.tcl
set hdlin_auto_save_templates 1

###### SOURCE FILES+DESIGN #######
analyze -format verilog -library WORK ../../../calf/brouter.v
analyze -format verilog -library WORK ../../../calf/xbar.v
analyze -format verilog -library WORK ../../../calf/defines.v
analyze -format verilog -library WORK ../../../calf/route_compute.v
analyze -format verilog -library WORK ../../../calf/ejector.v
analyze -format verilog -library WORK ../../../calf/injector.v
analyze -format verilog -library WORK ../../../calf/sortnet.v

set power_preserve_rtl_hier_names true

elaborate brouter -library WORK

current_design brouter

rtl2saif -output calf_forward.saif -design brouter

write -hierarchy -format ddc -output calf_elab.ddc

quit
