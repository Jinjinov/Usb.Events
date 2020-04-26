using System;
using System.Collections.Generic;

namespace Usb.Events
{
    public class UsbDevice
    {
        public string Manufacturer { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Serial { get; set; } = string.Empty;
        public string ProductID { get; set; } = string.Empty;
        public string VendorID { get; set; } = string.Empty;
    }

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
