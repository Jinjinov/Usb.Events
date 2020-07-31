﻿using System;
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

        public UsbEventWatcher()
        {
            Start();
        }

        #region Methods

        private void Start()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) // Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                StartWindowsWatcher();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) // Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                Task.Run(() => StartMacWatcher(InsertedCallback, RemovedCallback));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) // Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Task.Run(() => StartLinuxWatcher(InsertedCallback, RemovedCallback, Message));

                Task.Run(async () => 
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        foreach (UsbDevice usbDevice in UsbDeviceList)
                        {
                            if (!string.IsNullOrEmpty(usbDevice.DeviceSystemPath))
                            {
                                GetMountPoint(usbDevice.DeviceSystemPath, mountPoint =>
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
                                });
                            }
                        }

                        await Task.Delay(100);
                    }
                }, _cancellationTokenSource.Token);
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
            UsbDeviceList.RemoveAll(device => device.DeviceSystemPath == usbDevice.DeviceSystemPath);
        }

        #endregion

        #region Linux and Mac methods

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        delegate void WatcherCallback(UsbDeviceData usbDevice);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        delegate void MessageCallback(string message);

        private void InsertedCallback(UsbDeviceData usbDevice)
        {
            //OnDriveInserted(usbDevice.DevicePath);
            OnDeviceInserted(new UsbDevice(usbDevice));
        }

        private void RemovedCallback(UsbDeviceData usbDevice)
        {
            //OnDriveRemoved(usbDevice.DevicePath);
            OnDeviceRemoved(new UsbDevice(usbDevice));
        }

        private readonly List<string> _messageList = new List<string>();
        private void Message(string message)
        {
            _messageList.Add(message);
        }

        [DllImport("UsbEventWatcher.Linux.so", CallingConvention = CallingConvention.Cdecl)]
        static extern void GetMountPoint(string syspath, MessageCallback message);

        [DllImport("UsbEventWatcher.Linux.so", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartLinuxWatcher(WatcherCallback insertedCallback, WatcherCallback removedCallback, MessageCallback messageCallback);

        [DllImport("UsbEventWatcher.Mac.dylib", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartMacWatcher(WatcherCallback insertedCallback, WatcherCallback removedCallback);

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

                //System.Diagnostics.Debug.WriteLine("attempts " + attempts);
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
