# Begin_DVE_Session_Save_Info
# DVE full session
# Saved on Thu Nov 5 19:01:33 2009
# Designs open: 1
#   Sim: /home/dub/work/router/trunk/verif/tc_chan_test_mac/simv
# Toplevel windows open: 2
# 	TopLevel.1
# 	TopLevel.2
#   Source.1: testbench
#   Wave.1: 144 signals
#   Group count = 3
#   Group Group1 signal count = 33
#   Group Group2 signal count = 96
#   Group Group3 signal count = 15
# End_DVE_Session_Save_Info

# DVE version: C-2009.06_Full64
# DVE build date: May 19 2009 21:20:39


#<Session mode="Full" path="/home/dub/work/router/trunk/verif/tc_chan_test_mac/iolist.tcl" type="Debug">

gui_set_loading_session_type Post
gui_continuetime_set

# Close design
if { [gui_sim_state -check active] } {
    gui_sim_terminate
    gui_sim_wait terminated
}
gui_close_db -all
gui_expr_clear_all

# Close all windows
gui_close_window -type Console
gui_close_window -type Wave
gui_close_window -type Source
gui_close_window -type Schematic
gui_close_window -type Data
gui_close_window -type DriverLoad
gui_close_window -type List
gui_close_window -type Memory
gui_close_window -type HSPane
gui_close_window -type DLPane
gui_close_window -type Assertion
gui_close_window -type CovHier
gui_close_window -type CoverageTable
gui_close_window -type CoverageMap
gui_close_window -type CovDensity
gui_close_window -type CovDetail
gui_close_window -type Local
gui_close_window -type Watch
gui_close_window -type Grading
gui_close_window -type Group



# Application preferences
gui_set_pref_value -key app_default_font -value {Helvetica,10,-1,5,50,0,0,0,0,0}
gui_src_preferences -tabstop 8 -maxbits 24 -windownumber 1
#<WindowLayout>

# DVE Topleve session: 


# Create and position top-level windows :TopLevel.1

if {![gui_exist_window -window TopLevel.1]} {
    set TopLevel.1 [ gui_create_window -type TopLevel \
       -icon $::env(DVE)/auxx/gui/images/toolbars/dvewin.xpm] 
} else { 
    set TopLevel.1 TopLevel.1
}
gui_show_window -window ${TopLevel.1} -show_state normal -rect {{682 362} {1790 1011}}

# ToolBar settings
gui_set_toolbar_attributes -toolbar {TimeOperations} -dock_state top
gui_set_toolbar_attributes -toolbar {TimeOperations} -offset 0
gui_show_toolbar -toolbar {TimeOperations}
gui_set_toolbar_attributes -toolbar {&File} -dock_state top
gui_set_toolbar_attributes -toolbar {&File} -offset 0
gui_show_toolbar -toolbar {&File}
gui_set_toolbar_attributes -toolbar {&Edit} -dock_state top
gui_set_toolbar_attributes -toolbar {&Edit} -offset 0
gui_show_toolbar -toolbar {&Edit}
gui_set_toolbar_attributes -toolbar {Simulator} -dock_state top
gui_set_toolbar_attributes -toolbar {Simulator} -offset 0
gui_show_toolbar -toolbar {Simulator}
gui_set_toolbar_attributes -toolbar {Signal} -dock_state top
gui_set_toolbar_attributes -toolbar {Signal} -offset 0
gui_show_toolbar -toolbar {Signal}
gui_set_toolbar_attributes -toolbar {&Scope} -dock_state top
gui_set_toolbar_attributes -toolbar {&Scope} -offset 0
gui_show_toolbar -toolbar {&Scope}
gui_set_toolbar_attributes -toolbar {&Trace} -dock_state top
gui_set_toolbar_attributes -toolbar {&Trace} -offset 0
gui_show_toolbar -toolbar {&Trace}
gui_set_toolbar_attributes -toolbar {BackTrace} -dock_state top
gui_set_toolbar_attributes -toolbar {BackTrace} -offset 0
gui_show_toolbar -toolbar {BackTrace}
gui_set_toolbar_attributes -toolbar {&Window} -dock_state top
gui_set_toolbar_attributes -toolbar {&Window} -offset 0
gui_show_toolbar -toolbar {&Window}
gui_set_toolbar_attributes -toolbar {Zoom} -dock_state top
gui_set_toolbar_attributes -toolbar {Zoom} -offset 0
gui_show_toolbar -toolbar {Zoom}
gui_set_toolbar_attributes -toolbar {Zoom And Pan History} -dock_state top
gui_set_toolbar_attributes -toolbar {Zoom And Pan History} -offset 0
gui_show_toolbar -toolbar {Zoom And Pan History}

