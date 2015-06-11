#!/usr/bin/python

def timing(r):
  l = filter(lambda x: x.find("data arrival time") != -1, open(r + '_timing.report').readlines())[0]
  return l.split()[-1]
def power(r):
  l = filter(lambda x: x.find("Total Dynamic Power") != -1, open(r + '_power.report').readlines())[0]
  return ' '.join(l.split()[-3:-1])
def area(r):
  l = filter(lambda x: x.find("Total cell area") != -1, open(r + '_area.report').readlines())[0]
  return l.split()[-1]

print "Router    Timing    Power         Area"
for router in ['buffered', 'simplebuf', 'bless_age', 'calf']:
  print "%-9s %6s ns  %12s  %15s um^2" % (router, timing(router), power(router), area(router))
