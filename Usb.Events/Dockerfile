# Dockerfile
FROM arm64v8/ubuntu

# Update package manager repositories
RUN apt-get update

RUN apt-get install -y build-essential

#RUN apt-get install -y gcc-multilib

RUN apt-get install -y libudev-dev

# Install dependencies for ARMv7
#RUN apt-get install -y libudev-dev:armhf

# Copy C code to the container
COPY Linux/UsbEventWatcher.Linux.c /root/

# Compile C code for ARMv7 target
#RUN gcc -march=armv7-a -o /root/UsbEventWatcher.Linux_arm.so -shared -fPIC /root/UsbEventWatcher.Linux.c -ludev

# Install dependencies for ARMv8
#RUN apt-get install -y libudev-dev:arm64

# Compile C code for ARMv8 target
RUN gcc -march=armv8-a -o /root/UsbEventWatcher.Linux_arm64.so -shared -fPIC /root/UsbEventWatcher.Linux.c -ludev

# Copy the compiled shared libraries to their respective directories
RUN mkdir -p /arm /arm64

COPY entrypoint.sh /root/
RUN chmod +x /root/entrypoint.sh

CMD ["/root/entrypoint.sh"]