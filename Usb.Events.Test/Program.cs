using System;

namespace Usb.Events.Test
{
    class Program
    {
        static readonly IUsbEventWatcher usbEventWatcher = new UsbEventWatcher();

        static void Main(string[] _)
        {
            foreach (string driveName in usbEventWatcher.RemovableDriveNameList)
            {
                Console.WriteLine(driveName);
            }

            usbEventWatcher.DriveInserted += (_, driveName) => Console.WriteLine($"Drive {driveName} inserted!");

            usbEventWatcher.DriveRemoved += (_, driveName) => Console.WriteLine($"Drive {driveName} removed!");

            Console.ReadLine();
        }
    }
}
