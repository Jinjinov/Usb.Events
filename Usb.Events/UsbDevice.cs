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
            return "DeviceName: " + DeviceName +
                ", DevicePath: " + DevicePath +
                ", Product: " + Product +
                ", ProductDescription: " + ProductDescription +
                ", ProductID: " + ProductID +
                ", SerialNumber: " + SerialNumber +
                ", Vendor: " + Vendor +
                ", VendorDescription: " + VendorDescription +
                ", VendorID: " + VendorID;
        }
    }
}
