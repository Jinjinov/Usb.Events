using System;
using System.Collections.Generic;

namespace Usb.Events
{
    public interface IUsbEventWatcher
    {
        IList<string> UsbDrivePathList { get; }
        IList<UsbDevice> UsbDeviceList { get; }

        event EventHandler<string>? UsbDriveInserted;
        event EventHandler<string>? UsbDriveRemoved;

        event EventHandler<UsbDevice>? UsbDeviceInserted;
        event EventHandler<UsbDevice>? UsbDeviceRemoved;
    }
}
