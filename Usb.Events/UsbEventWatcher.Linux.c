/* List and monitor USB devices using libudev.
 *
 * gcc -o udev_monitor_usb udev_monitor_usb.c -ludev
 * ./udev_monitor_usb
 */
#include <libudev.h>
#include <stdio.h>
#include <string.h>

#define SUBSYSTEM "usb"

typedef void (*WatcherCallback)(char* message);
WatcherCallback InsertedCallback;
WatcherCallback RemovedCallback;

char buffer[4096];

void print_device(struct udev_device* dev)
{
    const char* action = udev_device_get_action(dev);
    if (! action)
        action = "exists";

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
        udev_device_get_sysattr_value(dev, "manufacturer"), // Kingston

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

    if (strcmp(action, "add") == 0)
    {
        InsertedCallback(buffer);
    }

    if (strcmp(action, "remove") == 0)
    {
        RemovedCallback(buffer);
    }
}

void process_device(struct udev_device* dev)
{
    if (dev) {
        if (udev_device_get_devnode(dev))
            print_device(dev);

        udev_device_unref(dev);
    }
}

void enumerate_devices(struct udev* udev)
{
    struct udev_enumerate* enumerate = udev_enumerate_new(udev);

    udev_enumerate_add_match_subsystem(enumerate, SUBSYSTEM);
    udev_enumerate_scan_devices(enumerate);

    struct udev_list_entry* devices = udev_enumerate_get_list_entry(enumerate);
    struct udev_list_entry* entry;

    udev_list_entry_foreach(entry, devices) {
        const char* path = udev_list_entry_get_name(entry);
        struct udev_device* dev = udev_device_new_from_syspath(udev, path);
        process_device(dev);
    }

    udev_enumerate_unref(enumerate);
}

void monitor_devices(struct udev* udev)
{
    struct udev_monitor* mon = udev_monitor_new_from_netlink(udev, "udev");

    udev_monitor_filter_add_match_subsystem_devtype(mon, SUBSYSTEM, NULL);
    udev_monitor_enable_receiving(mon);

    int fd = udev_monitor_get_fd(mon);

    while (1) {
        fd_set fds;
        FD_ZERO(&fds);
        FD_SET(fd, &fds);

        int ret = select(fd+1, &fds, NULL, NULL, NULL);
        if (ret <= 0)
            break;

        if (FD_ISSET(fd, &fds)) {
            struct udev_device* dev = udev_monitor_receive_device(mon);
            process_device(dev);
        }
    }
}

#ifdef __cplusplus
extern "C" {
#endif

    void StartLinuxWatcher(WatcherCallback insertedCallback, WatcherCallback removedCallback)
    {
        InsertedCallback = insertedCallback;
        RemovedCallback = removedCallback;

        struct udev* udev = udev_new();
        if (!udev) {
            fprintf(stderr, "udev_new() failed\n");
            return;
        }

        enumerate_devices(udev);
        monitor_devices(udev);

        udev_unref(udev);
    }

    /*
    char result[100] = "Linux received a message: ";

    char* DelegateTest(char* message, WatcherCallback testCallback)
    {
        strcat(result, message);

        testCallback(result);

        return result;
    }
    /**/

#ifdef __cplusplus
}
#endif
