#define _POSIX_C_SOURCE 199309L
#include <time.h>
#include <errno.h>
#include <libudev.h>
#include <mntent.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <fcntl.h>
#include <sys/select.h>

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

volatile int runLinuxWatcher = 0;

int pipefd[2];

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

void GetDeviceInfo(struct udev_device* dev)
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
}

void MonitorCallback(struct udev_device* dev)
{
    const char* action = udev_device_get_action(dev);
    
    // if device already exists "action" is NULL, otherwise it can be "add", "remove", "change", "move", "online", "offline", "bind", "unbind"

    if (action && (strcmp(action, "remove") == 0 || strcmp(action, "unbind") == 0 || strcmp(action, "offline") == 0))
    {
        RemovedCallback(usbDevice);
    }
    else if (action && (strcmp(action, "add") == 0 || strcmp(action, "bind") == 0 || strcmp(action, "online") == 0))
    {
        InsertedCallback(usbDevice);
    }
}

void EnumerateDevices(struct udev* udev, int includeTTY)
{
    struct udev_enumerate* enumerate = udev_enumerate_new(udev);

    udev_enumerate_add_match_subsystem(enumerate, "usb");
    if (includeTTY)
	    udev_enumerate_add_match_subsystem(enumerate, "tty");
    udev_enumerate_scan_devices(enumerate);

    struct udev_list_entry* devices = udev_enumerate_get_list_entry(enumerate);
    struct udev_list_entry* entry;

    udev_list_entry_foreach(entry, devices)
    {
        const char* path = udev_list_entry_get_name(entry);
        struct udev_device* dev = udev_device_new_from_syspath(udev, path);

        if (dev)
        {
            if (udev_device_get_devnode(dev))
            {
                GetDeviceInfo(dev);

                InsertedCallback(usbDevice);
            }

            udev_device_unref(dev);
        }
    }

    udev_enumerate_unref(enumerate);
}

/* msleep(): Sleep for the requested number of milliseconds. */
int msleep(long msec)
{
    struct timespec ts;
    int res;

    if (msec < 0)
    {
        errno = EINVAL;
        return -1;
    }

    ts.tv_sec = msec / 1000;
    ts.tv_nsec = (msec % 1000) * 1000000;

    do
    {
        res = nanosleep(&ts, &ts);
    } while (res && errno == EINTR);

    return res;
}

void MonitorDevices(struct udev* udev, int includeTTY)
{
    struct udev_monitor* mon = udev_monitor_new_from_netlink(udev, "udev");

    udev_monitor_filter_add_match_subsystem_devtype(mon, "usb", NULL);
    if (includeTTY)
	    udev_monitor_filter_add_match_subsystem_devtype(mon, "tty", NULL);
    udev_monitor_enable_receiving(mon);

    int fd = udev_monitor_get_fd(mon);

    // Create the pipe
    if (pipe(pipefd) == -1) {
        // Handle pipe creation error
        perror("Failed to create pipe");
        return;
    }

    // Set the read end of the pipe to non-blocking mode
    int flags = fcntl(pipefd[0], F_GETFL);
    fcntl(pipefd[0], F_SETFL, flags | O_NONBLOCK);

    while (runLinuxWatcher)
    {
        fd_set fds;
        FD_ZERO(&fds);
        FD_SET(fd, &fds);
        FD_SET(pipefd[0], &fds);

        int maxfd = (fd > pipefd[0]) ? fd : pipefd[0];

        int ret = select(maxfd + 1, &fds, NULL, NULL, NULL);

        if (ret <= 0)
        {
            msleep(100);
            continue;
        }

        if (FD_ISSET(fd, &fds))
        {
            struct udev_device* dev = udev_monitor_receive_device(mon);

            if (dev)
            {
                if (udev_device_get_devnode(dev))
                {
                    GetDeviceInfo(dev);

                    MonitorCallback(dev);
                }

                udev_device_unref(dev);
            }
        }

        if (FD_ISSET(pipefd[0], &fds))
        {
            // Read from the pipe to clear the signal
            char buffer[1];
            read(pipefd[0], buffer, sizeof(buffer));
            // Exit the loop after receiving the interruption signal
            break;
        }
    }

    // Close the pipe file descriptors
    close(pipefd[0]);
    close(pipefd[1]);

    udev_monitor_unref(mon);
}

#ifdef __cplusplus
extern "C" {
#endif

    void StartLinuxWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback, int includeTTY)
    {
        InsertedCallback = insertedCallback;
        RemovedCallback = removedCallback;

        g_udev = udev_new();

        if (!g_udev)
        {
            fprintf(stderr, "udev_new() failed\n");
            return;
        }

        runLinuxWatcher = 1;

        EnumerateDevices(g_udev, includeTTY);
        MonitorDevices(g_udev, includeTTY);

        udev_unref(g_udev);
    }

    void StopLinuxWatcher()
    {
        runLinuxWatcher = 0;

        // Write to the pipe to interrupt the select call in the main loop
        char buffer[1] = { 'x' };
        write(pipefd[1], buffer, sizeof(buffer));
    }

    void GetLinuxMountPoint(const char* syspath, MountPointCallback mountPointCallback)
    {
        int found = 0;

        struct udev_device* dev = udev_device_new_from_syspath(g_udev, syspath);
        if (dev)
        {
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
                            mountPointCallback(mount_point);
                        }
                    }

                    udev_device_unref(block);
                }

                udev_device_unref(scsi);
            }

            udev_device_unref(dev);
        }

        if (!found)
            mountPointCallback("");
    }

#ifdef __cplusplus
}
#endif
