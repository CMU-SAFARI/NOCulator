define_design_lib WORK -path ./work
 
###### TECHNOLOGY #######
source ../libtech_65nm.tcl
set hdlin_auto_save_templates 1


###### COMPILE #######

read_file -format ddc RCnode_elab.ddc

current_design nodeRouter

set_max_area 0
#ungroup -flatten -all
#set_wire_load_model -name area_0K
#set_wire_load_mode top

link
uniquify
compile_ultra

report_area > report/RCnode_area.report
report_timing > report/RCnode_timing.report


read_saif -input ./RCnode_backward_0.saif -instance_name tb/r
report_power > report/RCnode_power_0.report

read_saif -input ./RCnode_backward_1.saif -instance_name tb/r
report_power > report/RCnode_power_1.report

read_saif -input ./RCnode_backward_2.saif -instance_name tb/r
report_power > report/RCnode_power_2.report

read_saif -input ./RCnode_backward_3.saif -instance_name tb/r
report_power > report/RCnode_power_3.report

read_saif -input ./RCnode_backward_4.saif -instance_name tb/r
report_power > report/RCnode_power_4.report

quit
