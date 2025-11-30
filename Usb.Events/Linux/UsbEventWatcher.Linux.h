#ifndef USB_EVENT_WATCHER_LINUX_H
#define USB_EVENT_WATCHER_LINUX_H

#ifdef __cplusplus
extern "C" {
#endif

// Structures

typedef struct {
    char DeviceName[512];
    char DeviceSystemPath[512];
    char Product[512];
    char ProductDescription[512];
    char ProductID[512];
    char SerialNumber[512];
    char Vendor[512];
    char VendorDescription[512];
    char VendorID[512];
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
