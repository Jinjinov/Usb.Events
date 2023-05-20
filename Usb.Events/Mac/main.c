#include "UsbEventWatcher.Mac.h"

void InsertedCallback(UsbDeviceData usbDevice)
{
}

void RemovedCallback(UsbDeviceData usbDevice)
{
}

int main()
{
    StartMacWatcher(InsertedCallback, RemovedCallback);

    return 0;
}
