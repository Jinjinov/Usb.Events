FROM arm64v8/ubuntu

RUN apt-get update

#RUN apt-get install -y build-essential

RUN apt-get install -y libudev-dev

#RUN apt-get install -y libudev-dev:armhf

COPY Linux/UsbEventWatcher.Linux.c .

#RUN gcc -march=armv7-a -o UsbEventWatcher.Linux_arm.so -shared -fPIC UsbEventWatcher.Linux.c -ludev

#RUN apt-get install -y libudev-dev:arm64

RUN gcc -march=armv8-a -o UsbEventWatcher.Linux_arm64.so -shared -fPIC UsbEventWatcher.Linux.c -ludev

COPY entrypoint.sh .
RUN chmod +x entrypoint.sh
CMD entrypoint.sh