# End ToolBar settings

# Docked window settings
set HSPane.1 [gui_create_window -type HSPane -parent ${TopLevel.1} -dock_state left -dock_on_new_line true -dock_extent 546]
set Hier.1 [gui_share_window -id ${HSPane.1} -type Hier]
gui_set_window_pref_key -window ${HSPane.1} -key dock_width -value_type integer -value 546
gui_set_window_pref_key -window ${HSPane.1} -key dock_height -value_type integer -value 256
gui_set_window_pref_key -window ${HSPane.1} -key dock_offset -value_type integer -value 0
gui_update_layout -id ${HSPane.1} {{left 0} {top 0} {width 545} {height 256} {show_state normal} {dock_state left} {dock_on_new_line true} {child_hier_colhier 416} {child_hier_coltype 149} {child_hier_col1 0} {child_hier_col2 1}}
set Console.1 [gui_create_window -type Console -parent ${TopLevel.1} -dock_state bottom -dock_on_new_line true -dock_extent 261]
gui_set_window_pref_key -window ${Console.1} -key dock_width -value_type integer -value 1109
gui_set_window_pref_key -window ${Console.1} -key dock_height -value_type integer -value 261
gui_set_window_pref_key -window ${Console.1} -key dock_offset -value_type integer -value 0
gui_update_layout -id ${Console.1} {{left 0} {top 0} {width 1108} {height 260} {show_state normal} {dock_state bottom} {dock_on_new_line true}}
#### Start - Readjusting docked view's offset / size
set dockAreaList { top left right bottom }
foreach dockArea $dockAreaList {
  set viewList [gui_ekki_get_window_ids -active_parent -dock_area $dockArea]
  foreach view $viewList {
      if {[lsearch -exact [gui_get_window_pref_keys -window $view] dock_width] != -1} {
        set dockWidth [gui_get_window_pref_value -window $view -key dock_width]
        set dockHeight [gui_get_window_pref_value -window $view -key dock_height]
        set offset [gui_get_window_pref_value -window $view -key dock_offset]
        if { [string equal "top" $dockArea] || [string equal "bottom" $dockArea]} {
          gui_set_window_attributes -window $view -dock_offset $offset -width $dockWidth
        } else {
          gui_set_window_attributes -window $view -dock_offset $offset -height $dockHeight
        }
      }
  }
}
#### End - Readjusting docked view's offset / size
gui_sync_global -id ${TopLevel.1} -option true

# MDI window settings
set DLPane.1 [gui_create_window -type {DLPane}  -parent ${TopLevel.1}]
if {[gui_get_shared_view -id ${DLPane.1} -type Data] == {}} {
        set Data.1 [gui_share_window -id ${DLPane.1} -type Data]
} else {
        set Data.1  [gui_get_shared_view -id ${DLPane.1} -type Data]
}

gui_show_window -window ${DLPane.1} -show_state maximized
gui_update_layout -id ${DLPane.1} {{show_state maximized} {dock_state undocked} {dock_on_new_line false} {child_data_colvariable 232} {child_data_colvalue 173} {child_data_coltype 153} {child_data_col1 0} {child_data_col2 1} {child_data_col3 2} {dataShowMode detail} {max_item_length 50}}
set Source.1 [gui_create_window -type {Source}  -parent ${TopLevel.1}]
gui_show_window -window ${Source.1} -show_state maximized
gui_update_layout -id ${Source.1} {{show_state maximized} {dock_state undocked} {dock_on_new_line false}}

# End MDI window settings


# Create and position top-level windows :TopLevel.2

