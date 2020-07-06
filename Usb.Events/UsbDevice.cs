using System;
using System.Runtime.InteropServices;

namespace Usb.Events
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct UsbDevice
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255)]
        public string DevicePath;

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

        public override string ToString()
        {
            return "Device Name: " + DeviceName + Environment.NewLine +
                "Device Path: " + DevicePath + Environment.NewLine +
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
