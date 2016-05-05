#!/bin/sh
# Usage: countFails.sh path/to/cvf

printf "%s: " "$1"
FOO=`./starling.sh $1 |
	   grep "fail" |
	   cut -d":" -f1 |
	   xargs`
echo $FOO