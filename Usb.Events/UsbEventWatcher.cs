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
    public class UsbEventWatcher : IUsbEventWatcher, IDisposable
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

        private ManagementEventWatcher _volumeChangeEventWatcher = null!;

        private ManagementEventWatcher _USBControllerDeviceCreationEventWatcher = null!;
        private ManagementEventWatcher _USBControllerDeviceDeletionEventWatcher = null!;

        #endregion

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

		public UsbEventWatcher(string subsystem = "usb")
        {
			Start(subsystem);
		}

        #region Methods

		private void Start(string subsystem)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
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
				Task.Run(() => StartLinuxWatcher(InsertedCallback, RemovedCallback, subsystem));

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
        static extern void StartLinuxWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback);

        [DllImport("UsbEventWatcher.Mac.dylib", CallingConvention = CallingConvention.Cdecl)]
        static extern void GetMacMountPoint(string syspath, MountPointCallback mountPointCallback);

        [DllImport("UsbEventWatcher.Mac.dylib", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartMacWatcher(UsbDeviceCallback insertedCallback, UsbDeviceCallback removedCallback);

        #endregion

        #region Windows methods

        private void StartWindowsWatcher()
        {
            UsbDrivePathList = new List<string>(DriveInfo.GetDrives()
                .Where(driveInfo => driveInfo.DriveType == DriveType.Removable && driveInfo.IsReady)
                .Select(driveInfo => driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar)));

            _volumeChangeEventWatcher = new ManagementEventWatcher();
            _volumeChangeEventWatcher.EventArrived += new EventArrivedEventHandler(VolumeChangeEventWatcher_EventArrived);
            _volumeChangeEventWatcher.Query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2 or EventType = 3");
            _volumeChangeEventWatcher.Start();

            _USBControllerDeviceCreationEventWatcher = new ManagementEventWatcher();
            _USBControllerDeviceCreationEventWatcher.EventArrived += new EventArrivedEventHandler(USBControllerDeviceCreationEventWatcher_EventArrived);
            _USBControllerDeviceCreationEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBControllerDevice'");
            _USBControllerDeviceCreationEventWatcher.Start();

            _USBControllerDeviceDeletionEventWatcher = new ManagementEventWatcher();
            _USBControllerDeviceDeletionEventWatcher.EventArrived += new EventArrivedEventHandler(USBControllerDeviceDeletionEventWatcher_EventArrived);
            _USBControllerDeviceDeletionEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBControllerDevice'");
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

            string deviceID = GetUSBControllerDeviceID(Win32_USBControllerDevice);

            UsbDevice? usbDevice = GetUsbDevice(deviceID);

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

            string deviceID = GetUSBControllerDeviceID(Win32_USBControllerDevice);

            UsbDevice? usbDevice = GetUsbDevice(deviceID);

            if (usbDevice != null)
            {
                usbDevice.IsEjected = true;
                usbDevice.IsMounted = false;

                OnDeviceRemoved(usbDevice);
            }
        }

        private static string GetUSBControllerDeviceID(ManagementBaseObject Win32_USBControllerDevice)
        {
            DebugOutput(Win32_USBControllerDevice);

            string dependent = Win32_USBControllerDevice["Dependent"].ToString();
            string[] dependentInfo = dependent.Split('"');

            if (dependentInfo.Length < 2)
                return string.Empty;

            return dependentInfo[1].Replace(@"\\", @"\");
        }

        private static UsbDevice? GetUsbDevice(string deviceID)
        {
            string[] deviceInfo = deviceID.Split('\\');

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
                DeviceSystemPath = deviceID,
                ProductID = productId,
                SerialNumber = serial,
                VendorID = vendorId
            };

            string WindowsPortableDevicesClassGuid = "{eec5ad98-8080-425f-922a-dabf3de3f69a}";

#if DEBUG
            using ManagementObjectSearcher Win32_PnPEntity = new ManagementObjectSearcher($"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%{serial}%'");
#else
            using ManagementObjectSearcher Win32_PnPEntity = new ManagementObjectSearcher($"SELECT Caption, ClassGuid, Description, Manufacturer FROM Win32_PnPEntity WHERE DeviceID LIKE '%{serial}%'");
#endif

            foreach (ManagementObject entity in Win32_PnPEntity.Get())
            {
                DebugOutput(entity);

                usbDevice.DeviceName = entity["Caption"]?.ToString()?.Trim() ?? string.Empty;
                usbDevice.Product = entity["Description"]?.ToString()?.Trim() ?? string.Empty;
                usbDevice.ProductDescription = entity["Description"]?.ToString()?.Trim() ?? string.Empty;
                usbDevice.Vendor = entity["Manufacturer"]?.ToString()?.Trim() ?? string.Empty;
                usbDevice.VendorDescription = entity["Manufacturer"]?.ToString()?.Trim() ?? string.Empty;

                string ClassGuid = entity["ClassGuid"]?.ToString()?.Trim() ?? string.Empty;

                // most USB devices have only one ManagementObject that contains Caption, Description, Manufacturer
                // but USB flash drives have several ManagementObject-s and only WindowsPortableDevices has useful info
                if (ClassGuid == WindowsPortableDevicesClassGuid)
                {
                    break;
                }
            }

            using ManagementObjectSearcher Win32_USBHub = new ManagementObjectSearcher($"SELECT * FROM Win32_USBHub WHERE DeviceID LIKE '%{serial}%'");

            if (Win32_USBHub.Get().Count > 0)
            {
                int attempts = 0;

                while (++attempts < 9000)
                {
                    usbDevice.MountedDirectoryPath = GetDevicePath(serial);

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

            return usbDevice;
        }

        private static string GetDevicePath(string serial)
        {
            string devicePath = string.Empty;

            using ManagementObjectSearcher Win32_DiskDrive = new ManagementObjectSearcher(
                $"SELECT DeviceID FROM Win32_DiskDrive WHERE PNPDeviceID LIKE '%{serial}%'");

            foreach (ManagementObject drive in Win32_DiskDrive.Get())
            {
                using ManagementObjectSearcher Win32_DiskDriveToDiskPartition = new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + drive["DeviceID"] + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

                foreach (ManagementObject partition in Win32_DiskDriveToDiskPartition.Get())
                {
                    using ManagementObjectSearcher Win32_LogicalDiskToPartition = new ManagementObjectSearcher(
                        "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + partition["DeviceID"] + "'} WHERE AssocClass = Win32_LogicalDiskToPartition");

                    foreach (ManagementObject disk in Win32_LogicalDiskToPartition.Get())
                    {
                        devicePath = disk["DeviceID"].ToString();
                    }
                }
            }

            return devicePath;
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();

            _volumeChangeEventWatcher.Stop();
            _volumeChangeEventWatcher.Dispose();

            _USBControllerDeviceCreationEventWatcher.Stop();
            _USBControllerDeviceCreationEventWatcher.Dispose();

            _USBControllerDeviceDeletionEventWatcher.Stop();
            _USBControllerDeviceDeletionEventWatcher.Dispose();
        }

        #endregion
    }
}
