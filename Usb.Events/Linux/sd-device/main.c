#include "UsbEventWatcher.Linux.h"
#include <stdio.h>
#include <pthread.h>

void OnInserted(struct UsbDeviceData usbDevice)
{
    printf("Inserted: %s %s \n", usbDevice.DeviceName, usbDevice.DeviceSystemPath);
    printf("Data: \n\tProduct:%s \n\tProduct Description:%s \n\tProduct ID:%s \n\tSerial Number:%s \n\tVendor: %s \n\tVendor Description: %s \n\tVendor ID:%s \n",
           usbDevice.Product != NULL ? usbDevice.Product : "",
           usbDevice.ProductDescription != NULL ? usbDevice.ProductDescription : "",
           usbDevice.ProductID != NULL ? usbDevice.ProductID : "",
           usbDevice.SerialNumber != NULL ? usbDevice.SerialNumber : "",
           usbDevice.Vendor != NULL ? usbDevice.Vendor : "",
           usbDevice.VendorDescription != NULL ? usbDevice.VendorDescription : "",
           usbDevice.VendorID != NULL ? usbDevice.VendorID : "");
}

void OnRemoved(struct UsbDeviceData usbDevice)
{
    printf("Removed: %s %s \n", usbDevice.DeviceName, usbDevice.DeviceSystemPath);
    printf("Data: \n\tProduct:%s \n\tProduct Description:%s \n\tProduct ID:%s \n\tSerial Number:%s \n\tVendor: %s \n\tVendor Description: %s \n\tVendor ID:%s \n",
           usbDevice.Product != NULL ? usbDevice.Product : "",
           usbDevice.ProductDescription != NULL ? usbDevice.ProductDescription : "",
           usbDevice.ProductID != NULL ? usbDevice.ProductID : "",
           usbDevice.SerialNumber != NULL ? usbDevice.SerialNumber : "",
           usbDevice.Vendor != NULL ? usbDevice.Vendor : "",
           usbDevice.VendorDescription != NULL ? usbDevice.VendorDescription : "",
           usbDevice.VendorID != NULL ? usbDevice.VendorID : "");
}

void *StartWatcher(void *arg)
{
    StartLinuxWatcher(OnInserted, OnRemoved, 0);
    pthread_exit(NULL);
}

int main(void)
{
    pthread_t thread;
    int result;

    printf("USB events:\n");

    result = pthread_create(&thread, NULL, StartWatcher, NULL);

    if (result != 0)
    {
        printf("Error creating the thread. Exiting program.\n");
        return -1;
    }

    getchar();

    StopLinuxWatcher();

    pthread_join(thread, NULL);

    return 0;
}