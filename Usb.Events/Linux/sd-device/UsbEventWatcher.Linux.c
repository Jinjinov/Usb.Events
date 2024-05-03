#define _POSIX_C_SOURCE 199309L
#include <stdlib.h>
#include <stdio.h>
#include <errno.h>
#include <mntent.h>
#include <systemd/sd-event.h>
#include <systemd/sd-device.h>
#include <string.h>
#include "UsbEventWatcher.Linux.h"

static UsbDeviceCallback InsertedCallback;
static UsbDeviceCallback RemovedCallback;

static int monitor_handler(sd_device_monitor *monitor, sd_device *device, void *userdata);
static sd_device *get_child(sd_device *parent, const char *subsystem, const char *devtype);

static sd_device_monitor *sddm;
static sd_event *event;

static sd_device *get_child(sd_device *parent, const char *subsystem, const char *devtype)
{
    struct sd_device *match = NULL;
    struct sd_device_enumerator *enumerator = NULL;
    const char *matched_devtype = NULL;

    if (sd_device_enumerator_new(&enumerator) < 0)
        return NULL;
    if (sd_device_enumerator_add_match_parent(enumerator, parent) < 0)
        goto finish;
    if (sd_device_enumerator_add_match_subsystem(enumerator, subsystem, 1) < 0)
        goto finish;

    match = sd_device_enumerator_get_device_first(enumerator);

    // If nothing was found, there's nothing to do
    if (match == NULL)
        goto finish;

    // If we aren't searching for a specific device type, return the first match
    if (!devtype)
        goto finish;

    // Loop searching for the first result that matches the specified device type
    do
    {
        if (sd_device_get_devtype(match, &matched_devtype) < 0)
        {
            match = NULL;
            break;
        }
    } while (strcmp(devtype, matched_devtype) != 0 && (match = sd_device_enumerator_get_device_next(enumerator)) != NULL);

finish:
    sd_device_unref(match);
    sd_device_enumerator_unref(enumerator);

    return match;
}

static void construct_device(sd_device *device, struct UsbDeviceData *data)
{
    const char *device_name = NULL, *device_system_path = NULL, *product = NULL, *product_description = NULL, *product_id = NULL, *serial_number = NULL, *vendor = NULL, *vendor_description = NULL, *vendor_id = NULL;

    if (sd_device_get_devname(device, &device_name) >= 0)
        strcpy(data->DeviceName, device_name);
    if (sd_device_get_syspath(device, &device_system_path) >= 0)
        strcpy(data->DeviceSystemPath, device_system_path);
    if (sd_device_get_property_value(device, "ID_MODEL", &product) >= 0)
        strcpy(data->Product, product);
    if (sd_device_get_property_value(device, "ID_MODEL_FROM_DATABASE", &product_description) >= 0)
        strcpy(data->ProductDescription, product_description);
    if (sd_device_get_property_value(device, "ID_MODEL_ID", &product_id) >= 0)
        strcpy(data->ProductID, product_id);
    if (sd_device_get_property_value(device, "ID_SERIAL_SHORT", &serial_number) >= 0)
        strcpy(data->SerialNumber, serial_number);
    if (sd_device_get_property_value(device, "ID_VENDOR", &vendor) >= 0)
        strcpy(data->Vendor, vendor);
    if (sd_device_get_property_value(device, "ID_VENDOR_FROM_DATABASE", &vendor_description) >= 0)
        strcpy(data->VendorDescription, vendor_description);
    if (sd_device_get_property_value(device, "ID_VENDOR_ID", &vendor_id) >= 0)
        strcpy(data->VendorID, vendor_id);
}

static int monitor_handler(sd_device_monitor *monitor, sd_device *device, void *userdata)
{
    sd_device_action_t action = _SD_DEVICE_ACTION_INVALID;
    struct UsbDeviceData data = {.DeviceName = "", .DeviceSystemPath = "", .Product = "", .ProductDescription = "", .ProductID = "", .SerialNumber = "", .Vendor = "", .VendorDescription = "", .VendorID = ""};
    int result = EXIT_FAILURE;

    if ((result = sd_device_get_action(device, &action)) < 0)
        return result;

    switch (action)
    {
    case SD_DEVICE_ADD:
    case SD_DEVICE_ONLINE:
        construct_device(device, &data);
        InsertedCallback(data);
        break;
    case SD_DEVICE_REMOVE:
    case SD_DEVICE_OFFLINE:
        construct_device(device, &data);
        RemovedCallback(data);
        break;
    default:
        break;
    }

    return result;
}

static const char *find_mount_point(const char *device_node)
{
    struct mntent *mount_table_entry = NULL;
    FILE *file = NULL;
    char *mount_point = NULL;

    if (device_node == NULL)
        return NULL;

    if ((file = setmntent("/proc/mounts", "r")) == NULL)
        return NULL;

    while ((mount_table_entry = getmntent(file)) != NULL)
    {
        if (strncmp(mount_table_entry->mnt_fsname, device_node, strlen(mount_table_entry->mnt_fsname)) == 0)
        {
            mount_point = mount_table_entry->mnt_dir;

            break;
        }
    }

    endmntent(file);

    return mount_point;
}

#ifdef __cplusplus
extern "C"
{
#endif
    void StartLinuxWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback, int includeTTY)
    {
        InsertedCallback = insertedCallback;
        RemovedCallback = removedCallback;

        if (sd_event_default(&event) < 0)
            return;

        if (sd_device_monitor_new(&sddm) < 0)
        {
            sd_event_exit(event, EXIT_FAILURE);
            goto cleanup;
        }

        if (sd_device_monitor_filter_add_match_subsystem_devtype(sddm, "usb", "usb_device") < 0)
        {
            sd_event_exit(event, EXIT_FAILURE);
            goto cleanup;
        }

        if (includeTTY)
        {
            if (sd_device_monitor_filter_add_match_subsystem_devtype(sddm, "tty", NULL) < 0)
            {
                sd_event_exit(event, EXIT_FAILURE);
                goto cleanup;
            }
        }

        if (sd_device_monitor_attach_event(sddm, event) < 0)
        {
            sd_event_exit(event, EXIT_FAILURE);
            goto cleanup;
        }

        if (sd_device_monitor_start(sddm, monitor_handler, NULL) < 0)
        {
            sd_event_exit(event, EXIT_FAILURE);
            goto cleanup;
        }

        sd_event_loop(event);

    cleanup:
        sd_device_monitor_stop(sddm);
        sd_device_monitor_unref(sddm);
        sd_event_unref(event);

        event = NULL;
        sddm = NULL;
    }

    void StopLinuxWatcher(void)
    {
        sd_event_exit(event, EXIT_SUCCESS);
    }

    void GetLinuxMountPoint(const char *syspath, MountPointCallback mountPointCallback)
    {
        struct sd_device *parent_device = NULL;
        struct sd_device *scsi_device = NULL;
        struct sd_device *block_device = NULL;
        const char *block_devnode = NULL;
        const char *mount_point = NULL;

        if (sd_device_new_from_syspath(&parent_device, syspath) < 0)
            goto finish;

        if ((scsi_device = get_child(parent_device, "scsi", NULL)) != NULL && ((block_device = get_child(scsi_device, "block", "partition")) != NULL) && (sd_device_get_devname(block_device, &block_devnode) >= 0) && ((mount_point = find_mount_point(block_devnode)) != NULL))
        {
            mountPointCallback(mount_point);
        }

    finish:
        sd_device_unref(parent_device);
        sd_device_unref(scsi_device);
        sd_device_unref(block_device);
    }
#ifdef __cplusplus
}
#endif