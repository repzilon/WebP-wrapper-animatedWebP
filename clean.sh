#! /bin/sh

for d in */bin */obj; do
	rm -rf $d
	echo "Deleted $d"
done
rm -v .DS_Store */.DS_Store
