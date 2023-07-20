#!/bin/bash

echo "Started compiling"

#gcc -march=armv7-a -o UsbEventWatcher.Linux.so -shared -fPIC UsbEventWatcher.Linux.c -ludev

#gcc -march=armv8-a -o UsbEventWatcher.Linux.so -shared -fPIC UsbEventWatcher.Linux.c -ludev

echo "Finished compiling"

#exec "$@"