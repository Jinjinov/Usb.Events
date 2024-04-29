#ifndef USB_EVENT_WATCHER_LINUX_H
#define USB_EVENT_WATCHER_LINUX_H

#ifdef __cplusplus
extern "C" {
#endif

// Structures

typedef struct {
    char DeviceName[255];
    char DeviceSystemPath[255];
    char Product[255];
    char ProductDescription[255];
    char ProductID[255];
    char SerialNumber[255];
    char Vendor[255];
    char VendorDescription[255];
    char VendorID[255];
} UsbDeviceData;

// Function Pointers

typedef void (*UsbDeviceCallback)(UsbDeviceData usbDevice);
typedef void (*MountPointCallback)(const char* mountPoint);

// Linux Functions

void GetLinuxMountPoint(const char* syspath, MountPointCallback mountPointCallback);

void StartLinuxWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback, int includeTTY);

void StopLinuxWatcher(void);

#ifdef __cplusplus
}
#endif

#endif /* USB_EVENT_WATCHER_LINUX_H */
