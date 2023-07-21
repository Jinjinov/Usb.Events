#!/bin/bash

args_array=("$@")
for i in "${args_array[@]}"
do
  :
  echo "### Got variable $i ###"
done
echo "args_count = $#"

# Validate the number of arguments (exactly two arguments expected)
if [ $# -ne 2 ]; then
  echo "Usage: $0 <TargetArch> <BuildType>"
  exit 1
fi

# Set the target architecture and build type based on the arguments
target_arch="$1"
build_type="$2"

# Validate the target architecture argument
if [ "$target_arch" != "arm" ] && [ "$target_arch" != "arm64" ]; then
  echo "Invalid TargetArch argument. Use 'arm' or 'arm64'."
  exit 1
fi

# Validate the build type argument
if [ "$build_type" != "Debug" ] && [ "$build_type" != "Release" ]; then
  echo "Invalid BuildType argument. Use 'Debug' or 'Release'."
  exit 1
fi

# Determine the target architecture-specific gcc options
if [ "$target_arch" = "arm" ]; then
  gcc_arch="-march=armv7-a+fp"
else
  gcc_arch="-march=armv8-a"
fi

# Determine the gcc flags based on build type
if [ "$build_type" = "Debug" ]; then
  gcc_flags="-shared -g -D DEBUG"
else
  gcc_flags="-shared"
fi

# Execute the gcc command with the selected architecture and flags
gcc $gcc_arch $gcc_flags UsbEventWatcher.Linux.c -o UsbEventWatcher.Linux.so -ludev -fPIC