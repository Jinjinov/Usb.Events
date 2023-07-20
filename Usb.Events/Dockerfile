FROM arm64v8/ubuntu

RUN apt-get update

RUN apt-get install -y build-essential

RUN apt-get install -y libudev-dev

#RUN apt-get install -y libudev-dev:armhf

COPY Linux/UsbEventWatcher.Linux.c /root/

#RUN gcc -march=armv7-a -o /root/UsbEventWatcher.Linux_arm.so -shared -fPIC /root/UsbEventWatcher.Linux.c -ludev

#RUN apt-get install -y libudev-dev:arm64

RUN gcc -march=armv8-a -o /root/UsbEventWatcher.Linux_arm64.so -shared -fPIC /root/UsbEventWatcher.Linux.c -ludev

RUN mkdir -p /arm /arm64

COPY entrypoint.sh /root/
RUN chmod +x /root/entrypoint.sh

CMD ["/root/entrypoint.sh"]