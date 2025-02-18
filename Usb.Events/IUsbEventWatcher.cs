using System;
using System.Collections.Generic;

namespace Usb.Events
{
    /// <summary>
    /// Main Usb.Events interface
    /// </summary>
    public interface IUsbEventWatcher : IDisposable
    {
        /// <summary>
        /// List of USB drive paths
        /// </summary>
        List<string> UsbDrivePathList { get; }

        /// <summary>
        /// List of USB devices
        /// </summary>
        List<UsbDevice> UsbDeviceList { get; }

        /// <summary>
        /// USB drive mounted event
        /// </summary>
        event EventHandler<string>? UsbDriveMounted;

        /// <summary>
        /// USB drive ejected event
        /// </summary>
        event EventHandler<string>? UsbDriveEjected;

        /// <summary>
        /// USB device added event
        /// </summary>
        event EventHandler<UsbDevice>? UsbDeviceAdded;

        /// <summary>
        /// USB device removed event
        /// </summary>
        event EventHandler<UsbDevice>? UsbDeviceRemoved;

        /// <summary>
        /// Start monitoring USB events
        /// </summary>
        /// <param name="addAlreadyPresentDevicesToList">Set addAlreadyPresentDevicesToList to true to include already present devices in UsbDeviceList</param>
        /// <param name="usePnPEntity">Set usePnPEntity to true to query Win32_PnPEntity instead of Win32_USBControllerDevice in Windows</param>
        /// <param name="includeTTY">Set includeTTY to true to monitor the TTY subsystem in Linux (besides the USB subsystem)</param>
        /// <param name="isUseMountPoint">Activate/Deactivate mount point with <see cref="UsbDriveMounted"/> and <see cref="UsbDriveEjected"/></param>
        void Start(bool addAlreadyPresentDevicesToList = false, bool usePnPEntity = false, bool includeTTY = false, bool isUseMountPoint = true);
    }
}
