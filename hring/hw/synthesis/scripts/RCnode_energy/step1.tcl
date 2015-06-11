###### WORKSPACE #######
file mkdir ./work
define_design_lib WORK -path ./work
 
###### TECHNOLOGY #######
source ../libtech_65nm.tcl
set hdlin_auto_save_templates 1

###### SOURCE FILES+DESIGN #######
analyze -format verilog -library WORK ../../../RingClustered/nodeRouter.v
analyze -format verilog -library WORK ../../../RingClustered/defines.v
analyze -format verilog -library WORK ../../../RingClustered/ejector.v
analyze -format verilog -library WORK ../../../RingCLustered/injector.v

set power_preserve_rtl_hier_names true

elaborate nodeRouter -library WORK

current_design nodeRouter

rtl2saif -output RCnode_forward.saif -design nodeRouter

write -hierarchy -format ddc -output RCnode_elab.ddc

quit
