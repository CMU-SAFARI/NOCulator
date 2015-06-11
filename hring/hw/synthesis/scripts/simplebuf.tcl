###### WORKSPACE #######
file mkdir ./work
define_design_lib WORK -path ./work
 
###### TECHNOLOGY #######
source ./scripts/libtech_65nm.tcl
set hdlin_auto_save_templates 1

###### SOURCE FILES+DESIGN #######
source ./scripts/simplebuf_flist.tcl
current_design buffered_router

###### CLOCKS #######
set CLK_PORT [get_ports clk]
set TMP1 [remove_from_collection [all_inputs] $CLK_PORT]
set TMP2 [remove_from_collection $TMP1 rst]
set INPUTS [remove_from_collection $TMP2 rst]

create_clock -period 2 [get_ports clk]
set_input_delay 0.2 -max -clock clk $INPUTS
set_output_delay 0.2 -max -clock clk [all_outputs]

###### OPTIMIZATIONS #######
set_max_area 0
#ungroup -flatten -all
set_wire_load_model -name area_0K
set_wire_load_mode top

###### COMPILE #######
link
uniquify
compile_ultra

write -format ddc -hierarchy -output simplebuf.ddc

report_area > simplebuf_area.report
report_timing > simplebuf_timing.report
report_power > simplebuf_power.report
quit
