#include <libudev.h>
#include <mntent.h>
#include <stdio.h>
#include <string.h>

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

typedef void (*WatcherCallback)(UsbDeviceData usbDevice);
WatcherCallback InsertedCallback;
WatcherCallback RemovedCallback;

typedef void (*MessageCallback)(const char* message);
MessageCallback Message;

//char buffer[4096];

struct udev* g_udev;

struct udev_device* GetChild(struct udev* udev, struct udev_device* parent, const char* subsystem, const char* devtype)
{
    struct udev_device* child = NULL;
    struct udev_enumerate* enumerate = udev_enumerate_new(udev);

    udev_enumerate_add_match_parent(enumerate, parent);
    udev_enumerate_add_match_subsystem(enumerate, subsystem);
    udev_enumerate_scan_devices(enumerate);

    struct udev_list_entry* devices = udev_enumerate_get_list_entry(enumerate);
    struct udev_list_entry* entry;

    udev_list_entry_foreach(entry, devices)
    {
        const char* path = udev_list_entry_get_name(entry);
        child = udev_device_new_from_syspath(udev, path);

        if(!devtype)
            break;

        if (strcmp(udev_device_get_devtype(child), devtype) == 0)
        {
            break;
        }
    }

    udev_enumerate_unref(enumerate);

    return child;
}

char* FindMountPoint(const char* dev_node)
{
    struct mntent* mount_table_entry;
    FILE* file;
    char* mount_point = NULL;

    if (dev_node == NULL)
    {
        return NULL;
    }

    file = setmntent("/proc/mounts", "r");

    if (file == NULL)
    {
        return NULL;
    }

    while (NULL != (mount_table_entry = getmntent(file)))
    {
        if (strncmp(mount_table_entry->mnt_fsname, dev_node, strlen(mount_table_entry->mnt_fsname)) == 0)
        {
            mount_point = mount_table_entry->mnt_dir;

            break;
        }
    }

    endmntent(file);

    return mount_point;
}

void GetDeviceInfo(struct udev* udev, struct udev_device* dev)
{
    const char* action = udev_device_get_action(dev);
    if (! action)
        action = "exists";

    int added = strcmp(action, "add") == 0;

    int removed = strcmp(action, "remove") == 0;

    /*
    sprintf(buffer, "%s ; %s ; %s ; %s ; %s ; %s ; %s ; %s ; %s",
        udev_device_get_devpath(dev),   // /devices/pci0000:00/0000:00:06.0/usb1/1-2
        udev_device_get_subsystem(dev), // usb
        udev_device_get_devtype(dev),   // usb_device
        udev_device_get_syspath(dev),   // /sys/devices/pci0000:00/0000:00:06.0/usb1/1-2
        udev_device_get_sysname(dev),   // 1-2
        udev_device_get_sysnum(dev),    // 2
        udev_device_get_devnode(dev),   // /dev/bus/usb/001/011
        udev_device_get_driver(dev),    // usb
        udev_device_get_action(dev));   // add

    sprintf(buffer, "%s ; %s ; %s ; %s ; %s",
        udev_device_get_sysattr_value(dev, "idVendor"),     // 0951
        udev_device_get_sysattr_value(dev, "idProduct"),    // 1625
        udev_device_get_sysattr_value(dev, "serial"),       // 0019E06B9C85F9A0F7550C20
        udev_device_get_sysattr_value(dev, "product"),      // DT 101 II
        udev_device_get_sysattr_value(dev, "manufacturer")); // Kingston

    sprintf(buffer, "%s ; %s ; %s ; %s ; %s ; %s ; %s ; %s ; %s",
        udev_device_get_property_value(dev, "DEVNAME"),                 // /dev/bus/usb/001/016
        udev_device_get_property_value(dev, "DEVPATH"),                 // /devices/pci0000:00/0000:00:06.0/usb1/1-3
        udev_device_get_property_value(dev, "ID_MODEL"),                // DT_101_II
        udev_device_get_property_value(dev, "ID_MODEL_FROM_DATABASE"),  // DataTraveler 101 II
        udev_device_get_property_value(dev, "ID_MODEL_ID"),             // 1625
        udev_device_get_property_value(dev, "ID_SERIAL_SHORT"),         // 0019E06B9C85F9A0F7550C20
        udev_device_get_property_value(dev, "ID_VENDOR"),               // Kingston
        udev_device_get_property_value(dev, "ID_VENDOR_FROM_DATABASE"), // Kingston Technology
        udev_device_get_property_value(dev, "ID_VENDOR_ID"));           // 0951

    strcpy(buffer, " ; ");
    struct udev_list_entry* entry;
    struct udev_list_entry* sysattrs = udev_device_get_properties_list_entry(dev);
    udev_list_entry_foreach(entry, sysattrs) {
        const char* name = udev_list_entry_get_name(entry);
        const char* value = udev_list_entry_get_value(entry);
        strcat(buffer, name);
        strcat(buffer, " = ");
        strcat(buffer, value);
        strcat(buffer, " ; ");
    }
    /**/

    if (added || removed)
    {
        usbDevice = empty;
        
        const char* DeviceName = udev_device_get_property_value(dev, "DEVNAME");
        if (DeviceName)
            strcpy(usbDevice.DeviceName, DeviceName);

        const char* DeviceSystemPath = udev_device_get_syspath(dev); //udev_device_get_property_value(dev, "DEVPATH");
        if (DeviceSystemPath)
            strcpy(usbDevice.DeviceSystemPath, DeviceSystemPath);

        const char* Product = udev_device_get_property_value(dev, "ID_MODEL");
        if (Product)
            strcpy(usbDevice.Product, Product);

        const char* ProductDescription = udev_device_get_property_value(dev, "ID_MODEL_FROM_DATABASE");
        if (ProductDescription)
            strcpy(usbDevice.ProductDescription, ProductDescription);

        const char* ProductID = udev_device_get_property_value(dev, "ID_MODEL_ID");
        if (ProductID)
            strcpy(usbDevice.ProductID, ProductID);

        const char* SerialNumber = udev_device_get_property_value(dev, "ID_SERIAL_SHORT");
        if (SerialNumber)
            strcpy(usbDevice.SerialNumber, SerialNumber);

        const char* Vendor = udev_device_get_property_value(dev, "ID_VENDOR");
        if (Vendor)
            strcpy(usbDevice.Vendor, Vendor);

        const char* VendorDescription = udev_device_get_property_value(dev, "ID_VENDOR_FROM_DATABASE");
        if (VendorDescription)
            strcpy(usbDevice.VendorDescription, VendorDescription);

        const char* VendorID = udev_device_get_property_value(dev, "ID_VENDOR_ID");
        if (VendorID)
            strcpy(usbDevice.VendorID, VendorID);

        if (added)
        {
            InsertedCallback(usbDevice);
        }

        if (removed)
        {
            RemovedCallback(usbDevice);
        }
    }
}

