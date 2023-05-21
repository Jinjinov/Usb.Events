#include "UsbEventWatcher.Linux.h"
#include <stdio.h>

void OnInserted(UsbDeviceData usbDevice)
{
    printf("Inserted: %s %s \n", usbDevice.DeviceName, usbDevice.DeviceSystemPath);
}

void OnRemoved(UsbDeviceData usbDevice)
{
    printf("Removed: %s %s \n", usbDevice.DeviceName, usbDevice.DeviceSystemPath);
}

int main()
{
    printf("USB events: \n");

    StartLinuxWatcher(OnInserted, OnRemoved, 0);

    return 0;
}
