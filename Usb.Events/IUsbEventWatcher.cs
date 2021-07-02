using System;
using System.Collections.Generic;

namespace Usb.Events
{
    public interface IUsbEventWatcher : IDisposable
    {
        List<string> UsbDrivePathList { get; }
        List<UsbDevice> UsbDeviceList { get; }

        event EventHandler<string>? UsbDriveMounted;
        event EventHandler<string>? UsbDriveEjected;

        event EventHandler<UsbDevice>? UsbDeviceAdded;
        event EventHandler<UsbDevice>? UsbDeviceRemoved;
    }
}
