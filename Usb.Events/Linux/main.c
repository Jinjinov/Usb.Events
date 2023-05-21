#include "UsbEventWatcher.Linux.h"

void InsertedCallback(UsbDeviceData usbDevice)
{
}

void RemovedCallback(UsbDeviceData usbDevice)
{
}

int main()
{
    StartLinuxWatcher(InsertedCallback, RemovedCallback, 0);

    return 0;
}
