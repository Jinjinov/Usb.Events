#!/bin/bash

#gcc -march=armv7-a -o /root/UsbEventWatcher.Linux_arm.so -shared -fPIC /root/UsbEventWatcher.Linux.c -ludev

#gcc -march=armv8-a -o /root/UsbEventWatcher.Linux_arm64.so -shared -fPIC /root/UsbEventWatcher.Linux.c -ludev

#mkdir -p /arm /arm64
#cp /root/UsbEventWatcher.Linux_arm.so /arm/UsbEventWatcher.Linux.so
cp /root/UsbEventWatcher.Linux_arm64.so /arm64/UsbEventWatcher.Linux.so
cp /root/UsbEventWatcher.Linux_arm64.so /root/UsbEventWatcher.Linux.so

#exec "$@"