# $Id: Makefile 1853 2010-03-24 03:06:21Z dub $

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