if {![gui_exist_window -window TopLevel.2]} {
    set TopLevel.2 [ gui_create_window -type TopLevel \
       -icon $::env(DVE)/auxx/gui/images/toolbars/dvewin.xpm] 
} else { 
    set TopLevel.2 TopLevel.2
}
gui_show_window -window ${TopLevel.2} -show_state normal -rect {{2 48} {1919 1144}}

# ToolBar settings
gui_set_toolbar_attributes -toolbar {TimeOperations} -dock_state top
gui_set_toolbar_attributes -toolbar {TimeOperations} -offset 0
gui_show_toolbar -toolbar {TimeOperations}
gui_set_toolbar_attributes -toolbar {&File} -dock_state top
gui_set_toolbar_attributes -toolbar {&File} -offset 0
gui_show_toolbar -toolbar {&File}
gui_set_toolbar_attributes -toolbar {&Edit} -dock_state top
gui_set_toolbar_attributes -toolbar {&Edit} -offset 0
gui_show_toolbar -toolbar {&Edit}
gui_set_toolbar_attributes -toolbar {Simulator} -dock_state top
gui_set_toolbar_attributes -toolbar {Simulator} -offset 0
gui_show_toolbar -toolbar {Simulator}
gui_set_toolbar_attributes -toolbar {Signal} -dock_state top
gui_set_toolbar_attributes -toolbar {Signal} -offset 0
gui_show_toolbar -toolbar {Signal}
gui_set_toolbar_attributes -toolbar {&Scope} -dock_state top
gui_set_toolbar_attributes -toolbar {&Scope} -offset 0
gui_show_toolbar -toolbar {&Scope}
gui_set_toolbar_attributes -toolbar {&Trace} -dock_state top
gui_set_toolbar_attributes -toolbar {&Trace} -offset 0
gui_show_toolbar -toolbar {&Trace}
gui_set_toolbar_attributes -toolbar {BackTrace} -dock_state top
gui_set_toolbar_attributes -toolbar {BackTrace} -offset 0
gui_show_toolbar -toolbar {BackTrace}
gui_set_toolbar_attributes -toolbar {&Window} -dock_state top
gui_set_toolbar_attributes -toolbar {&Window} -offset 0
gui_show_toolbar -toolbar {&Window}
gui_set_toolbar_attributes -toolbar {Zoom} -dock_state top
gui_set_toolbar_attributes -toolbar {Zoom} -offset 0
gui_show_toolbar -toolbar {Zoom}
gui_set_toolbar_attributes -toolbar {Zoom And Pan History} -dock_state top
gui_set_toolbar_attributes -toolbar {Zoom And Pan History} -offset 0
gui_show_toolbar -toolbar {Zoom And Pan History}

# End ToolBar settings

# Docked window settings
gui_sync_global -id ${TopLevel.2} -option true

# MDI window settings
set Wave.1 [gui_create_window -type {Wave}  -parent ${TopLevel.2}]
gui_show_window -window ${Wave.1} -show_state maximized
gui_update_layout -id ${Wave.1} {{show_state maximized} {dock_state undocked} {dock_on_new_line false} {child_wave_left 556} {child_wave_right 1356} {child_wave_colname 276} {child_wave_colvalue 276} {child_wave_col1 0} {child_wave_col2 1}}

# End MDI window settings

gui_set_env TOPLEVELS::TARGET_FRAME(Source) ${TopLevel.1}
gui_set_env TOPLEVELS::TARGET_FRAME(Schematic) ${TopLevel.1}
gui_set_env TOPLEVELS::TARGET_FRAME(PathSchematic) ${TopLevel.1}
gui_set_env TOPLEVELS::TARGET_FRAME(Wave) none
gui_set_env TOPLEVELS::TARGET_FRAME(List) none
gui_set_env TOPLEVELS::TARGET_FRAME(Memory) ${TopLevel.1}
gui_set_env TOPLEVELS::TARGET_FRAME(DriverLoad) none
gui_update_statusbar_target_frame ${TopLevel.1}
gui_update_statusbar_target_frame ${TopLevel.2}

#</WindowLayout>

#<Database>

# DVE Open design session: 

