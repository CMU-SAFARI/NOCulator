# $Id$

all: 
	for dir in $(wildcard *); do \
          if [ -d $$dir ] ; then \
	    $(MAKE) -C $$dir $@ ; \
	  fi ; \
        done

runall:
	for dir in $(wildcard *); do \
          if [ -d $$dir ] ; then \
	    cd $$dir ; \
	    ./simv ; \
	    cd ..  ; \
	  fi \
        done

clean:
	for dir in $(wildcard *); do \
          if [ -d $$dir ] ; then \
	    $(MAKE) -C $$dir $@ ; \
	  fi ; \
        done

realclean:
	for dir in $(wildcard *); do \
          if [ -d $$dir ] ; then \
	    $(MAKE) -C $$dir $@ ; \
	  fi ; \
        done
