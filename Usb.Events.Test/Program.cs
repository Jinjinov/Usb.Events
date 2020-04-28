using System;

namespace Usb.Events.Test
{
    class Program
    {
        static readonly IUsbEventWatcher usbEventWatcher = new UsbEventWatcher();

        static void Main(string[] _)
        {
            usbEventWatcher.UsbDriveInserted += (_, path) => Console.WriteLine($"Inserted: {path}");

            usbEventWatcher.UsbDriveRemoved += (_, path) => Console.WriteLine($"Removed: {path}");

            usbEventWatcher.UsbDeviceInserted += (_, device) => Console.WriteLine($"Inserted: {device}");

            usbEventWatcher.UsbDeviceRemoved += (_, device) => Console.WriteLine($"Removed: {device}");

            Console.ReadLine();
        }
    }
}
