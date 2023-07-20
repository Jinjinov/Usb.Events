FROM arm64v8/ubuntu

RUN apt-get update

RUN apt-get install -y gcc libudev-dev

COPY Linux/UsbEventWatcher.Linux.c .

#RUN gcc -march=armv7-a -o UsbEventWatcher.Linux.so -shared -fPIC UsbEventWatcher.Linux.c -ludev

RUN gcc -march=armv8-a -o UsbEventWatcher.Linux.so -shared -fPIC UsbEventWatcher.Linux.c -ludev

COPY entrypoint.sh .
RUN chmod +x entrypoint.sh
CMD ./entrypoint.sh