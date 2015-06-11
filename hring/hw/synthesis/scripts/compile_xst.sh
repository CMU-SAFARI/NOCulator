################################################################
# 
#     Quick and dirty script for running xst on Verilog
#     for quick LUT/BRAM estimation.
# 
#     First argument specifies the directory containing
#     all the source Verilog files.
#
#     Second argument specifies the top-level module
#
################################################################

VLOG_DIR=$1
PROJ_NAME=$2

## PARAMS ##


############


if [[ $# -eq 0 ]] ; then
  echo "two args needed: <vlog_dir> <top_module>"
  exit
fi

cd ${VLOG_DIR}

rm -f system_xst.scr
echo "run" >> system_xst.scr
echo "-opt_mode speed" >> system_xst.scr
#echo "-opt_mode area" >> system_xst.scr
#echo "-opt_level 2" >> system_xst.scr

# bee2
#echo "-p xc2vp30ff896-7" >> system_xst.scr
#echo "-p xc2vp70ff1704-6" >> system_xst.scr
#echo "-p xc2vp70ff1704-7" >> system_xst.scr

# xupv5
#echo "-fsm_extract NO" >> system_xst.scr
#echo "-p xc5vlx330t-2ff1738" >> system_xst.scr
#echo "-p xc5vlx155t-3ff1136" >> system_xst.scr
echo "-p xc5vlx110t-1ff1136" >> system_xst.scr
echo "-top $PROJ_NAME" >> system_xst.scr
echo "-ifmt MIXED" >> system_xst.scr
echo "-ifn system_xst.prj" >> system_xst.scr
echo "-ofn $PROJ_NAME.ngc" >> system_xst.scr
echo "-loop_iteration_limit 1000000" >> system_xst.scr
echo "-iobuf NO" >> system_xst.scr
echo "-tristate2logic NO" >> system_xst.scr
echo "-hierarchy_separator /" >> system_xst.scr
echo "-register_duplication yes" >> system_xst.scr
#echo "-work_lib $VLOG_DIR" >> system_xst.scr
#echo "-keep_hierarchy YES" >> system_xst.scr

echo "Done creating system_xst.scr"

rm -f system_xst.prj


for i in *.v; do
  echo "verilog work $i" >> system_xst.prj
done

echo "Done creating system_xst.prj"
xst -ifn system_xst.scr 

USER=`whoami`
cp ${VLOG_DIR}/$PROJ_NAME.ngc /home/$USER
#gdb xst 
