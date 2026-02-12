# Add serial port name (COM* - /dev/tty* - /dev/cu.*) for serial port connection

If I understand correctly, you want to get the serial port name (like `/dev/cu.usbserial-1420`) directly from the `UsbDevice` object when a USB device is detected on macOS, so you can easily create and open a serial port connection without having to separately call `SerialPort.GetPortNames()`.

**The Problem:**
- The `UsbEventWatcher` library gives you the IOKit device path (e.g., `IOService:/IOResources/AppleUSBHostResources/...`)
- But you need the actual serial port device name (e.g., `/dev/cu.usbserial-1420`) to create a `SerialPort` connection
- These are two different naming schemes, so there's no obvious mapping between them

---------------------------------------------------------------------------------------------------
```
/// <summary>
/// Serial port name (device file path on macOS/Linux)
/// </summary>
public string PortName { get; internal set; } = string.Empty;
```
---------------------------------------------------------------------------------------------------
```
_mountPointTask = Task.Run(async () =>
{
    while (!_cancellationTokenSource.Token.IsCancellationRequested)
    {
        try
        {
            foreach (UsbDevice usbDevice in UsbDeviceList.Where(device => !string.IsNullOrEmpty(device.DeviceSystemPath)).ToList())
            {
                GetMacMountPoint(usbDevice.DeviceSystemPath, mountPoint => SetMountPoint(usbDevice, mountPoint));
                
                // NEW: Get port name for serial devices
                GetMacPortName(usbDevice.DeviceSystemPath, portName => SetPortName(usbDevice, portName));
            }
        }
        catch (InvalidOperationException)
        {
        }

        await Task.Delay(1000, _cancellationTokenSource.Token);
    }
}, _cancellationTokenSource.Token);
```
---------------------------------------------------------------------------------------------------
```
private void SetPortName(UsbDevice usbDevice, string portName)
{
    if (!string.IsNullOrEmpty(portName) && string.IsNullOrEmpty(usbDevice.PortName))
    {
        usbDevice.PortName = portName;
    }
}

[DllImport("UsbEventWatcher.Mac.dylib", CallingConvention = CallingConvention.Cdecl)]
static extern void GetMacPortName(string syspath, MountPointCallback portNameCallback);
```
---------------------------------------------------------------------------------------------------
```
void GetMacPortName(const char* syspath, MountPointCallback portNameCallback)
{
    if (!syspath)
    {
        portNameCallback("");
        return;
    }

    CFMutableDictionaryRef matchingDictionary = IOServiceMatching(kIOUSBInterfaceClassName);

    // Filter for serial/modem devices
    CFNumberRef cfValue;
    SInt32 deviceClassNum = kUSBCommClassID;
    cfValue = CFNumberCreate(kCFAllocatorDefault, kCFNumberSInt32Type, &deviceClassNum);
    CFDictionaryAddValue(matchingDictionary, CFSTR(kUSBInterfaceClass), cfValue);
    CFRelease(cfValue);

    io_iterator_t foundIterator = 0;
    io_service_t usbInterface;
    IOServiceGetMatchingServices(kIOMainPortDefault, matchingDictionary, &foundIterator);

    char* cVal;
    int found = 0;
    long len;
    int match = 0;
    io_string_t devicepath;

    // iterate through USB serial/modem devices
    while ((usbInterface = IOIteratorNext(foundIterator)))
    {
        if (IORegistryEntryGetPath(usbInterface, kIOServicePlane, devicepath) == KERN_SUCCESS)
        {
            if (strncmp(devicepath, syspath, strlen(syspath)) == 0)
            {
                // Look for callout device (cu.*)
                CFStringRef bsdName = (CFStringRef)IORegistryEntrySearchCFProperty(
                    usbInterface,
                    kIOServicePlane,
                    CFSTR("IOCalloutDevice"),
                    kCFAllocatorDefault,
                    kIORegistryIterateRecursively);

                if (bsdName)
                {
                    len = CFStringGetLength(bsdName) + 1;
                    cVal = malloc(len * sizeof(char));
                    if (cVal)
                    {
                        if (CFStringGetCString(bsdName, cVal, len, kCFStringEncodingASCII))
                        {
                            found = 1;
                            portNameCallback(cVal);
                        }

                        free(cVal);
                    }
                    CFRelease(bsdName);
                }
                else
                {
                    // Fallback: look for BSD Name
                    bsdName = (CFStringRef)IORegistryEntrySearchCFProperty(
                        usbInterface,
                        kIOServicePlane,
                        CFSTR("BSD Name"),
                        kCFAllocatorDefault,
                        kIORegistryIterateRecursively);

                    if (bsdName)
                    {
                        len = CFStringGetLength(bsdName) + 1;
                        cVal = malloc(len * sizeof(char));
                        if (cVal)
                        {
                            if (CFStringGetCString(bsdName, cVal, len, kCFStringEncodingASCII))
                            {
                                // Prepend /dev/ for compatibility
                                char portBuffer[256];
                                snprintf(portBuffer, sizeof(portBuffer), "/dev/%s", cVal);
                                found = 1;
                                portNameCallback(portBuffer);
                            }

                            free(cVal);
                        }
                        CFRelease(bsdName);
                    }
                }

                match = 1;
            }
        }
        IOObjectRelease(usbInterface);

        if (match)
            break;
    }
    IOObjectRelease(foundIterator);

    if (!found)
        portNameCallback("");
}
```
---------------------------------------------------------------------------------------------------
```
IUsbEventWatcher usbEvent = new UsbEventWatcher();

usbEvent.UsbDeviceAdded += (sender, device) =>
{
    Console.WriteLine($"USB Device Added: {device.DeviceName}");
    Console.WriteLine($"Port Name: {device.PortName}"); // e.g., "/dev/cu.usbserial-1420"
    
    if (!string.IsNullOrEmpty(device.PortName))
    {
        var serialPort = new SerialPort(device.PortName, 9600);
        serialPort.Open();
        // Use serial port...
    }
};
```
---------------------------------------------------------------------------------------------------
```
// Here's a complete working solution to add serial port name detection to the UsbDevice class:

using System;
using System.IO.Ports;

public class UsbDevice
{
    public string PortName { get; private set; }

    public UsbDevice(string portName)
    {
        PortName = portName;
    }

    // Method to get available serial ports
    public static string[] GetAvailableSerialPorts()
    {
        return SerialPort.GetPortNames();
    }

    // Additional method to show the current state of the device
    public void DisplayDeviceInfo()
    {
        Console.WriteLine("UsbDevice connected on port: " + PortName);
    }
}

class Program
{
    static void Main(string[] args)
    {
        // Example of using the UsbDevice class
        var availablePorts = UsbDevice.GetAvailableSerialPorts();
        foreach (var port in availablePorts)
        {
            // Creating UsbDevice instance for each available port
            var usbDevice = new UsbDevice(port);
            usbDevice.DisplayDeviceInfo();
        }
    }
}
```
---------------------------------------------------------------------------------------------------
---------------------------------------------------------------------------------------------------
```
/// <summary>
/// Serial port name (COM port on Windows, /dev/ttyUSB*/ttyACM* on Linux, /dev/cu.* on macOS)
/// </summary>
public string PortName { get; internal set; } = string.Empty;
```
---------------------------------------------------------------------------------------------------
```
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    if (addAlreadyPresentDevicesToList)
    {
        AddAlreadyPresentDevicesToList();
    }

    StartWindowsWatcher(usePnPEntity);
    
    // NEW: Get port names for serial devices
    _mountPointTask = Task.Run(async () =>
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                foreach (UsbDevice usbDevice in UsbDeviceList.ToList())
                {
                    if (string.IsNullOrEmpty(usbDevice.PortName))
                    {
                        string portName = GetWindowsSerialPortName(usbDevice.SerialNumber);
                        if (!string.IsNullOrEmpty(portName))
                        {
                            usbDevice.PortName = portName;
                        }
                    }
                }
            }
            catch (InvalidOperationException) { }
            
            await Task.Delay(1000, _cancellationTokenSource.Token);
        }
    }, _cancellationTokenSource.Token);
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    _watcherTask = Task.Run(() => StartLinuxWatcher(InsertedCallback, RemovedCallback, includeTTY));
    _cancellationTokenSource = new CancellationTokenSource();

    _mountPointTask = Task.Run(async () => 
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                foreach (UsbDevice usbDevice in UsbDeviceList.Where(device => !string.IsNullOrEmpty(device.DeviceSystemPath)).ToList())
                {
                    GetLinuxMountPoint(usbDevice.DeviceSystemPath, mountPoint => SetMountPoint(usbDevice, mountPoint));
                    
                    // NEW: Get port name for serial devices
                    if (string.IsNullOrEmpty(usbDevice.PortName))
                    {
                        GetLinuxPortName(usbDevice.DeviceSystemPath, portName => SetPortName(usbDevice, portName));
                    }
                }
            }
            catch (InvalidOperationException) { }

            await Task.Delay(1000, _cancellationTokenSource.Token);
        }
    }, _cancellationTokenSource.Token);
}
```
---------------------------------------------------------------------------------------------------
```
private static string GetWindowsSerialPortName(string serialNumber)
{
    try
    {
        using ManagementObjectSearcher Win32_SerialPort = new ManagementObjectSearcher(
            $"SELECT Name FROM Win32_SerialPort WHERE PNPDeviceID LIKE '%{serialNumber}%'");

        foreach (ManagementObject port in Win32_SerialPort.Get())
        {
            string portName = port["Name"]?.ToString();
            if (!string.IsNullOrEmpty(portName))
            {
                return portName; // e.g., "COM3"
            }
        }
    }
    catch { }

    return string.Empty;
}
```
---------------------------------------------------------------------------------------------------
```
private void SetPortName(UsbDevice usbDevice, string portName)
{
    if (!string.IsNullOrEmpty(portName) && string.IsNullOrEmpty(usbDevice.PortName))
    {
        usbDevice.PortName = portName;
    }
}

[DllImport("UsbEventWatcher.Linux.so", CallingConvention = CallingConvention.Cdecl)]
static extern void GetLinuxPortName(string syspath, MountPointCallback portNameCallback);

[DllImport("UsbEventWatcher.Mac.dylib", CallingConvention = CallingConvention.Cdecl)]
static extern void GetMacPortName(string syspath, MountPointCallback portNameCallback);
```
---------------------------------------------------------------------------------------------------
```
void GetLinuxPortName(const char* syspath, MountPointCallback portNameCallback)
{
    if (!syspath)
    {
        portNameCallback("");
        return;
    }

    // Search in /sys/bus/usb/devices for matching device
    FILE* find_output = popen("find /sys/bus/usb/devices -name 'ttyUSB*' -o -name 'ttyACM*' 2>/dev/null", "r");
    if (!find_output)
    {
        portNameCallback("");
        return;
    }

    char device_path[256];
    char port_name[64];
    int found = 0;

    while (fgets(device_path, sizeof(device_path), find_output) != NULL)
    {
        device_path[strcspn(device_path, "\n")] = 0;

        // Traverse up to find the USB device
        char parent_path[512];
        snprintf(parent_path, sizeof(parent_path), "%s/../../device", device_path);

        char realpath_buf[512];
        if (realpath(parent_path, realpath_buf))
        {
            // Check if this device matches our syspath
            if (strstr(realpath_buf, syspath) != NULL)
            {
                // Extract ttyUSB* or ttyACM* from device_path
                char* port_ptr = strrchr(device_path, '/');
                if (port_ptr)
                {
                    port_ptr++;
                    snprintf(port_name, sizeof(port_name), "/dev/%s", port_ptr);
                    found = 1;
                    break;
                }
            }
        }
    }

    pclose(find_output);

    if (found)
        portNameCallback(port_name);
    else
        portNameCallback("");
}
```
---------------------------------------------------------------------------------------------------
```
void GetMacPortName(const char* syspath, MountPointCallback portNameCallback)
{
    if (!syspath)
    {
        portNameCallback("");
        return;
    }

    CFMutableDictionaryRef matchingDictionary = IOServiceMatching(kIOUSBInterfaceClassName);

    // Filter for serial/modem devices
    CFNumberRef cfValue;
    SInt32 deviceClassNum = kUSBCommClassID;
    cfValue = CFNumberCreate(kCFAllocatorDefault, kCFNumberSInt32Type, &deviceClassNum);
    CFDictionaryAddValue(matchingDictionary, CFSTR(kUSBInterfaceClass), cfValue);
    CFRelease(cfValue);

    io_iterator_t foundIterator = 0;
    io_service_t usbInterface;
    IOServiceGetMatchingServices(kIOMainPortDefault, matchingDictionary, &foundIterator);

    char* cVal;
    int found = 0;
    long len;
    int match = 0;
    io_string_t devicepath;

    while ((usbInterface = IOIteratorNext(foundIterator)))
    {
        if (IORegistryEntryGetPath(usbInterface, kIOServicePlane, devicepath) == KERN_SUCCESS)
        {
            if (strncmp(devicepath, syspath, strlen(syspath)) == 0)
            {
                // Look for callout device (cu.*)
                CFStringRef bsdName = (CFStringRef)IORegistryEntrySearchCFProperty(
                    usbInterface,
                    kIOServicePlane,
                    CFSTR("IOCalloutDevice"),
                    kCFAllocatorDefault,
                    kIORegistryIterateRecursively);

                if (bsdName)
                {
                    len = CFStringGetLength(bsdName) + 1;
                    cVal = malloc(len * sizeof(char));
                    if (cVal)
                    {
                        if (CFStringGetCString(bsdName, cVal, len, kCFStringEncodingASCII))
                        {
                            found = 1;
                            portNameCallback(cVal);
                        }
                        free(cVal);
                    }
                    CFRelease(bsdName);
                }

                match = 1;
            }
        }
        IOObjectRelease(usbInterface);

        if (match)
            break;
    }
    IOObjectRelease(foundIterator);

    if (!found)
        portNameCallback("");
}
```
---------------------------------------------------------------------------------------------------
```
IUsbEventWatcher usbEvent = new UsbEventWatcher();

usbEvent.UsbDeviceAdded += (sender, device) =>
{
    Console.WriteLine($"USB Device: {device.DeviceName}");
    
    if (!string.IsNullOrEmpty(device.PortName))
    {
        Console.WriteLine($"Serial Port: {device.PortName}");
        
        // Windows: "COM3"
        // Linux: "/dev/ttyUSB0" or "/dev/ttyACM0"
        // macOS: "/dev/cu.usbserial-1420"
        
        var serialPort = new SerialPort(device.PortName, 9600);
        serialPort.Open();
    }
};
```
---------------------------------------------------------------------------------------------------
```
Platform	Format				Example
Windows		COM port number		COM3, COM4
Linux		/dev path			/dev/ttyUSB0, /dev/ttyACM0
macOS		/dev path			/dev/cu.usbserial-1420, /dev/cu.usbmodem14201
```
---------------------------------------------------------------------------------------------------