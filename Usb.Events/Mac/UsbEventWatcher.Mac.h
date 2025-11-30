#ifndef USB_EVENT_WATCHER_MAC_H
#define USB_EVENT_WATCHER_MAC_H

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

// macOS Functions

void GetMacMountPoint(const char* syspath, MountPointCallback mountPointCallback);

void StartMacWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback);

void StopMacWatcher(void);

#ifdef __cplusplus
}
#endif

#endif /* USB_EVENT_WATCHER_MAC_H */
