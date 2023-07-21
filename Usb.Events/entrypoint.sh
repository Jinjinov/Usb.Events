#!/bin/bash

# Validate the number of arguments
if [ $# -ne 1 ]; then
  echo "Usage: $0 <BuildType>"
  exit 1
fi

# Set the build type based on the argument
build_type="$1"

# Validate the build type argument
if [ "$build_type" != "Debug" ] && [ "$build_type" != "Release" ]; then
  echo "Invalid BuildType argument. Use 'Debug' or 'Release'."
  exit 1
fi

# Perform the build based on the build type
if [ "$build_type" = "Debug" ]; then
  gcc_flags="-shared -g -D DEBUG"
else
  gcc_flags="-shared"
fi

# Execute the gcc command
gcc -march=armv8-a -o UsbEventWatcher.Linux.so "$gcc_flags" -fPIC UsbEventWatcher.Linux.c -ludev


echo "Started compiling"

#gcc -march=armv7-a -o UsbEventWatcher.Linux.so -shared -fPIC UsbEventWatcher.Linux.c -ludev

#gcc -march=armv8-a -o UsbEventWatcher.Linux.so -shared -fPIC UsbEventWatcher.Linux.c -ludev

echo "Finished compiling"

#exec "$@"