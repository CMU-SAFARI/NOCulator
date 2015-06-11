# this projects source files
PROGRAM = bless
SOURCES = *.v


# some useful others that you may need to edit
IV=iverilog

VFLAGS = -g2

# name of this file
MF = Makefile


# ------------- Stuff you shouldn't have to change ------------------

main:	$(PROGRAM)
	@vvp $(PROGRAM) -vcp

$(PROGRAM): $(SOURCES)
	@echo compiling...
	@$(IV) -o $(PROGRAM) $(VFLAGS) $(SOURCES)

clean:
	@echo "Deleting temporary files..."
	@rm -f *~ "#*" $(OBJECTS) $(PROGRAM)

