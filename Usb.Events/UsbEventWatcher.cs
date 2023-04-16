using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Usb.Events
{
    public class UsbEventWatcher : IUsbEventWatcher
    {
        #region IUsbEventWatcher

        public List<string> UsbDrivePathList { get; private set; } = new List<string>();
        public List<UsbDevice> UsbDeviceList { get; private set; } = new List<UsbDevice>();

        public event EventHandler<string>? UsbDriveMounted;
        public event EventHandler<string>? UsbDriveEjected;

        public event EventHandler<UsbDevice>? UsbDeviceAdded;
        public event EventHandler<UsbDevice>? UsbDeviceRemoved;

        #endregion

        #region Windows fields

        private ManagementEventWatcher? _volumeChangeEventWatcher;

        private ManagementEventWatcher? _USBControllerDeviceCreationEventWatcher;
        private ManagementEventWatcher? _USBControllerDeviceDeletionEventWatcher;

        #endregion

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isRunning;

        public UsbEventWatcher(bool startImmediately = true, bool getAlreadyPresentDevices = false, bool includeTTY = false)
        {
            if (startImmediately)
            {
                Start(getAlreadyPresentDevices, includeTTY);
            }
        }

        #region Methods

        public void Start(bool getAlreadyPresentDevices = false, bool includeTTY = false)
        {
            if (_isRunning)
                return;

            _isRunning = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (getAlreadyPresentDevices)
                {
                    GetAlreadyPresentDevices();
                }

                StartWindowsWatcher();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Task.Run(() => StartMacWatcher(InsertedCallback, RemovedCallback));

                Task.Run(async () =>
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        foreach (UsbDevice usbDevice in UsbDeviceList.Where(device => !string.IsNullOrEmpty(device.DeviceSystemPath)))
                        {
                            GetMacMountPoint(usbDevice.DeviceSystemPath, mountPoint => SetMountPoint(usbDevice, mountPoint));
                        }

                        await Task.Delay(1000);
                    }
                }, _cancellationTokenSource.Token);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Task.Run(() => StartLinuxWatcher(InsertedCallback, RemovedCallback, includeTTY));

                Task.Run(async () => 
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        foreach (UsbDevice usbDevice in UsbDeviceList.Where(device => !string.IsNullOrEmpty(device.DeviceSystemPath)))
                        {
                            GetLinuxMountPoint(usbDevice.DeviceSystemPath, mountPoint => SetMountPoint(usbDevice, mountPoint));
                        }

                        await Task.Delay(1000);
                    }
                }, _cancellationTokenSource.Token);
            }
        }

        private void SetMountPoint(UsbDevice usbDevice, string mountPoint)
        {
            if (string.IsNullOrEmpty(usbDevice.MountedDirectoryPath) && !string.IsNullOrEmpty(mountPoint))
            {
                usbDevice.MountedDirectoryPath = mountPoint;
                OnDriveInserted(usbDevice.MountedDirectoryPath);

                usbDevice.IsEjected = false;
                usbDevice.IsMounted = true;
            }
            else if (!string.IsNullOrEmpty(usbDevice.MountedDirectoryPath) && string.IsNullOrEmpty(mountPoint))
            {
                OnDriveRemoved(usbDevice.MountedDirectoryPath);
                usbDevice.MountedDirectoryPath = mountPoint;

                usbDevice.IsEjected = true;
                usbDevice.IsMounted = false;
            }
        }

        private void OnDriveInserted(string path)
        {
            UsbDriveMounted?.Invoke(this, path);
            UsbDrivePathList.Add(path);
        }

        private void OnDriveRemoved(string path)
        {
            UsbDriveEjected?.Invoke(this, path);
            UsbDrivePathList.RemoveAll(p => p == path);
        }

        private void OnDeviceInserted(UsbDevice usbDevice)
        {
            UsbDeviceAdded?.Invoke(this, usbDevice);
            UsbDeviceList.Add(usbDevice);
        }

        private void OnDeviceRemoved(UsbDevice usbDevice)
        {
            UsbDeviceRemoved?.Invoke(this, usbDevice);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                UsbDeviceList.RemoveAll(device => device.DeviceName == usbDevice.DeviceName && device.DeviceSystemPath == usbDevice.DeviceSystemPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                UsbDeviceList.RemoveAll(device => device.ProductID == usbDevice.ProductID && device.VendorID == usbDevice.VendorID && device.SerialNumber == usbDevice.SerialNumber);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                UsbDeviceList.RemoveAll(device => device.SerialNumber == usbDevice.SerialNumber);
            }
        }

        #endregion

        #region Linux and Mac methods

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        delegate void UsbDeviceCallback(UsbDeviceData usbDevice);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        delegate void MountPointCallback(string mountPoint);

        private void InsertedCallback(UsbDeviceData usbDevice)
        {
            OnDeviceInserted(new UsbDevice(usbDevice));
        }

        private void RemovedCallback(UsbDeviceData usbDevice)
        {
            OnDeviceRemoved(new UsbDevice(usbDevice));
        }

        [DllImport("UsbEventWatcher.Linux.so", CallingConvention = CallingConvention.Cdecl)]
        static extern void GetLinuxMountPoint(string syspath, MountPointCallback mountPointCallback);

        [DllImport("UsbEventWatcher.Linux.so", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartLinuxWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback, bool includeTTY);

        [DllImport("UsbEventWatcher.Mac.dylib", CallingConvention = CallingConvention.Cdecl)]
        static extern void GetMacMountPoint(string syspath, MountPointCallback mountPointCallback);

        [DllImport("UsbEventWatcher.Mac.dylib", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartMacWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback);

        #endregion

        /*
        Product: DT_101_II
        Product Description: DataTraveler 101 II
        Product ID: 1625
        Serial Number: 0019E06B9C85F9A0F7550C20
        Vendor: Kingston
        Vendor Description: Kingston Technology
        Vendor ID: 0951

        Product: DataTraveler_2.0
        Product Description: Kingston DataTraveler 102/2.0 / HEMA Flash Drive 2 GB / PNY Attache 4GB Stick
        Product ID: 6545
        Serial Number: 6CF049E0FBE3B0A069B24172
        Vendor: Kingston
        Vendor Description: Toshiba Corp.
        Vendor ID: 0930

        Product: Mass_Storage_Device
        Product Description: JetFlash
        Product ID: 1000
        Serial Number: 08ZLSF32M9ZNUJRJ
        Vendor: JetFlash
        Vendor Description: Transcend Information, Inc.
        Vendor ID: 8564

        Product: USB_Flash_Memory
        Product Description: Kingston DataTraveler 102/2.0 / HEMA Flash Drive 2 GB / PNY Attache 4GB Stick
        Product ID: 6545
        Serial Number: 0BB1B8700301387D
        Vendor: 0930
        Vendor Description: Toshiba Corp.
        Vendor ID: 0930
        /**/

        #region Windows methods

        private void GetAlreadyPresentDevices()
        {
            using ManagementObjectSearcher Win32_USBControllerDevice = new ManagementObjectSearcher($"SELECT * FROM Win32_USBControllerDevice");

            foreach (ManagementObject USBControllerDevice in Win32_USBControllerDevice.Get())
            {
                (string USBControllerDeviceID, string PnPEntityDeviceID) = GetUSBControllerDeviceID(USBControllerDevice);

                UsbDevice? usbDevice = GetUsbDevice(PnPEntityDeviceID);

                if (usbDevice != null)
                {
                    GetData(usbDevice);

                    usbDevice.IsEjected = false;
                    usbDevice.IsMounted = true;

                    UsbDeviceList.Add(usbDevice);
                }
            }
        }

        private void StartWindowsWatcher()
        {
            UsbDrivePathList = new List<string>(DriveInfo.GetDrives()
                .Where(driveInfo => driveInfo.DriveType == DriveType.Removable && driveInfo.IsReady)
                .Select(driveInfo => driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar)));

            //_deviceChangeEventWatcher = new ManagementEventWatcher();
            //_deviceChangeEventWatcher.EventArrived += new EventArrivedEventHandler(DeviceChangeEventWatcher_EventArrived);
            //_deviceChangeEventWatcher.Query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2 or EventType = 3"); -licence key detected 3x, no info
            //_deviceChangeEventWatcher.Start();

            _volumeChangeEventWatcher = new ManagementEventWatcher();
            _volumeChangeEventWatcher.EventArrived += new EventArrivedEventHandler(VolumeChangeEventWatcher_EventArrived);
            _volumeChangeEventWatcher.Query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2 or EventType = 3");
            _volumeChangeEventWatcher.Start();

            _USBControllerDeviceCreationEventWatcher = new ManagementEventWatcher();
            _USBControllerDeviceCreationEventWatcher.EventArrived += new EventArrivedEventHandler(USBControllerDeviceCreationEventWatcher_EventArrived);
            //_USBControllerDeviceCreationEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'"); - licence key not detected, USB disk detected
            //_USBControllerDeviceCreationEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'"); // high CPU load
            //_USBControllerDeviceCreationEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBController'"); - nothing detected
            _USBControllerDeviceCreationEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBControllerDevice'"); // -licence key detected 2x, no info
            _USBControllerDeviceCreationEventWatcher.Start();

            _USBControllerDeviceDeletionEventWatcher = new ManagementEventWatcher();
            _USBControllerDeviceDeletionEventWatcher.EventArrived += new EventArrivedEventHandler(USBControllerDeviceDeletionEventWatcher_EventArrived);
            //_USBControllerDeviceDeletionEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'"); - licence key not detected, USB disk detected
            //_USBControllerDeviceDeletionEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'"); // high CPU load
            //_USBControllerDeviceDeletionEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBController'"); - nothing detected
            _USBControllerDeviceDeletionEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBControllerDevice'"); // -licence key detected 2x, no info
            _USBControllerDeviceDeletionEventWatcher.Start();
        }

        private void VolumeChangeEventWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject Win32_VolumeChangeEvent = e.NewEvent;

            string driveName = Win32_VolumeChangeEvent["DriveName"].ToString();
            string eventType = Win32_VolumeChangeEvent["EventType"].ToString();

            bool inserted = eventType == "2";
            bool removed = eventType == "3";

            if (inserted)
            {
                foreach (UsbDevice usbDevice in UsbDeviceList.Where(device => device.MountedDirectoryPath == driveName))
                {
                    usbDevice.IsEjected = false;
                    usbDevice.IsMounted = true;
                }

                OnDriveInserted(driveName);
            }

            if (removed)
            {
                foreach (UsbDevice usbDevice in UsbDeviceList.Where(device => device.MountedDirectoryPath == driveName))
                {
                    usbDevice.IsEjected = true;
                    usbDevice.IsMounted = false;
                }

                OnDriveRemoved(driveName);
            }
        }

        private static void DebugOutput(ManagementBaseObject managementBaseObject)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(string.Empty);

            foreach (PropertyData property in managementBaseObject.Properties)
            {
                System.Diagnostics.Debug.WriteLine(property.Name + " = " + property.Value);
            }

            System.Diagnostics.Debug.WriteLine(string.Empty);
