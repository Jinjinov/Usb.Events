using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace Usb.Events
{
    public class UsbEventWatcher : IUsbEventWatcher, IDisposable
    {
        #region IUsbEventWatcher

        public ObservableCollection<string> RemovableDriveNameList { get; private set; } = new ObservableCollection<string>();

        public event EventHandler<string>? DriveInserted;
        public event EventHandler<string>? DriveRemoved;

        public event EventHandler<string>? PnPEntityInstanceCreation;
        public event EventHandler<string>? PnPEntityInstanceDeletion;

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
                StartMacWatcher();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || Environment.OSVersion.Platform == PlatformID.Unix)
            {
                StartLinuxWatcher();
            }
        }

        #region Mac methods

        [DllImport("UsbEventWatcher.Mac.dylib", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartMacWatcher();

        #endregion

        #region Linux methods

        [DllImport("UsbEventWatcher.Linux.so", CallingConvention = CallingConvention.Cdecl)]
        static extern void StartLinuxWatcher();

        #endregion

        #region Windows methods

        private void StartWindowsWatcher()
        {
            RemovableDriveNameList = new ObservableCollection<string>(DriveInfo.GetDrives()
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
            string DriveName = e.NewEvent?.Properties["DriveName"]?.Value?.ToString() ?? throw new NullReferenceException(nameof(DriveName) + " is null");
            if (!DriveName.EndsWith(Path.DirectorySeparatorChar.ToString()))
                DriveName += Path.DirectorySeparatorChar;

            string EventType = e.NewEvent?.Properties["EventType"]?.Value?.ToString() ?? throw new NullReferenceException(nameof(EventType) + " is null");
            bool inserted = EventType == "2";
            bool removed = EventType == "3";

            if (inserted)
            {
                DriveInserted?.Invoke(this, DriveName);

                RemovableDriveNameList.Add(DriveName);
            }

            if (removed)
            {
                DriveRemoved?.Invoke(this, DriveName);

                RemovableDriveNameList.Remove(DriveName);
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

            PnPEntityInstanceCreation?.Invoke(this, name);
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

            PnPEntityInstanceDeletion?.Invoke(this, name);
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
