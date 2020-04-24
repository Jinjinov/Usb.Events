/* List and monitor USB devices using libudev.
 *
 * gcc -o udev_monitor_usb udev_monitor_usb.c -ludev
 * ./udev_monitor_usb
 */
#include <libudev.h>
#include <stdio.h>

#define SUBSYSTEM "usb"

static void print_device(struct udev_device* dev)
{
    const char* action = udev_device_get_action(dev);
    if (! action)
        action = "exists";

    const char* vendor = udev_device_get_sysattr_value(dev, "idVendor");
    if (! vendor)
        vendor = "0000";

    const char* product = udev_device_get_sysattr_value(dev, "idProduct");
    if (! product)
        product = "0000";

    printf("%s %s %6s %s:%s %s\n",
           udev_device_get_subsystem(dev),
           udev_device_get_devtype(dev),
           action,
           vendor,
           product,
           udev_device_get_devnode(dev));
}

static void process_device(struct udev_device* dev)
{
    if (dev) {
        if (udev_device_get_devnode(dev))
            print_device(dev);

        udev_device_unref(dev);
    }
}

static void enumerate_devices(struct udev* udev)
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

static void monitor_devices(struct udev* udev)
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

int main(void)
{
    struct udev* udev = udev_new();
    if (!udev) {
        fprintf(stderr, "udev_new() failed\n");
        return 1;
    }

    enumerate_devices(udev);
    monitor_devices(udev);

    udev_unref(udev);
    return 0;
}