if { [llength [lindex [gui_get_db -design Sim] 0]] == 0 } {
gui_set_env SIMSETUP::SIMARGS {{-ucligui }}
gui_set_env SIMSETUP::SIMEXE {/home/dub/work/router/trunk/verif/tc_chan_test_mac/simv}
gui_set_env SIMSETUP::ALLOW_POLL {0}
if { ![gui_is_db_opened -db {/home/dub/work/router/trunk/verif/tc_chan_test_mac/simv}] } {
gui_sim_run Ucli -exe simv -args {-ucligui } -dir /home/dub/work/router/trunk/verif/tc_chan_test_mac -nosource
}
}
if { ![gui_sim_state -check active] } {error "Simulator did not start correctly" error}
gui_set_precision 1ns
gui_set_time_units 1ns
#</Database>

# DVE Global setting session: 


# Global: Breakpoints

# Global: Bus

# Global: Expressions

# Global: Signal Time Shift

# Global: Signal Compare

# Global: Signal Groups
set {Group1} {Group1}
gui_sg_create ${Group1}
gui_sg_addsignal -group ${Group1} { {testbench.clk} {testbench.reset} {testbench.io_node_addr_base} {testbench.io_write} {testbench.io_read} {testbench.io_addr} {testbench.io_write_data} {testbench.io_read_data} {testbench.io_done} {testbench.error} {testbench.nctl_cfg_node_addrs} {testbench.cfg_req} {testbench.cfg_write} {testbench.cfg_addr} {testbench.cfg_write_data} {testbench.cfg_read_data} {testbench.cfg_done} {testbench.node_ctrl} {testbench.node_status} {testbench.force_node_reset_b} {testbench.node_reset} {testbench.node_clk_en} {testbench.node_clk} {testbench.force_chan_reset_b} {testbench.chan_reset} {testbench.chan_clk_en} {testbench.chan_clk} {testbench.ctest_cfg_node_addrs} {testbench.xmit_cal} {testbench.xmit_data} {testbench.recv_cal} {testbench.recv_data} {testbench.ctest_error} }
set {Group2} {Group2}
gui_sg_create ${Group2}
gui_sg_addsignal -group ${Group2} { {testbench.ctest.clk} {testbench.ctest.reset} {testbench.ctest.cfg_node_addrs} {testbench.ctest.cfg_req} {testbench.ctest.cfg_write} {testbench.ctest.cfg_addr} {testbench.ctest.cfg_write_data} {testbench.ctest.cfg_read_data} {testbench.ctest.cfg_done} {testbench.ctest.xmit_cal} {testbench.ctest.xmit_data} {testbench.ctest.recv_cal} {testbench.ctest.recv_data} {testbench.ctest.error} {testbench.ctest.ifc_active} {testbench.ctest.ifc_req} {testbench.ctest.ifc_write} {testbench.ctest.ifc_node_addr_match} {testbench.ctest.ifc_reg_addr} {testbench.ctest.ifc_write_data} {testbench.ctest.ifc_read_data} {testbench.ctest.ifc_done} {testbench.ctest.ifc_sel_node} {testbench.ctest.do_write} {testbench.ctest.ifc_sel_ctrl} {testbench.ctest.ifc_sel_status} {testbench.ctest.ifc_sel_errors} {testbench.ctest.ifc_sel_test_duration} {testbench.ctest.ifc_sel_warmup_duration} {testbench.ctest.ifc_sel_cal_interval} {testbench.ctest.ifc_sel_cal_duration} {testbench.ctest.ifc_sel_pattern_addr} {testbench.ctest.ifc_sel_pattern_data} {testbench.ctest.ifc_read_data_status} {testbench.ctest.ifc_read_data_errors} {testbench.ctest.write_ctrl} {testbench.ctest.active_s} {testbench.ctest.active_q} {testbench.ctest.preset_s} {testbench.ctest.preset_q} {testbench.ctest.cal_en_s} {testbench.ctest.cal_en_q} {testbench.ctest.start_s} {testbench.ctest.start_q} {testbench.ctest.running_s} {testbench.ctest.running_q} {testbench.ctest.ignore_errors} {testbench.ctest.errors} {testbench.ctest.errors_s} {testbench.ctest.errors_q} {testbench.ctest.test_duration_loadval} {testbench.ctest.test_duration_zero} {testbench.ctest.test_duration_inf} {testbench.ctest.test_duration_q} {testbench.ctest.test_duration_next} {testbench.ctest.write_test_duration} {testbench.ctest.test_duration_s} {testbench.ctest.warmup_duration_loadval} {testbench.ctest.warmup_duration_zero} {testbench.ctest.warmup_duration_q} {testbench.ctest.warmup_duration_next} {testbench.ctest.write_warmup_duration} {testbench.ctest.warmup_duration_s} {testbench.ctest.cal_interval_loadval} {testbench.ctest.write_cal_interval} {testbench.ctest.cal_interval_s} {testbench.ctest.cal_interval_q} {testbench.ctest.cal_duration_loadval} {testbench.ctest.write_cal_duration} {testbench.ctest.cal_duration_s} {testbench.ctest.cal_duration_q} {testbench.ctest.pattern_addr_loadval} {testbench.ctest.write_pattern_addr} {testbench.ctest.pattern_addr_s} {testbench.ctest.pattern_addr_q} {testbench.ctest.cal_ctr_q} {testbench.ctest.cal_ctr_max} {testbench.ctest.cal_ctr_next} {testbench.ctest.cal_ctr_s} {testbench.ctest.cal_ctr_zero} {testbench.ctest.cal_duration_ext} {testbench.ctest.cal_duration_minus_one} {testbench.ctest.cal_duration_minus_two} {testbench.ctest.tx_cal_set} {testbench.ctest.tx_cal_reset} {testbench.ctest.tx_cal_s} {testbench.ctest.tx_cal_q} {testbench.ctest.invalid_data_s} {testbench.ctest.invalid_data_q} {testbench.ctest.rx_cal_set} {testbench.ctest.rx_cal_reset} {testbench.ctest.rx_cal_s} {testbench.ctest.rx_cal_q} {testbench.ctest.pattern_data_loadval} {testbench.ctest.write_pattern_data} {testbench.ctest.ref_data} }
set {Group3} {Group3}
gui_sg_create ${Group3}
gui_sg_addsignal -group ${Group3} { {testbench.chan.clk} {testbench.chan.reset} {testbench.chan.xmit_cal} {testbench.chan.xmit_data} {testbench.chan.recv_cal} {testbench.chan.recv_data} {testbench.chan.xmit_data_s} {testbench.chan.xmit_data_q} {testbench.chan.channel_data_in} {testbench.chan.seed} {testbench.chan.i} {testbench.chan.channel_errors} {testbench.chan.channel_data_out} {testbench.chan.recv_data_s} {testbench.chan.recv_data_q} }
gui_set_radix -radix {decimal} -signals {testbench.chan.seed}
gui_set_radix -radix {twosComplement} -signals {testbench.chan.seed}
gui_set_radix -radix {decimal} -signals {testbench.chan.i}
gui_set_radix -radix {twosComplement} -signals {testbench.chan.i}

