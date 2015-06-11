define_design_lib WORK -path ./work
 
###### TECHNOLOGY #######
source ../libtech_65nm.tcl
set hdlin_auto_save_templates 1


###### COMPILE #######

read_file -format ddc calf_elab.ddc

current_design brouter

set_max_area 0
#ungroup -flatten -all
#set_wire_load_model -name area_0K
#set_wire_load_mode top

link
uniquify
compile_ultra

report_area > report/calf_area.report
report_timing > report/calf_timing.report


read_saif -input ./calf_backward_0.saif -instance_name tb/r
report_power > report/calf_power_0.report

read_saif -input ./calf_backward_1.saif -instance_name tb/r
report_power > report/calf_power_1.report

read_saif -input ./calf_backward_2.saif -instance_name tb/r
report_power > report/calf_power_2.report

read_saif -input ./calf_backward_3.saif -instance_name tb/r
report_power > report/calf_power_3.report

read_saif -input ./calf_backward_4.saif -instance_name tb/r
report_power > report/calf_power_4.report

quit
