DIRS = Proc Net Common Memory Controller
SRC = $(foreach dir,$(DIRS), $(shell find $(dir) -name '*.cs'))

ifeq ($(DEBUG),1)
FLAGS=-d:DEBUG
else
FLAGS=
endif

.PHONY: all
all: bin/sim.exe 

bin/sim.exe: $(SRC)
	gmcs -r:Mono.Posix -debug -unsafe $(FLAGS) -out:$@ $^

.PHONY: clean
clean:
	rm -rf bin/sim.exe bin/sim.exe.mdb $(shell find . -name '*~' -o -name '*.pyc')

.PHONY: run
run: bin/sim.exe
	(cd bin/; mono --debug sim.exe config.txt)

.PHONY: arch
arch:
	tar zcvf code.tar.gz --exclude 'Common/gzip/*' Proc/ Net/ Common/
