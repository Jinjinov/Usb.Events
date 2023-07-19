#!/bin/bash

# Compile C code for ARMv7 target
#gcc -march=armv7-a -o /root/UsbEventWatcher.Linux_arm.so -shared -fPIC /root/UsbEventWatcher.Linux.c -ludev

# Compile C code for ARMv8 target
#gcc -march=armv8-a -o /root/UsbEventWatcher.Linux_arm64.so -shared -fPIC /root/UsbEventWatcher.Linux.c -ludev

cd root

ls

# Copy the compiled shared libraries to their respective directories
#mkdir -p /arm /arm64
#cp /root/UsbEventWatcher.Linux_arm.so /arm/UsbEventWatcher.Linux.so
cp /root/UsbEventWatcher.Linux_arm64.so /arm64/UsbEventWatcher.Linux.so  2>&1
cp /root/UsbEventWatcher.Linux_arm64.so /root/UsbEventWatcher.Linux.so  2>&1

# Run your desired command(s) here
# For example, you can execute your program that uses the shared libraries

echo "Hello World!"

#exec "$@"