# Global: Highlighting

# Post database loading setting...

# Restore C1 time
gui_set_time -C1_only 11616



# Save global setting...

# Wave/List view global setting
gui_cov_show_value -switch false

# Close all empty TopLevel windows
foreach __top [gui_ekki_get_window_ids -type TopLevel] {
    if { [llength [gui_ekki_get_window_ids -parent $__top]] == 0} {
        gui_close_window -window $__top
    }
}
gui_set_loading_session_type noSession
# DVE View/pane content session: 


# Hier 'Hier.1'
gui_list_set_filter -id ${Hier.1} -list { {Package 1} {All 1} {Process 1} {UnnamedProcess 1} {Function 1} {Block 1} {OVA Unit 1} {LeafScCell 1} {LeafVlgCell 1} {Interface 1} {LeafVhdCell 1} {NamedBlock 1} {Task 1} {DollarUnit 1} {VlgPackage 1} {ClassDef 1} }
gui_list_set_filter -id ${Hier.1} -text {*}
gui_hier_list_init -id ${Hier.1}
gui_change_design -id ${Hier.1} -design Sim
catch {gui_list_expand -id ${Hier.1} testbench}
catch {gui_list_select -id ${Hier.1} {testbench.chan}}
gui_view_scroll -id ${Hier.1} -vertical -set 0
gui_view_scroll -id ${Hier.1} -horizontal -set 0

