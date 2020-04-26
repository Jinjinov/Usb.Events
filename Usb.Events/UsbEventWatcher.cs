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
                //string message = Marshal.PtrToStringAuto(DelegateTest("To Linux", back => Console.WriteLine(back)));
                //Console.WriteLine(message);

                Task.Run(() => StartLinuxWatcher(InsertedCallback, RemovedCallback));
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        delegate void WatcherCallback(string driveName);

        private void InsertedCallback(string driveName)
        {
            UsbDriveInserted?.Invoke(this, driveName);

            UsbDrivePathList.Add(driveName);
        }

        private void RemovedCallback(string driveName)
        {
            UsbDriveRemoved?.Invoke(this, driveName);

            if (UsbDrivePathList.Contains(driveName))
            {
                UsbDrivePathList.Remove(driveName);
            }
        }

        #region Linux methods

        // https://github.com/PaulStoffregen/SerialDiscovery_JSON/blob/master/SerialDiscovery.c
        // https://github.com/MadLittleMods/node-usb-detection/blob/master/src/detection_linux.cpp
        // https://chromium.googlesource.com/chromiumos/platform/cros-disks/+/c32f05bf1b37716fdab4512f38248a139006473d/udev_device.cc

        //[DllImport("UsbEventWatcher.Linux.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        //static extern IntPtr DelegateTest(string message, WatcherCallback testCallback);

        [DllImport("UsbEventWatcher.Linux.so", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartLinuxWatcher(WatcherCallback insertedCallback, WatcherCallback removedCallback);

        #endregion

        #region Mac methods

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
            _instanceCreationEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            _instanceCreationEventWatcher.Start();

            _instanceDeletionEventWatcher = new ManagementEventWatcher();
            _instanceDeletionEventWatcher.EventArrived += new EventArrivedEventHandler(InstanceDeletionEventWatcher_EventArrived);
            _instanceDeletionEventWatcher.Query = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            _instanceDeletionEventWatcher.Start();
        }

        private void VolumeChangeEventWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            string driveName = e.NewEvent?.Properties["DriveName"]?.Value?.ToString() ?? throw new NullReferenceException(nameof(driveName) + " is null");
            if (!driveName.EndsWith(Path.DirectorySeparatorChar.ToString()))
                driveName += Path.DirectorySeparatorChar;

            string eventType = e.NewEvent?.Properties["EventType"]?.Value?.ToString() ?? throw new NullReferenceException(nameof(eventType) + " is null");
            bool inserted = eventType == "2";
            bool removed = eventType == "3";

            if (inserted)
            {
                InsertedCallback(driveName);
            }

            if (removed)
            {
                RemovedCallback(driveName);
            }
        }

        private void InstanceCreationEventWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

#if DEBUG
            foreach (PropertyData property in instance.Properties)
            {
                System.Diagnostics.Debug.WriteLine(property.Name + " = " + property.Value);
            }
#endif

            string caption = (string)instance.Properties["Caption"].Value;
            string description = (string)instance.Properties["Description"].Value;
            string name = (string)instance.Properties["Name"].Value;

            UsbDeviceInserted?.Invoke(this, new UsbDevice { Name = name });
        }

        private void InstanceDeletionEventWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];

#if DEBUG
            foreach (PropertyData property in instance.Properties)
            {
                System.Diagnostics.Debug.WriteLine(property.Name + " = " + property.Value);
            }
#endif

            string caption = (string)instance.Properties["Caption"].Value;
            string description = (string)instance.Properties["Description"].Value;
            string name = (string)instance.Properties["Name"].Value;

            UsbDeviceRemoved?.Invoke(this, new UsbDevice { Name = name });
        }

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
