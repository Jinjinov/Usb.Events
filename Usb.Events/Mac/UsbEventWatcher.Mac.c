#include <CoreFoundation/CoreFoundation.h>

#include <IOKit/IOKitLib.h>
#include <IOKit/IOMessage.h>
#include <IOKit/IOCFPlugIn.h>
#include <IOKit/usb/IOUSBLib.h>

#ifdef __MAC_OS_X_VERSION_MAX_ALLOWED
#if __MAC_OS_X_VERSION_MAX_ALLOWED <= 120100
#define kIOMainPortDefault kIOMasterPortDefault
#endif
#endif

#include <DiskArbitration/DiskArbitration.h>
#include <DiskArbitration/DASession.h>

#include <signal.h>
#include <stdio.h>
#include <stdlib.h>

typedef struct UsbDeviceData
{
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

UsbDeviceData usbDevice;

static const struct UsbDeviceData empty;

typedef void (*UsbDeviceCallback)(UsbDeviceData usbDevice);
UsbDeviceCallback InsertedCallback;
UsbDeviceCallback RemovedCallback;

typedef void (*MountPointCallback)(const char* mountPoint);

char buffer[1024];

static IONotificationPortRef notificationPort;

void debug_print(const char* format, ...)
{
#ifdef DEBUG
    va_list args;
    va_start(args, format);
    vprintf(format, args);
    va_end(args);
#endif
}

void print_cfstringref(const char* prefix, CFStringRef cfVal)
{
#ifdef DEBUG
    long len = CFStringGetLength(cfVal) + 1;
    char* cVal = malloc(len * sizeof(char));

    if (!cVal)
    {
        return;
    }

    if (CFStringGetCString(cfVal, cVal, len, kCFStringEncodingASCII))
    {
        printf("%s %s\n", prefix, cVal);
    }

    free(cVal);
#endif
}

void print_cfnumberref(const char* prefix, CFNumberRef cfVal)
{
#ifdef DEBUG
    int result;

    if (CFNumberGetValue(cfVal, kCFNumberSInt32Type, &result))
    {
        printf("%s %i\n", prefix, result);
    }
#endif
}

// --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

char* getMountPathByBSDName(char* bsdName)
{
    if (!bsdName)
    {
        return NULL;
    }

    DASessionRef session = DASessionCreate(kCFAllocatorDefault);
    if (!session)
    {
        return NULL;
    }

    char* cVal;
    int found = 0;
    long len;

    CFDictionaryRef matchingDictionary = IOBSDNameMatching(kIOMainPortDefault, 0, bsdName);
    io_iterator_t it;
    kern_return_t kr = IOServiceGetMatchingServices(kIOMainPortDefault, matchingDictionary, &it);
    if (kr != KERN_SUCCESS)
    {
        CFRelease(session);
        return NULL;
    }

    io_object_t service;
    while ((service = IOIteratorNext(it)))
    {
        io_iterator_t children;
        io_registry_entry_t child;

        IORegistryEntryGetChildIterator(service, kIOServicePlane, &children);
        while ((child = IOIteratorNext(children)))
        {
            CFStringRef bsdNameChild = (CFStringRef)IORegistryEntrySearchCFProperty(child,
                kIOServicePlane,
                CFSTR("BSD Name"),
                kCFAllocatorDefault,
                kIORegistryIterateRecursively);

            if (bsdNameChild)
            {
                len = CFStringGetLength(bsdNameChild) + 1;
                cVal = malloc(len * sizeof(char));
                if (cVal)
                {
                    if (CFStringGetCString(bsdNameChild, cVal, len, kCFStringEncodingASCII))
                    {
                        found = 1;

                        // Copy / Paste --->
                        DADiskRef disk = DADiskCreateFromBSDName(kCFAllocatorDefault, session, cVal);
                        if (disk)
                        {
                            CFDictionaryRef diskInfo = DADiskCopyDescription(disk);
                            if (diskInfo)
                            {
                                CFURLRef fspath = (CFURLRef)CFDictionaryGetValue(diskInfo, kDADiskDescriptionVolumePathKey);
                                if (CFURLGetFileSystemRepresentation(fspath, false, (UInt8*)buffer, 1024))
                                {
                                    // for now, return the first found partition

                                    CFRelease(diskInfo);
                                    CFRelease(disk);
                                    CFRelease(session);
                                    free(cVal);

                                    IOObjectRelease(child);
                                    IOObjectRelease(children);
                                    IOObjectRelease(service);
                                    IOObjectRelease(it);

                                    return buffer;
                                }

                                CFRelease(diskInfo);
                            }

                            CFRelease(disk);
                        }
                        // <--- Copy / Paste
                    }

                    free(cVal);
                }
            }
            IOObjectRelease(child);
        }
        IOObjectRelease(children);

        IOObjectRelease(service);
    }
    IOObjectRelease(it);

    /*
    The device could get name 'disk1s1, or just 'disk1'.
    In first case, the original bsd name would be 'disk1', and the child bsd name would be 'disk1s1'.
    In second case, there would be no child bsd names, but the original one is valid for further work (obtaining various properties).
    */

    if (!found)
    {
        // Copy / Paste --->
        DADiskRef disk = DADiskCreateFromBSDName(kCFAllocatorDefault, session, bsdName);
        if (disk)
        {
            CFDictionaryRef diskInfo = DADiskCopyDescription(disk);
            if (diskInfo)
            {
                CFURLRef fspath = (CFURLRef)CFDictionaryGetValue(diskInfo, kDADiskDescriptionVolumePathKey);
                if (CFURLGetFileSystemRepresentation(fspath, false, (UInt8*)buffer, 1024))
                {
                    // for now, return the first found partition

                    CFRelease(diskInfo);
                    CFRelease(disk);
                    CFRelease(session);

                    return buffer;
                }

                CFRelease(diskInfo);
            }

            CFRelease(disk);
        }
        // <--- Copy / Paste
    }

    CFRelease(session);
    return NULL;
}

// --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    void get_usb_device_info(io_service_t device, int newdev)
    {
        io_name_t devicename;
        io_string_t devicepath; // CHANGED: io_name_t (128 bytes) -> io_string_t (512 bytes)
        io_name_t classname;

        char* cVal;
        long len;
        int result;

        if (IORegistryEntryGetName(device, devicename) != KERN_SUCCESS)
        {
            fprintf(stderr, "%s unknown device\n", newdev ? "added" : " removed");
            return;
        }

        usbDevice = empty;

        debug_print("USB device %s: %s\n", newdev ? "FOUND" : "REMOVED", devicename);

        // Safe copy for DeviceName
        strncpy(usbDevice.DeviceName, devicename, sizeof(usbDevice.DeviceName) - 1);
        usbDevice.DeviceName[sizeof(usbDevice.DeviceName) - 1] = '\0';

        if (IORegistryEntryGetPath(device, kIOServicePlane, devicepath) == KERN_SUCCESS)
        {
            debug_print("\tDevice path: %s\n", devicepath);

            // CHANGED: Safe copy with truncation to avoid overflowing the 255-byte struct field
            strncpy(usbDevice.DeviceSystemPath, devicepath, sizeof(usbDevice.DeviceSystemPath) - 1);
            usbDevice.DeviceSystemPath[sizeof(usbDevice.DeviceSystemPath) - 1] = '\0';
        }

        if (IOObjectGetClass(device, classname) == KERN_SUCCESS)
        {
            debug_print("\tDevice class name: %s\n", classname);
        }

        CFStringRef vendorname = (CFStringRef)IORegistryEntrySearchCFProperty(device
        , kIOServicePlane
        , CFSTR("USB Vendor Name")
        , NULL
        , kIORegistryIterateRecursively | kIORegistryIterateParents);

    if (vendorname)
    {
        print_cfstringref("\tDevice vendor name:", vendorname);

        len = CFStringGetLength(vendorname) + 1;
        cVal = malloc(len * sizeof(char));
        if (cVal)
        {
            if (CFStringGetCString(vendorname, cVal, len, kCFStringEncodingASCII))
            {
                // CHANGED: Use strncpy for safety on all string fields
                strncpy(usbDevice.Vendor, cVal, sizeof(usbDevice.Vendor) - 1);
                usbDevice.Vendor[sizeof(usbDevice.Vendor) - 1] = '\0';
                
                strncpy(usbDevice.VendorDescription, cVal, sizeof(usbDevice.VendorDescription) - 1);
                usbDevice.VendorDescription[sizeof(usbDevice.VendorDescription) - 1] = '\0';
            }

            free(cVal);
        }
    }

    CFNumberRef vendorId = (CFNumberRef)IORegistryEntrySearchCFProperty(device
        , kIOServicePlane
        , CFSTR("idVendor")
        , NULL
        , kIORegistryIterateRecursively | kIORegistryIterateParents);

    if (vendorId)
    {
        print_cfnumberref("\tVendor id:", vendorId);

        if (CFNumberGetValue(vendorId, kCFNumberSInt32Type, &result))
        {
            sprintf(usbDevice.VendorID, "%d", result);
        }
    }

    CFStringRef productname = (CFStringRef)IORegistryEntrySearchCFProperty(device
        , kIOServicePlane
        , CFSTR("USB Product Name")
        , NULL
        , kIORegistryIterateRecursively | kIORegistryIterateParents);

    if (productname)
    {
        print_cfstringref("\tDevice product name:", productname);

        len = CFStringGetLength(productname) + 1;
        cVal = malloc(len * sizeof(char));
        if (cVal)
        {
            if (CFStringGetCString(productname, cVal, len, kCFStringEncodingASCII))
            {
                // CHANGED: Use strncpy for safety
                strncpy(usbDevice.Product, cVal, sizeof(usbDevice.Product) - 1);
                usbDevice.Product[sizeof(usbDevice.Product) - 1] = '\0';
                    
                strncpy(usbDevice.ProductDescription, cVal, sizeof(usbDevice.ProductDescription) - 1);
                usbDevice.ProductDescription[sizeof(usbDevice.ProductDescription) - 1] = '\0';
            }

            free(cVal);
        }
    }

    CFNumberRef productId = (CFNumberRef)IORegistryEntrySearchCFProperty(device
        , kIOServicePlane
        , CFSTR("idProduct")
        , NULL
        , kIORegistryIterateRecursively | kIORegistryIterateParents);

    if (productId)
    {
        print_cfnumberref("\tProduct id:", productId);

        if (CFNumberGetValue(productId, kCFNumberSInt32Type, &result))
        {
            sprintf(usbDevice.ProductID, "%d", result);
        }
    }

    CFStringRef serialnumber = (CFStringRef)IORegistryEntrySearchCFProperty(device
        , kIOServicePlane
        , CFSTR("USB Serial Number")
        , NULL
        , kIORegistryIterateRecursively | kIORegistryIterateParents);

    if (serialnumber)
    {
        print_cfstringref("\tDevice serial number:", serialnumber);

        len = CFStringGetLength(serialnumber) + 1;
        cVal = malloc(len * sizeof(char));
        if (cVal)
        {
            if (CFStringGetCString(serialnumber, cVal, len, kCFStringEncodingASCII))
            {
                // CHANGED: Use strncpy for safety
                strncpy(usbDevice.SerialNumber, cVal, sizeof(usbDevice.SerialNumber) - 1);
                usbDevice.SerialNumber[sizeof(usbDevice.SerialNumber) - 1] = '\0';
            }

            free(cVal);
        }
    }

    debug_print("\n");

    if (newdev)
    {
        InsertedCallback(usbDevice);
    }
    else
    {
        RemovedCallback(usbDevice);
    }
}

void iterate_usb_devices(io_iterator_t iterator, int newdev)
{
    io_service_t usbDevice;

    while ((usbDevice = IOIteratorNext(iterator)))
    {
        get_usb_device_info(usbDevice, newdev);
        IOObjectRelease(usbDevice);
    }
}

void usb_device_added(void* refcon, io_iterator_t iterator)
{
    iterate_usb_devices(iterator, 1);
}

void usb_device_removed(void* refcon, io_iterator_t iterator)
{
    iterate_usb_devices(iterator, 0);
}

// Global variable to hold the run loop source
CFRunLoopSourceRef stopRunLoopSource = NULL;

CFRunLoopRef runLoop;

// Callback function for the run loop source
void stopRunLoopSourceCallback(void* info)
{
    // Stop the run loop when the source is triggered
    CFRunLoopStop(runLoop);
}

// Function to add the stop run loop source
void addStopRunLoopSource(void)
{
    // Create a custom context for the run loop source
    CFRunLoopSourceContext sourceContext = {
        .version = 0,
        .info = NULL,
        .retain = NULL,
        .release = NULL,
        .copyDescription = NULL,
        .equal = NULL,
        .hash = NULL,
        .schedule = NULL,
        .cancel = NULL,
        .perform = stopRunLoopSourceCallback
    };
    
    // Create the run loop source
    stopRunLoopSource = CFRunLoopSourceCreate(kCFAllocatorDefault, 0, &sourceContext);
    
    // Add the run loop source to the current run loop
    CFRunLoopAddSource(runLoop, stopRunLoopSource, kCFRunLoopDefaultMode);
}

// Function to remove the stop run loop source
void removeStopRunLoopSource(void)
{
    if (stopRunLoopSource != NULL)
    {
        // Remove the run loop source from the current run loop
        CFRunLoopRemoveSource(runLoop, stopRunLoopSource, kCFRunLoopDefaultMode);
        
        // Release the run loop source
        CFRelease(stopRunLoopSource);
        stopRunLoopSource = NULL;
    }
}

void init_notifier(void)
{
    notificationPort = IONotificationPortCreate(kIOMainPortDefault);
    CFRunLoopAddSource(runLoop, IONotificationPortGetRunLoopSource(notificationPort), kCFRunLoopDefaultMode);

    debug_print("init_notifier ok\n");
}

// https://sudonull.com/post/141779-Working-with-USB-devices-in-a-C-program-on-MacOS-X

    void configure_and_start_notifier(void)
    {
        debug_print("Starting notifier\n");

        // CHANGED: Create a distinct dictionary for Inserted events
        CFMutableDictionaryRef matchDict = (CFMutableDictionaryRef)IOServiceMatching(kIOUSBDeviceClassName);

        if (!matchDict)
        {
            fprintf(stderr, "Failed to create matching dictionary for kIOUSBDeviceClassName\n");
            return;
        }

        kern_return_t addResult;

        io_iterator_t deviceAddedIter;
        // CHANGED: Removed CFRetain from IOServiceMatching above, IOServiceAddMatchingNotification consumes it
        addResult = IOServiceAddMatchingNotification(notificationPort, kIOMatchedNotification, matchDict, usb_device_added, NULL, &deviceAddedIter);

        if (addResult != KERN_SUCCESS)
        {
            fprintf(stderr, "IOServiceAddMatchingNotification failed for kIOMatchedNotification\n");
            //CFRelease(matchDict); - CF_IS_OBJC exception
            return;
        }

        usb_device_added(NULL, deviceAddedIter);

        // CHANGED: Create a NEW dictionary for Removed events (DO NOT REUSE matchDict)
        matchDict = (CFMutableDictionaryRef)IOServiceMatching(kIOUSBDeviceClassName);
        if (!matchDict)
        {
            fprintf(stderr, "Failed to create matching dictionary for kIOUSBDeviceClassName (Removed)\n");
            return;
        }

        io_iterator_t deviceRemovedIter;
        addResult = IOServiceAddMatchingNotification(notificationPort, kIOTerminatedNotification, matchDict, usb_device_removed, NULL, &deviceRemovedIter);

        if (addResult != KERN_SUCCESS)
        {
            fprintf(stderr, "IOServiceAddMatchingNotification failed for kIOTerminatedNotification\n");
            //CFRelease(matchDict); - CF_IS_OBJC exception
            return;
        }

        usb_device_removed(NULL, deviceRemovedIter);

        // Add the stop run loop source
        addStopRunLoopSource();

    // Start the run loop
    CFRunLoopRun();

    // Remove the stop run loop source
    removeStopRunLoopSource();

    //CFRelease(matchDict); - CF_IS_OBJC exception
}

void deinit_notifier(void)
{
    CFRunLoopRemoveSource(runLoop, IONotificationPortGetRunLoopSource(notificationPort), kCFRunLoopDefaultMode);
    IONotificationPortDestroy(notificationPort);

    debug_print("deinit_notifier ok\n");
}

void signal_handler(int signum)
{
    debug_print("\ngot signal, signnum=%i  stopping current RunLoop\n", signum);

    CFRunLoopStop(runLoop);
}

void init_signal_handler(void)
{
    signal(SIGINT, signal_handler);
    signal(SIGQUIT, signal_handler);
    signal(SIGTERM, signal_handler);
}

#ifdef __cplusplus
extern "C" {
#endif

void StartMacWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback)
{
    InsertedCallback = insertedCallback;
    RemovedCallback = removedCallback;

    runLoop = CFRunLoopGetCurrent();

    //init_signal_handler();
    init_notifier();
    configure_and_start_notifier();
    deinit_notifier();
}

void StopMacWatcher(void)
{
    if (stopRunLoopSource != NULL)
    {
        // Signal the run loop source to stop the run loop
        CFRunLoopSourceSignal(stopRunLoopSource);
        
        // Wake up the run loop to process the signal immediately
        CFRunLoopWakeUp(runLoop);
    }
}

void GetMacMountPoint(const char* syspath, MountPointCallback mountPointCallback)
{
    if (!syspath)
    {
        mountPointCallback("");
        return;
    }

    CFMutableDictionaryRef matchingDictionary = IOServiceMatching(kIOUSBInterfaceClassName);

    // now specify class and subclass to iterate only through USB mass storage devices:
    CFNumberRef cfValue;
    SInt32 deviceClassNum = kUSBMassStorageInterfaceClass;
    cfValue = CFNumberCreate(kCFAllocatorDefault, kCFNumberSInt32Type, &deviceClassNum);
    CFDictionaryAddValue(matchingDictionary, CFSTR(kUSBInterfaceClass), cfValue);
    CFRelease(cfValue);

    // NOTE: if you will specify only device class and will not specify subclass, it will return an empty iterator, and I don't know how to say that we need any subclass. 
    // BUT: all the devices I've check had kUSBMassStorageSCSISubClass
    SInt32 deviceSubClassNum = kUSBMassStorageSCSISubClass;
    cfValue = CFNumberCreate(kCFAllocatorDefault, kCFNumberSInt32Type, &deviceSubClassNum);
    CFDictionaryAddValue(matchingDictionary, CFSTR(kUSBInterfaceSubClass), cfValue);
    CFRelease(cfValue);

    io_iterator_t foundIterator = 0;
    io_service_t usbInterface;
    IOServiceGetMatchingServices(kIOMainPortDefault, matchingDictionary, &foundIterator);

    char* cVal;
    int found = 0;
    long len;
    int match = 0;
    io_name_t devicepath;

    // iterate through USB mass storage devices
    while ((usbInterface = IOIteratorNext(foundIterator)))
    {
        if (IORegistryEntryGetPath(usbInterface, kIOServicePlane, devicepath) == KERN_SUCCESS)
        {
            if (strncmp(devicepath, syspath, strlen(syspath)) == 0)
            {
                CFStringRef bsdName = (CFStringRef)IORegistryEntrySearchCFProperty(usbInterface,
                    kIOServicePlane,
                    CFSTR("BSD Name"),
                    kCFAllocatorDefault,
                    kIORegistryIterateRecursively);

                if (bsdName)
                {
                    len = CFStringGetLength(bsdName) + 1;
                    cVal = malloc(len * sizeof(char));
                    if (cVal)
                    {
                        if (CFStringGetCString(bsdName, cVal, len, kCFStringEncodingASCII))
                        {
                            char* mountPath = getMountPathByBSDName(cVal);

                            if (mountPath)
                            {
                                found = 1;
                                mountPointCallback(mountPath);
                            }
                        }

                        free(cVal);
                    }
                }

                match = 1;
            }
        }
        IOObjectRelease(usbInterface);

        if (match)
            break;
    }
    IOObjectRelease(foundIterator);

    if (!found)
        mountPointCallback("");
}

#ifdef __cplusplus
}
#endif
