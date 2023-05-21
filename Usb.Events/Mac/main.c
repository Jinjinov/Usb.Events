#include "UsbEventWatcher.Mac.h"
#include <stdio.h>

void OnInserted(UsbDeviceData usbDevice)
{
    printf("Inserted: %s \n", usbDevice.DeviceName);
}

void OnRemoved(UsbDeviceData usbDevice)
{
    printf("Removed: %s \n", usbDevice.DeviceName);
}

int main()
{
    printf("USB events: \n");

    StartMacWatcher(OnInserted, OnRemoved);

    return 0;
}
