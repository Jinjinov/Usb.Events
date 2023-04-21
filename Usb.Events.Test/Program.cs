using System;
using System.IO;

namespace Usb.Events.Test
{
    class Program
    {
        static void Main(string[] _)
        {
            using IUsbEventWatcher usbEventWatcher = new UsbEventWatcher(startImmediately: true, addAlreadyPresentDevicesToList: true);

            foreach (UsbDevice device in usbEventWatcher.UsbDeviceList)
            {
                Console.WriteLine(device + Environment.NewLine);
            }

            usbEventWatcher.UsbDeviceRemoved += (_, device) => Console.WriteLine("Removed:" + Environment.NewLine + device + Environment.NewLine);

            usbEventWatcher.UsbDeviceAdded += (_, device) => Console.WriteLine("Added:" + Environment.NewLine + device + Environment.NewLine);

            usbEventWatcher.UsbDriveEjected += (_, path) => Console.WriteLine("Ejected:" + Environment.NewLine + path + Environment.NewLine);

            usbEventWatcher.UsbDriveMounted += (_, path) =>
            {
                Console.WriteLine("Mounted:" + Environment.NewLine + path + Environment.NewLine);

                foreach (string entry in Directory.GetFileSystemEntries(path))
                    Console.WriteLine(entry);

                Console.WriteLine();
            };

            Console.ReadLine();
        }
    }
}
