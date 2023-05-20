#ifndef USB_EVENT_WATCHER_MAC_H
#define USB_EVENT_WATCHER_MAC_H

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

// macOS Functions

void GetMacMountPoint(const char* syspath, MountPointCallback mountPointCallback);

void StartMacWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback);

void StopMacWatcher();

#ifdef __cplusplus
}
#endif

#endif /* USB_EVENT_WATCHER_MAC_H */
