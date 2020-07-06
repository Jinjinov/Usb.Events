using System;

namespace Usb.Events.Test
{
    class Program
    {
        static readonly IUsbEventWatcher usbEventWatcher = new UsbEventWatcher();

        static void Main(string[] _)
        {
            usbEventWatcher.UsbDriveInserted += (_, path) => Console.WriteLine("Inserted:" + Environment.NewLine + path + Environment.NewLine);

            usbEventWatcher.UsbDriveRemoved += (_, path) => Console.WriteLine("Removed:" + Environment.NewLine + path + Environment.NewLine);

            usbEventWatcher.UsbDeviceInserted += (_, device) => Console.WriteLine("Inserted:" + Environment.NewLine + device + Environment.NewLine);

            usbEventWatcher.UsbDeviceRemoved += (_, device) => Console.WriteLine("Removed:" + Environment.NewLine + device + Environment.NewLine);

            Console.ReadLine();
        }
    }
}
