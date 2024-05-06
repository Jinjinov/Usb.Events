using System;
using System.Runtime.InteropServices;

namespace Usb.Events
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal struct UsbDeviceData
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string DeviceSystemPath;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string Product;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string ProductDescription;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string ProductID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string SerialNumber;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string Vendor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string VendorDescription;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string VendorID;
    }

    /// <summary>
    /// USB device
    /// </summary>
    /// <remarks>
    /// On removal events in Linux, only the device name and system path will be populated.
    /// </remarks>
    public class UsbDevice
    {
        /// <summary>
        /// Device name
        /// </summary>
        public string DeviceName { get; internal set; } = string.Empty;

        /// <summary>
        /// Device system path
        /// </summary>
        public string DeviceSystemPath { get; internal set; } = string.Empty;

        /// <summary>
        /// Device mounted directory path
        /// </summary>
        public string MountedDirectoryPath { get; internal set; } = string.Empty;

        /// <summary>
        /// Device product name
        /// </summary>
        public string Product { get; internal set; } = string.Empty;

        /// <summary>
        /// Device product description
        /// </summary>
        public string ProductDescription { get; internal set; } = string.Empty;

        /// <summary>
        /// Device product ID
        /// </summary>
        public string ProductID { get; internal set; } = string.Empty;

        /// <summary>
        /// Device serial number
        /// </summary>
        public string SerialNumber { get; internal set; } = string.Empty;

        /// <summary>
        /// Device vendor name
        /// </summary>
        public string Vendor { get; internal set; } = string.Empty;

        /// <summary>
        /// Device vendor description
        /// </summary>
        public string VendorDescription { get; internal set; } = string.Empty;

        /// <summary>
        /// Device vendor ID
        /// </summary>
        public string VendorID { get; internal set; } = string.Empty;

        /// <summary>
        /// Is device mounted
        /// </summary>
        public bool IsMounted { get; internal set; }

        /// <summary>
        /// Is device ejected
        /// </summary>
        public bool IsEjected { get; internal set; }

        /// <inheritdoc cref="UsbDevice"/>
        public UsbDevice()
        {
        }

        internal UsbDevice(UsbDeviceData usbDeviceData)
        {
            DeviceName = usbDeviceData.DeviceName;
            DeviceSystemPath = usbDeviceData.DeviceSystemPath;
            Product = usbDeviceData.Product;
            ProductDescription = usbDeviceData.ProductDescription;
            ProductID = usbDeviceData.ProductID;
            SerialNumber = usbDeviceData.SerialNumber;
            Vendor = usbDeviceData.Vendor;
            VendorDescription = usbDeviceData.VendorDescription;
            VendorID = usbDeviceData.VendorID;
        }

        /// <summary>
        /// Write all property values to a string
        /// </summary>
        /// <returns>Each property on a new line</returns>
        public override string ToString()
        {
            return "Device Name: " + DeviceName + Environment.NewLine +
                "Device System Path: " + DeviceSystemPath + Environment.NewLine +
                "Mounted Directory Path: " + MountedDirectoryPath + Environment.NewLine +
                "Product: " + Product + Environment.NewLine +
                "Product Description: " + ProductDescription + Environment.NewLine +
                "Product ID: " + ProductID + Environment.NewLine +
                "Serial Number: " + SerialNumber + Environment.NewLine +
                "Vendor: " + Vendor + Environment.NewLine +
                "Vendor Description: " + VendorDescription + Environment.NewLine +
                "Vendor ID: " + VendorID + Environment.NewLine;
        }
    }
}
