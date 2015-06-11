FILES=*.rpt *.log *.syn *.pvl *.mr
XST_DIR = ./xst_runs
timestamp = `date +%F_%H%M`

.PHONY: all
all: calf bless_age buffered

define xst_compile
@echo Compiling for xst
mkdir -p ${XST_DIR}/$(strip $(1))_${timestamp}
cp -r $(2)/* ${XST_DIR}/$(strip $(1))_${timestamp}
./scripts/compile_xst.sh ${XST_DIR}/$(strip $(1))_${timestamp} $(strip $(1))
endef  

bless_age:
	dc_shell-xg-t -f scripts/bless_age.tcl

calf:
	dc_shell-xg-t -f scripts/calf.tcl

buffered:
	dc_shell-xg-t -f scripts/buffered.tcl

simplebuf:
	dc_shell-xg-t -f scripts/simplebuf.tcl

clean:
	rm -rf *.rpt *.log *.syn *.pvl *.mr *.svf work
