# Usb.Events
Subscribe to the Inserted and Removed events to be notified when a USB drive is plugged in or unplugged, or when a USB device is connected or disconnected. Usb.Events is a .NET Standard 2.0 library and uses WMI on Windows, libudev on Linux and IOKit on macOS.

How to use:

1. Include NuGet package from https://www.nuget.org/packages/Usb.Events

        <PackageReference Include="Usb.Events" Version="1.0.0" />
        
2. Subscribe to events:

        using Usb.Events;

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

Version history:

- 1.0.1: Events for all USB devices
- 1.0.0: Events for USB drives and USB storage devices
