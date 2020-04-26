using System;

namespace Usb.Events.Test
{
    class Program
    {
        static readonly IUsbEventWatcher usbEventWatcher = new UsbEventWatcher();

        static void Main(string[] _)
        {
            foreach (string path in usbEventWatcher.UsbDrivePathList)
            {
                Console.WriteLine(path);
            }

            usbEventWatcher.UsbDriveInserted += (_, path) => Console.WriteLine($"Drive {path} inserted!");

            usbEventWatcher.UsbDriveRemoved += (_, path) => Console.WriteLine($"Drive {path} removed!");

            usbEventWatcher.UsbDeviceInserted += (_, device) => Console.WriteLine($"Device {device.VendorDescription} {device.ProductDescription} inserted!");

            usbEventWatcher.UsbDeviceRemoved += (_, device) => Console.WriteLine($"Device {device.DeviceName} {device.DevicePath} removed!");

            Console.ReadLine();
        }
    }
}