void ProcessDevice(struct udev* udev, struct udev_device* dev)
{
    if (dev)
    {
        if (udev_device_get_devnode(dev))
        {
            GetDeviceInfo(udev, dev);
        }

        udev_device_unref(dev);
    }
}

void EnumerateDevices(struct udev* udev)
{
    struct udev_enumerate* enumerate = udev_enumerate_new(udev);

    udev_enumerate_add_match_subsystem(enumerate, "usb");
    udev_enumerate_scan_devices(enumerate);

    struct udev_list_entry* devices = udev_enumerate_get_list_entry(enumerate);
    struct udev_list_entry* entry;

    udev_list_entry_foreach(entry, devices)
    {
        const char* path = udev_list_entry_get_name(entry);
        struct udev_device* dev = udev_device_new_from_syspath(udev, path);

        ProcessDevice(udev, dev);
    }

    udev_enumerate_unref(enumerate);
}

void MonitorDevices(struct udev* udev)
{
    struct udev_monitor* mon = udev_monitor_new_from_netlink(udev, "udev");

    udev_monitor_filter_add_match_subsystem_devtype(mon, "usb", NULL);
    udev_monitor_enable_receiving(mon);

    int fd = udev_monitor_get_fd(mon);

    while (1)
    {
        fd_set fds;
        FD_ZERO(&fds);
        FD_SET(fd, &fds);

        int ret = select(fd+1, &fds, NULL, NULL, NULL);

        if (ret <= 0)
        {
            break;
        }

        if (FD_ISSET(fd, &fds))
        {
            struct udev_device* dev = udev_monitor_receive_device(mon);

            ProcessDevice(udev, dev);
        }
    }
}

#ifdef __cplusplus
extern "C" {
#endif

    void StartLinuxWatcher(WatcherCallback insertedCallback, WatcherCallback removedCallback, MessageCallback message)
    {
        InsertedCallback = insertedCallback;
        RemovedCallback = removedCallback;
        Message = message;

        g_udev = udev_new();

        if (!g_udev)
        {
            fprintf(stderr, "udev_new() failed\n");
            return;
        }

        EnumerateDevices(g_udev);
        MonitorDevices(g_udev);

        udev_unref(g_udev);
    }

    void GetMountPoint(const char* syspath, MessageCallback message)
    {
        int found = 0;

        struct udev_device* dev = udev_device_new_from_syspath(g_udev, syspath);

        struct udev_device* scsi = GetChild(g_udev, dev, "scsi", NULL);
        if (scsi)
        {
            struct udev_device* block = GetChild(g_udev, scsi, "block", "partition");
            if (block)
            {
                const char* block_devnode = udev_device_get_devnode(block);
                if (block_devnode)
                {
                    char* mount_point = FindMountPoint(block_devnode);
                    if (mount_point)
                    {
                        found = 1;
                        message(mount_point);
                    }
                }

                udev_device_unref(block);
            }

            udev_device_unref(scsi);
        }

        if (!found)
            message("");
    }

#ifdef __cplusplus
}
#endif