# Data 'Data.1'
gui_list_set_filter -id ${Data.1} -list { {Buffer 1} {Input 1} {Others 1} {Linkage 1} {Output 1} {Parameter 1} {All 1} {Aggregate 1} {Event 1} {Assertion 1} {Constant 1} {Interface 1} {Signal 1} {$unit 1} {Inout 1} {Variable 1} }
gui_list_set_filter -id ${Data.1} -text {*}
gui_list_show_data -id ${Data.1} {testbench.chan}
gui_show_window -window ${Data.1}
catch { gui_list_select -id ${Data.1} {testbench.chan.clk testbench.chan.reset testbench.chan.xmit_cal testbench.chan.xmit_data testbench.chan.recv_cal testbench.chan.recv_data testbench.chan.xmit_data_s testbench.chan.xmit_data_q testbench.chan.channel_data_in testbench.chan.seed testbench.chan.i testbench.chan.channel_errors testbench.chan.channel_data_out testbench.chan.recv_data_s testbench.chan.recv_data_q }}
gui_view_scroll -id ${Data.1} -vertical -set 0
gui_view_scroll -id ${Data.1} -horizontal -set 0
gui_view_scroll -id ${Hier.1} -vertical -set 0
gui_view_scroll -id ${Hier.1} -horizontal -set 0

# Source 'Source.1'
gui_src_value_annotate -id ${Source.1} -switch false
gui_set_env TOGGLE::VALUEANNOTATE 0
gui_open_source -id ${Source.1}  -replace -active testbench /home/dub/work/router/trunk/verif/tc_chan_test_mac/testbench.v
gui_view_scroll -id ${Source.1} -vertical -set 384
gui_src_set_reusable -id ${Source.1}

# View 'Wave.1'
gui_wv_sync -id ${Wave.1} -switch false
set groupExD [gui_get_pref_value -category Wave -key exclusiveSG]
gui_set_pref_value -category Wave -key exclusiveSG -value {false}
set origWaveHeight [gui_get_pref_value -category Wave -key waveRowHeight]
gui_list_set_height -id Wave -height 25
set origGroupCreationState [gui_list_create_group_when_add -wave]
gui_list_create_group_when_add -wave -disable
gui_marker_set_ref -id ${Wave.1}  C1
gui_wv_zoom_timerange -id ${Wave.1} 11410 11819
gui_list_set_filter -id ${Wave.1} -list { {Buffer 1} {Input 1} {Others 1} {Linkage 1} {Output 1} {Parameter 1} {All 1} {Aggregate 1} {Event 1} {Assertion 1} {Constant 1} {Interface 1} {Signal 1} {$unit 1} {Inout 1} {Variable 1} }
gui_list_set_filter -id ${Wave.1} -text {*}
gui_list_add_group -id ${Wave.1} -after {New Group} {{Group1}}
gui_list_add_group -id ${Wave.1} -after {New Group} {{Group2}}
gui_list_add_group -id ${Wave.1} -after {New Group} {{Group3}}
gui_list_select -id ${Wave.1} {testbench.ctest.ref_data }
gui_list_set_insertion_bar  -id ${Wave.1} -group {Group3}  -position in
gui_seek_criteria -id ${Wave.1} {Any Edge}



gui_set_env TOGGLE::DEFAULT_WAVE_WINDOW ${Wave.1}
gui_set_pref_value -category Wave -key exclusiveSG -value $groupExD
gui_list_set_height -id Wave -height $origWaveHeight
if {$origGroupCreationState} {
	gui_list_create_group_when_add -wave -enable
}
if { $groupExD } {
 gui_msg_report -code DVWW028
}
gui_marker_move -id ${Wave.1} {C1} 11616
gui_view_scroll -id ${Wave.1} -vertical -set 2826

# DVE Active view and window setting: 

gui_set_active_window -window ${DLPane.1}
gui_set_active_window -window ${DLPane.1}
gui_set_active_window -window ${Wave.1}
gui_set_active_window -window ${Wave.1}
# Restore toplevel window zorder
# The toplevel window could be closed if it has no view/pane
if {[gui_exist_window -window ${TopLevel.1}]} {
	gui_set_active_window -window ${TopLevel.1} }
if {[gui_exist_window -window ${TopLevel.2}]} {
	gui_set_active_window -window ${TopLevel.2} }
#</Session>

