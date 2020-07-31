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

    public class UsbDevice
    {
        public string DeviceName { get; internal set; } = string.Empty;

        public string DeviceSystemPath { get; internal set; } = string.Empty;

        public string MountedDirectoryPath { get; internal set; } = string.Empty;

        public string Product { get; internal set; } = string.Empty;

        public string ProductDescription { get; internal set; } = string.Empty;

        public string ProductID { get; internal set; } = string.Empty;

        public string SerialNumber { get; internal set; } = string.Empty;

        public string Vendor { get; internal set; } = string.Empty;

        public string VendorDescription { get; internal set; } = string.Empty;

        public string VendorID { get; internal set; } = string.Empty;

        public bool IsMounted { get; internal set; }

        public bool IsEjected { get; internal set; }

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
