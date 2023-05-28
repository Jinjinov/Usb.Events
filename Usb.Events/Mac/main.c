#include "UsbEventWatcher.Mac.h"
#include <stdio.h>
#include <pthread.h>

void OnInserted(UsbDeviceData usbDevice)
{
    printf("Inserted: %s \n", usbDevice.DeviceName);
}

void OnRemoved(UsbDeviceData usbDevice)
{
    printf("Removed: %s \n", usbDevice.DeviceName);
}

void *StartWatcher(void *arg)
{
    StartMacWatcher(OnInserted, OnRemoved);

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

    StopMacWatcher();

    pthread_join(thread, NULL);

    return 0;
}
