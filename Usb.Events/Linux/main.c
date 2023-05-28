#include "UsbEventWatcher.Linux.h"
#include <stdio.h>
#include <pthread.h>

void OnInserted(UsbDeviceData usbDevice)
{
    printf("Inserted: %s %s \n", usbDevice.DeviceName, usbDevice.DeviceSystemPath);
}

void OnRemoved(UsbDeviceData usbDevice)
{
    printf("Removed: %s %s \n", usbDevice.DeviceName, usbDevice.DeviceSystemPath);
}

void *StartWatcher(void *arg)
{
    StartLinuxWatcher(OnInserted, OnRemoved, 0);

    pthread_exit(NULL);
}

int main()
{
    pthread_t thread;

    printf("USB events: \n");

    int result = pthread_create(&thread, NULL, StartWatcher, NULL);
    
    if (result != 0) {
        printf("Error creating the thread. Exiting program.\n");
        return -1;
    }

    getchar();

    StopLinuxWatcher();

    pthread_join(thread, NULL);

    return 0;
}