#endif
        }

        private void USBControllerDeviceCreationEventWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject Win32_USBControllerDevice = (ManagementBaseObject)e.NewEvent["TargetInstance"];

            (string USBControllerDeviceID, string PnPEntityDeviceID) = GetUSBControllerDeviceID(Win32_USBControllerDevice);

            UsbDevice? usbDevice = GetUsbDevice(USBControllerDeviceID, PnPEntityDeviceID);

            if (usbDevice != null)
            {
                usbDevice.IsEjected = false;
                usbDevice.IsMounted = true;

                OnDeviceInserted(usbDevice);
            }
        }

        private void USBControllerDeviceDeletionEventWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject Win32_USBControllerDevice = (ManagementBaseObject)e.NewEvent["TargetInstance"];

            (string USBControllerDeviceID, string PnPEntityDeviceID) = GetUSBControllerDeviceID(Win32_USBControllerDevice);

            UsbDevice? usbDevice = GetUsbDevice(USBControllerDeviceID, PnPEntityDeviceID);

            if (usbDevice != null)
            {
                usbDevice.IsEjected = true;
                usbDevice.IsMounted = false;

                OnDeviceRemoved(usbDevice);
            }
        }

        private static (string USBControllerDeviceID, string PnPEntityDeviceID) GetUSBControllerDeviceID(ManagementBaseObject Win32_USBControllerDevice)
        {
            DebugOutput(Win32_USBControllerDevice);

            string Win32_USBController = Win32_USBControllerDevice["Antecedent"].ToString();
            string[] antecedentInfo = Win32_USBController.Split('"');

            string Win32_PnPEntity = Win32_USBControllerDevice["Dependent"].ToString();
            string[] dependentInfo = Win32_PnPEntity.Split('"');

            if (antecedentInfo.Length < 2 || dependentInfo.Length < 2)
                return (string.Empty, string.Empty);

            return (antecedentInfo[1].Replace(@"\\", @"\"), dependentInfo[1].Replace(@"\\", @"\"));
        }

        private static UsbDevice? GetUsbDevice(string PnPEntityDeviceID)
        {
            string[] deviceInfo = PnPEntityDeviceID.Split('\\');

            if (deviceInfo.Length < 3)
                return null;

            string[] id = deviceInfo[1].Split('&');

            if (id.Length < 2)
                return null;

            if (id[0].Length != 8 || !id[0].StartsWith("VID_"))
                return null;

            string vendorId = id[0].Substring(4, 4);

            if (id[1].Length != 8 || !id[1].StartsWith("PID_"))
                return null;

            string productId = id[1].Substring(4, 4);

            string serial = deviceInfo[2];

            UsbDevice usbDevice = new UsbDevice
            {
                DeviceSystemPath = PnPEntityDeviceID,
                ProductID = productId,
                SerialNumber = serial,
                VendorID = vendorId
            };

            // TODO::
            // get VID, PID from "USB\" and drive letter from "USBSTOR\"
            // this doesn't depend on: LIKE '%{serial}%' - in case that "USB\" and "USBSTOR\" don't end with the same serial
            // check if Win32_PnPEntity WHERE DeviceID="USB\" and "USBSTOR\" return different results

            return usbDevice;
        }

        private static UsbDevice? GetUsbDevice(string USBControllerDeviceID, string PnPEntityDeviceID)
        {
            UsbDevice? usbDevice = GetUsbDevice(PnPEntityDeviceID);

            if (usbDevice == null)
                return null;

            string diskDriveDeviceID = GetData(usbDevice);

            GetMountedDirectoryPath(usbDevice, diskDriveDeviceID, USBControllerDeviceID);

            return usbDevice;
        }

        private static string GetData(UsbDevice usbDevice)
        {
            const string WindowsPortableDevicesClassGuid = "{eec5ad98-8080-425f-922a-dabf3de3f69a}";

#if DEBUG
            using ManagementObjectSearcher Win32_PnPEntity = new ManagementObjectSearcher($"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%{usbDevice.SerialNumber}%'");
#else
            using ManagementObjectSearcher Win32_PnPEntity = new ManagementObjectSearcher($"SELECT Caption, ClassGuid, Description, DeviceID, Manufacturer FROM Win32_PnPEntity WHERE DeviceID LIKE '%{usbDevice.SerialNumber}%'");
#endif

            bool parseData = true;

            string diskDriveDeviceID = string.Empty;

            foreach (ManagementObject entity in Win32_PnPEntity.Get())
            {
                DebugOutput(entity);

                // https://stackoverflow.com/questions/9525996/how-can-i-detect-whether-a-garmin-gps-device-is-connected-in-mass-storage-mode

                using ManagementObjectSearcher Win32_DiskDrive = new ManagementObjectSearcher(
                        "ASSOCIATORS OF {Win32_PnPEntity.DeviceID=\"" + entity["DeviceID"].ToString().Replace(@"\", @"\\") + "\"} WHERE ResultClass = Win32_DiskDrive");

                foreach (ManagementObject diskDrive in Win32_DiskDrive.Get())
                {
                    DebugOutput(diskDrive);

                    diskDriveDeviceID = diskDrive["DeviceID"].ToString();
                }

                if (parseData)
                {
                    usbDevice.DeviceName = entity["Caption"]?.ToString()?.Trim() ?? string.Empty;
                    usbDevice.Product = entity["Description"]?.ToString()?.Trim() ?? string.Empty;
                    usbDevice.ProductDescription = entity["Description"]?.ToString()?.Trim() ?? string.Empty;
                    usbDevice.Vendor = entity["Manufacturer"]?.ToString()?.Trim() ?? string.Empty;
                    usbDevice.VendorDescription = entity["Manufacturer"]?.ToString()?.Trim() ?? string.Empty;
                }

                string ClassGuid = entity["ClassGuid"]?.ToString()?.Trim() ?? string.Empty;

                // most USB devices have only one ManagementObject that contains Caption, Description, Manufacturer
                // but USB flash drives have several ManagementObject-s and only WindowsPortableDevices has useful info
                if (ClassGuid == WindowsPortableDevicesClassGuid)
                {
                    parseData = false;
                }

                /*
                Storage Volumes
                Class = Volume
                ClassGuid = {71a27cdd-812a-11d0-bec7-08002be2092f}
                DeviceID = STORAGE\VOLUME\_??_USBSTOR#DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00#0019E06B9C85F9A0F7550C20&0#{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}
                PNPDeviceID = STORAGE\VOLUME\_??_USBSTOR#DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00#0019E06B9C85F9A0F7550C20&0#{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}

                Windows Portable Devices (WPD)
                Class = WPD
                ClassGuid = {eec5ad98-8080-425f-922a-dabf3de3f69a}
                Caption = IRM_CCSA_X64FRE_EN-US_DV5
                Description = DT 101 II
                Manufacturer = Kingston
                Name = IRM_CCSA_X64FRE_EN-US_DV5
                DeviceID = SWD\WPDBUSENUM\_??_USBSTOR#DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00#0019E06B9C85F9A0F7550C20&0#{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}
                PNPDeviceID = SWD\WPDBUSENUM\_??_USBSTOR#DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00#0019E06B9C85F9A0F7550C20&0#{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}

                USB Bus Devices (hubs and host controllers)
                Class = USB
                ClassGuid = {36fc9e60-c465-11cf-8056-444553540000}
                DeviceID = USB\VID_0951&PID_1625\0019E06B9C85F9A0F7550C20
                PNPDeviceID = USB\VID_0951&PID_1625\0019E06B9C85F9A0F7550C20

                Disk Drives
                Class = DiskDrive
                ClassGuid = {4d36e967-e325-11ce-bfc1-08002be10318}
                Caption = Kingston DT 101 II USB Device
                Name = Kingston DT 101 II USB Device
                DeviceID = USBSTOR\DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00\0019E06B9C85F9A0F7550C20&0
                PNPDeviceID = USBSTOR\DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00\0019E06B9C85F9A0F7550C20&0
                /**/
            }

            return diskDriveDeviceID;
        }

        private static void GetMountedDirectoryPath(UsbDevice usbDevice, string diskDriveDeviceID, string USBControllerDeviceID)
        {
            using ManagementObjectSearcher Win32_USBHub = new ManagementObjectSearcher($"SELECT * FROM Win32_USBHub WHERE DeviceID LIKE '%{usbDevice.SerialNumber}%'");

            ManagementObjectCollection USBHubCollection = Win32_USBHub.Get();

            if (USBHubCollection.Count > 0)
            {
                int attempts = 0;

                while (++attempts < 1000)
                {
                    usbDevice.MountedDirectoryPath = GetDevicePath(usbDevice.SerialNumber);

                    if (string.IsNullOrEmpty(usbDevice.MountedDirectoryPath) && !string.IsNullOrEmpty(diskDriveDeviceID))
                    {
                        usbDevice.MountedDirectoryPath = GetDiskDrivePath(diskDriveDeviceID);
                    }

                    if (string.IsNullOrEmpty(usbDevice.MountedDirectoryPath))
                    {
                        // https://stackoverflow.com/questions/20143264/find-windows-drive-letter-of-a-removable-disk-from-usb-vid-pid

                        string USBHubDeviceID = string.Empty;

                        foreach (ManagementObject USBHub in USBHubCollection)
                        {
                            USBHubDeviceID = USBHub["DeviceID"].ToString();
                        }

                        List<string> DeviceIDList = new List<string>();

                        using ManagementObjectSearcher associators = new ManagementObjectSearcher(
                            "ASSOCIATORS OF {Win32_USBController.DeviceID=\"" + USBControllerDeviceID.Replace(@"\", @"\\") + "\"}");

                        foreach (ManagementObject associator in associators.Get())
                        {
                            foreach (PropertyData propertyData in associator.Properties)
                            {
                                if (propertyData.Name == "DeviceID")
                                {
                                    DeviceIDList.Add(associator["DeviceID"].ToString());
                                    break;
                                }
                            }
                        }

                        // This is working under the assumption that in the list of all devices of Win32_USBController
                        // Win32_DiskDrive DeviceID="USBSTOR\DISK&VEN_....&PROD_....&REV_....
                        // comes directly after
                        // Win32_USBHub DeviceID="USB\\VID_....&PID_....
                        // in case there are several disk drives

                        int index = DeviceIDList.IndexOf(USBHubDeviceID);

                        if (index != -1)
                        {
                            for (int i = index; i < DeviceIDList.Count; ++i)
                            {
                                if (DeviceIDList[i].Contains("USBSTOR"))
                                {
                                    using ManagementObjectSearcher Win32_DiskDrive = new ManagementObjectSearcher(
                                        $"SELECT DeviceID FROM Win32_DiskDrive WHERE PNPDeviceID = '{DeviceIDList[i].Replace(@"\", @"\\")}'");

                                    foreach (ManagementObject diskDrive in Win32_DiskDrive.Get())
                                    {
                                        usbDevice.MountedDirectoryPath = GetDiskDrivePath(diskDrive["DeviceID"].ToString());
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(usbDevice.MountedDirectoryPath))
                    {
                        System.Threading.Thread.Sleep(1);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private static string GetDevicePath(string serial)
        {
            string devicePath = string.Empty;

            using ManagementObjectSearcher Win32_DiskDrive = new ManagementObjectSearcher(
                $"SELECT DeviceID FROM Win32_DiskDrive WHERE PNPDeviceID LIKE '%{serial}%'");

            foreach (ManagementObject diskDrive in Win32_DiskDrive.Get())
            {
                devicePath = GetDiskDrivePath(diskDrive["DeviceID"].ToString());
            }

            return devicePath;
        }

        private static string GetDiskDrivePath(string diskDriveDeviceID)
        {
            string devicePath = string.Empty;

            using ManagementObjectSearcher Win32_DiskDriveToDiskPartition = new ManagementObjectSearcher(
                "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + diskDriveDeviceID + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

            foreach (ManagementObject diskPartition in Win32_DiskDriveToDiskPartition.Get())
            {
                using ManagementObjectSearcher Win32_LogicalDiskToPartition = new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + diskPartition["DeviceID"] + "'} WHERE AssocClass = Win32_LogicalDiskToPartition");

                foreach (ManagementObject logicalDisk in Win32_LogicalDiskToPartition.Get())
                {
                    devicePath = logicalDisk["DeviceID"].ToString();
                }
            }

            return devicePath;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            _volumeChangeEventWatcher?.Stop();
            _volumeChangeEventWatcher?.Dispose();

            _USBControllerDeviceCreationEventWatcher?.Stop();
            _USBControllerDeviceCreationEventWatcher?.Dispose();

            _USBControllerDeviceDeletionEventWatcher?.Stop();
            _USBControllerDeviceDeletionEventWatcher?.Dispose();
        }

        #endregion
    }
}
