# Usb.Events

Subscribe to the Inserted and Removed events to be notified when a USB drive is plugged in or unplugged, or when a USB device is connected or disconnected. Usb.Events is a .NET Standard 2.0 library and uses WMI on Windows, libudev on Linux and IOKit on macOS.

## How to use:

1. Include NuGet package from https://www.nuget.org/packages/Usb.Events

        <ItemGroup>
            <PackageReference Include="Usb.Events" Version="10.0.1.1" />
        </ItemGroup>
        
2. Subscribe to events:

        using Usb.Events;

        class Program
        {
            static void Main(string[] _)
            {
                using IUsbEventWatcher usbEventWatcher = new UsbEventWatcher();

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

## Constructor parameter:

```
UsbEventWatcher(bool startImmediately = true, bool includeTTY = false)
```

- Set `startImmediately` to `false` if you don't want to start immediately. Then call the `Start(bool includeTTY = false)` method.
- Set `includeTTY` to `true` if you want to monitor the `TTY` subsystem in Linux (besides the `USB` subsystem).

## Example:

`Usb.Events.Example` demonstrates how to use Windows `SetupAPI.dll` functions [SetupDiGetClassDevs](https://docs.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetclassdevsw), [SetupDiEnumDeviceInfo](https://docs.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdienumdeviceinfo) and [SetupDiGetDeviceProperty](https://docs.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetdevicepropertyw) together with [DEVPKEY_Device_DeviceDesc](https://docs.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-devicedesc), [DEVPKEY_Device_BusReportedDeviceDesc](https://docs.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-busreporteddevicedesc) and [DEVPKEY_Device_FriendlyName](https://docs.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-friendlyname) to get "Device description", "Bus reported device description" and "Friendly name" of the `Usb.Events.UsbDevice` reported by the `Usb.Events.IUsbEventWatcher.UsbDeviceAdded` event.

## TO DO:

- [ ] Automatically mount USB drive on `UsbDeviceAdded` event in Linux
- [ ] Automatically mount USB drive on `UsbDeviceAdded` event in macOS

## Version history:

- 10.0.1.1:
    - Added `bool startImmediately = true` to `UsbEventWatcher` constructor
    - Added `void Start(bool includeTTY = false)` to `IUsbEventWatcher`
- 10.0.1.0:
    - Added `bool includeTTY = false` to `UsbEventWatcher` constructor
    - Fixed a `EnumerateDevices` bug in Linux - thanks to [@d79ima](https://github.com/d79ima)
- 10.0.0.1:
    - Fixed a false "device added" events bug in Linux - thanks to [@d79ima](https://github.com/d79ima)
- 10.0.0.0:
    - Fixed a `NullReferenceException` in Linux and macOS - by [@thomOrbelius](https://github.com/thomOrbelius)
- 1.1.1.1:
    - Fixed a bug in Windows where `MountedDirectoryPath` wasn't set for a disk drive - thanks to [@cksoft0807](https://github.com/cksoft0807)
- 1.1.1.0:
    - Fixed a memory leak in Linux function `GetLinuxMountPoint` - by [@maskimthedog](https://github.com/maskimthedog)
    - Fixed a bug in Linux where after instantiating `UsbEventWatcher`, the list of devices was empty - by [@maskimthedog](https://github.com/maskimthedog)
    - Added monitoring of `TTY` subsystem in Linux - by [@maskimthedog](https://github.com/maskimthedog)
    - Fixed a bug in Linux where monitoring would stop upon error - by [@maskimthedog](https://github.com/maskimthedog)
- 1.1.0.1:
    - Fixed a bug
- 1.1.0.0:
    - Added:
        - `MountedDirectoryPath`
        - `IsMounted`
        - `IsEjected`
    - Breaking changes:
        - `DevicePath` renamed to `DeviceSystemPath`
        - `UsbDriveInserted` renamed to `UsbDriveMounted`
        - `UsbDriveRemoved` renamed to `UsbDriveEjected`
        - `UsbDeviceInserted` renamed to `UsbDeviceAdded`
- 1.0.1.1:
    - Fixed a bug
- 1.0.1.0:
    - Events for all USB devices
- 1.0.0.1:
    - Fixed a bug
- 1.0.0.0:
    - Events for USB drives and USB storage devices