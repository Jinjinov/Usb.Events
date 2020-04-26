using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Usb.Events
{
    public class UsbEventWatcher : IUsbEventWatcher, IDisposable
    {
        #region IUsbEventWatcher

        public IList<string> UsbDrivePathList { get; private set; } = new List<string>();
        public IList<UsbDevice> UsbDeviceList { get; private set; } = new List<UsbDevice>();

        public event EventHandler<string>? UsbDriveInserted;
        public event EventHandler<string>? UsbDriveRemoved;

        public event EventHandler<UsbDevice>? UsbDeviceInserted;
        public event EventHandler<UsbDevice>? UsbDeviceRemoved;

        #endregion

        #region Windows fields

        private ManagementEventWatcher _volumeChangeEventWatcher = null!;
        private ManagementEventWatcher _instanceCreationEventWatcher = null!;
        private ManagementEventWatcher _instanceDeletionEventWatcher = null!;

        #endregion

        public UsbEventWatcher()
        {
            Start();
        }

        #region Methods

        private void Start()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                StartWindowsWatcher();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                Task.Run(() => StartMacWatcher(InsertedCallback, RemovedCallback));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Task.Run(() => StartLinuxWatcher(InsertedCallback, RemovedCallback));
            }
        }

        private void OnDriveInserted(string path)
        {
            UsbDriveInserted?.Invoke(this, path);
            UsbDrivePathList.Add(path);
        }

        private void OnDriveRemoved(string path)
        {
            UsbDriveRemoved?.Invoke(this, path);

            if (UsbDrivePathList.Contains(path))
                UsbDrivePathList.Remove(path);
        }

        private void OnDeviceInserted(UsbDevice usbDevice)
        {
            UsbDeviceInserted?.Invoke(this, usbDevice);
            UsbDeviceList.Add(usbDevice);
        }

        private void OnDeviceRemoved(UsbDevice usbDevice)
        {
            UsbDeviceRemoved?.Invoke(this, usbDevice);

            if (UsbDeviceList.Any(device => device.DeviceName == usbDevice.DeviceName || device.DevicePath == usbDevice.DevicePath))
                UsbDeviceList.Remove(UsbDeviceList.First(device => device.DeviceName == usbDevice.DeviceName || device.DevicePath == usbDevice.DevicePath));
        }

        #endregion

        #region Linux and Mac methods

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        delegate void WatcherCallback(UsbDevice usbDevice);

        private void InsertedCallback(UsbDevice usbDevice)
        {
            OnDriveInserted(usbDevice.DevicePath);
            OnDeviceInserted(usbDevice);
        }

        private void RemovedCallback(UsbDevice usbDevice)
        {
            OnDriveRemoved(usbDevice.DevicePath);
            OnDeviceRemoved(usbDevice);
        }

        [DllImport("UsbEventWatcher.Linux.so", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartLinuxWatcher(WatcherCallback insertedCallback, WatcherCallback removedCallback);

        [DllImport("UsbEventWatcher.Mac.dylib", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartMacWatcher(WatcherCallback insertedCallback, WatcherCallback removedCallback);

        #endregion

        #region Windows methods

        private void StartWindowsWatcher()
        {
            UsbDrivePathList = new List<string>(DriveInfo.GetDrives()
                .Where(driveInfo => driveInfo.DriveType == DriveType.Removable && driveInfo.IsReady)
                .Select(driveInfo => driveInfo.Name.EndsWith(Path.DirectorySeparatorChar.ToString()) ? driveInfo.Name : driveInfo.Name + Path.DirectorySeparatorChar));

            _volumeChangeEventWatcher = new ManagementEventWatcher();
            _volumeChangeEventWatcher.EventArrived += new EventArrivedEventHandler(VolumeChangeEventWatcher_EventArrived);
            _volumeChangeEventWatcher.Query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2 or EventType = 3");
            _volumeChangeEventWatcher.Start();

            _instanceCreationEventWatcher = new ManagementEventWatcher();
            _instanceCreationEventWatcher.EventArrived += new EventArrivedEventHandler(InstanceCreationEventWatcher_EventArrived);
            _instanceCreationEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");
            _instanceCreationEventWatcher.Start();

            _instanceDeletionEventWatcher = new ManagementEventWatcher();
            _instanceDeletionEventWatcher.EventArrived += new EventArrivedEventHandler(InstanceDeletionEventWatcher_EventArrived);
            _instanceDeletionEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_USBHub'");
            _instanceDeletionEventWatcher.Start();
        }

        private void VolumeChangeEventWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            string driveName = e.NewEvent.Properties["DriveName"].Value.ToString();
            if (!driveName.EndsWith(Path.DirectorySeparatorChar.ToString()))
                driveName += Path.DirectorySeparatorChar;

            string eventType = e.NewEvent.Properties["EventType"].Value.ToString();
            bool inserted = eventType == "2";
            bool removed = eventType == "3";

            if (inserted)
            {
                OnDriveInserted(driveName);
            }

            if (removed)
            {
                OnDriveRemoved(driveName);
            }
        }

        private void InstanceCreationEventWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject Win32_USBHub = (ManagementBaseObject)e.NewEvent["TargetInstance"];

            UsbDevice usbDevice = GetUsbDevice(Win32_USBHub);

            OnDeviceInserted(usbDevice);
        }

        private void InstanceDeletionEventWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject Win32_USBHub = (ManagementBaseObject)e.NewEvent["TargetInstance"];

            UsbDevice usbDevice = GetUsbDevice(Win32_USBHub);

            OnDeviceRemoved(usbDevice);
        }

        private static UsbDevice GetUsbDevice(ManagementBaseObject Win32_USBHub)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine(string.Empty);

            foreach (PropertyData property in Win32_USBHub.Properties)
            {
                System.Diagnostics.Debug.WriteLine(property.Name + " = " + property.Value);
            }

            System.Diagnostics.Debug.WriteLine(string.Empty);
#endif

            string deviceID = (string)Win32_USBHub.Properties["DeviceID"].Value;

            string[] info = deviceID.Split('\\');

            string[] id = info[1].Split('&');

            string vendorId = id[0].Substring(4, 4);
            string productId = id[1].Substring(4, 4);

            string serial = info[2];

            UsbDevice usbDevice = new UsbDevice
            {
                ProductID = productId,
                SerialNumber = serial,
                VendorID = vendorId
            };

            string ClassGuid = "{eec5ad98-8080-425f-922a-dabf3de3f69a}";

            using ManagementObjectSearcher Win32_PnPEntity = new ManagementObjectSearcher(
                $"SELECT Caption, Description, Manufacturer FROM Win32_PnPEntity WHERE ClassGuid = '{ClassGuid}' AND DeviceID LIKE '%{serial}%'");

            foreach (ManagementObject entity in Win32_PnPEntity.Get())
            {
                usbDevice.DeviceName = ((string)entity.Properties["Caption"].Value).Trim();
                usbDevice.Product = ((string)entity.Properties["Description"].Value).Trim();
                usbDevice.ProductDescription = ((string)entity.Properties["Description"].Value).Trim();
                usbDevice.Vendor = ((string)entity.Properties["Manufacturer"].Value).Trim();
                usbDevice.VendorDescription = ((string)entity.Properties["Manufacturer"].Value).Trim();
            }

            using ManagementObjectSearcher Win32_DiskDrive = new ManagementObjectSearcher(
                $"SELECT DeviceID FROM Win32_DiskDrive WHERE SerialNumber = '{serial}'");

            foreach (ManagementObject drive in Win32_DiskDrive.Get())
            {
                using ManagementObjectSearcher Win32_DiskDriveToDiskPartition = new ManagementObjectSearcher(
                    "ASSOCIATORS OF {Win32_DiskDrive.DeviceID='" + drive.Properties["DeviceID"].Value + "'} WHERE AssocClass = Win32_DiskDriveToDiskPartition");

                foreach (ManagementObject partition in Win32_DiskDriveToDiskPartition.Get())
                {
                    using ManagementObjectSearcher Win32_LogicalDiskToPartition = new ManagementObjectSearcher(
                        "ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + partition["DeviceID"] + "'} WHERE AssocClass = Win32_LogicalDiskToPartition");

                    foreach (ManagementObject disk in Win32_LogicalDiskToPartition.Get())
                    {
                        usbDevice.DevicePath = (string)disk["DeviceID"];
                    }
                }
            }

            return usbDevice;
        }

        /*
        ClassGuid = {71a27cdd-812a-11d0-bec7-08002be2092f}
        DeviceID = STORAGE\VOLUME\_??_USBSTOR#DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00#0019E06B9C85F9A0F7550C20&0#{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}
        PNPDeviceID = STORAGE\VOLUME\_??_USBSTOR#DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00#0019E06B9C85F9A0F7550C20&0#{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}

        ClassGuid = {eec5ad98-8080-425f-922a-dabf3de3f69a}
        Caption = IRM_CCSA_X64FRE_EN-US_DV5
        Description = DT 101 II
        Manufacturer = Kingston
        Name = IRM_CCSA_X64FRE_EN-US_DV5
        DeviceID = SWD\WPDBUSENUM\_??_USBSTOR#DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00#0019E06B9C85F9A0F7550C20&0#{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}
        PNPDeviceID = SWD\WPDBUSENUM\_??_USBSTOR#DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00#0019E06B9C85F9A0F7550C20&0#{53F56307-B6BF-11D0-94F2-00A0C91EFB8B}

        ClassGuid = {36fc9e60-c465-11cf-8056-444553540000}
        DeviceID = USB\VID_0951&PID_1625\0019E06B9C85F9A0F7550C20
        PNPDeviceID = USB\VID_0951&PID_1625\0019E06B9C85F9A0F7550C20

        ClassGuid = {4d36e967-e325-11ce-bfc1-08002be10318}
        Caption = Kingston DT 101 II USB Device
        Name = Kingston DT 101 II USB Device
        DeviceID = USBSTOR\DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00\0019E06B9C85F9A0F7550C20&0
        PNPDeviceID = USBSTOR\DISK&VEN_KINGSTON&PROD_DT_101_II&REV_1.00\0019E06B9C85F9A0F7550C20&0
        /**/

        public void Dispose()
        {
            _volumeChangeEventWatcher.Stop();
            _volumeChangeEventWatcher.Dispose();

            _instanceCreationEventWatcher.Stop();
            _instanceCreationEventWatcher.Dispose();

            _instanceDeletionEventWatcher.Stop();
            _instanceDeletionEventWatcher.Dispose();
        }

        #endregion
    }
}
