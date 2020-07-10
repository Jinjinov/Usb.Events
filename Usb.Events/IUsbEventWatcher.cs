using System;
using System.Collections.Generic;

namespace Usb.Events
{
    public interface IUsbEventWatcher
    {
        List<string> UsbDrivePathList { get; }
        List<UsbDevice> UsbDeviceList { get; }

        event EventHandler<string>? UsbDriveInserted;
        event EventHandler<string>? UsbDriveRemoved;

        event EventHandler<UsbDevice>? UsbDeviceInserted;
        event EventHandler<UsbDevice>? UsbDeviceRemoved;
    }
}
