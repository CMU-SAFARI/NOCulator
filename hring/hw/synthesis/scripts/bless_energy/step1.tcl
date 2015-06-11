###### WORKSPACE #######
file mkdir ./work
define_design_lib WORK -path ./work
 
###### TECHNOLOGY #######
source ../libtech_65nm.tcl
set hdlin_auto_save_templates 1

###### SOURCE FILES+DESIGN #######
analyze -format verilog -library WORK ../../../bless_age/age.v
analyze -format verilog -library WORK ../../../bless_age/arbitor.v
analyze -format verilog -library WORK ../../../bless_age/crossbar.v
analyze -format verilog -library WORK ../../../bless_age/data.v
analyze -format verilog -library WORK ../../../bless_age/defines.v
analyze -format verilog -library WORK ../../../bless_age/priority_comp.v
analyze -format verilog -library WORK ../../../bless_age/route_compute.v
analyze -format verilog -library WORK ../../../bless_age/brouter.v

set power_preserve_rtl_hier_names true

elaborate brouter -library WORK

current_design brouter

rtl2saif -output bless_forward.saif -design brouter

write -hierarchy -format ddc -output bless_elab.ddc

quit
