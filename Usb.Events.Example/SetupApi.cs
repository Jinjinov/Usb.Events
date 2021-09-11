using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Usb.Events.Example
{
    public sealed class Device : IDisposable
    {
        private IntPtr _hDevInfo;
        private SP_DEVINFO_DATA _data;

        private Device(IntPtr hDevInfo, SP_DEVINFO_DATA data)
        {
            _hDevInfo = hDevInfo;
            _data = data;
        }

        public static Device Get(string pnpDeviceId)
        {
            if (pnpDeviceId == null)
                throw new ArgumentNullException("pnpDeviceId");

            IntPtr hDevInfo = SetupDiGetClassDevs(IntPtr.Zero, pnpDeviceId, IntPtr.Zero, DIGCF.DIGCF_ALLCLASSES | DIGCF.DIGCF_DEVICEINTERFACE);
            if (hDevInfo == (IntPtr)INVALID_HANDLE_VALUE)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            SP_DEVINFO_DATA data = new SP_DEVINFO_DATA();
            data.cbSize = Marshal.SizeOf(data);
            if (!SetupDiEnumDeviceInfo(hDevInfo, 0, ref data))
            {
                int err = Marshal.GetLastWin32Error();
                if (err == ERROR_NO_MORE_ITEMS)
                    return null;

                throw new Win32Exception(err);
            }

            return new Device(hDevInfo, data) { PnpDeviceId = pnpDeviceId };
        }

        public void Dispose()
        {
            if (_hDevInfo != IntPtr.Zero)
            {
                SetupDiDestroyDeviceInfoList(_hDevInfo);
                _hDevInfo = IntPtr.Zero;
            }
        }

        public string PnpDeviceId { get; private set; } = string.Empty;

        public string ParentPnpDeviceId
        {
            get
            {
                if (IsVistaOrHiger)
                    return GetStringProperty(DEVPROPKEY.DEVPKEY_Device_Parent);

                int cr = CM_Get_Parent(out uint parent, _data.DevInst, 0);
                if (cr != 0)
                    throw new Exception("CM Error:" + cr);

                return GetDeviceId(parent);
            }
        }

        private static string GetDeviceId(uint inst)
        {
            IntPtr buffer = Marshal.AllocHGlobal(MAX_DEVICE_ID_LEN + 1);
            int cr = CM_Get_Device_ID(inst, buffer, MAX_DEVICE_ID_LEN + 1, 0);
            if (cr != 0)
                throw new Exception("CM Error:" + cr);

            try
            {
                return Marshal.PtrToStringAnsi(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public string[] ChildrenPnpDeviceIds
        {
            get
            {
                if (IsVistaOrHiger)
                    return GetStringListProperty(DEVPROPKEY.DEVPKEY_Device_Children);

                int cr = CM_Get_Child(out uint child, _data.DevInst, 0);
                if (cr != 0)
                    return new string[0];

                List<string> ids = new List<string> { GetDeviceId(child) };

                do
                {
                    cr = CM_Get_Sibling(out child, child, 0);
                    if (cr != 0)
                        return ids.ToArray();

                    ids.Add(GetDeviceId(child));
                }
                while (true);
            }
        }

        private static bool IsVistaOrHiger
        {
            get
            {
                return (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.CompareTo(new Version(6, 0)) >= 0);
            }
        }

        private const int INVALID_HANDLE_VALUE = -1;
        private const int ERROR_NO_MORE_ITEMS = 259;
        private const int MAX_DEVICE_ID_LEN = 200;

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [Flags]
        private enum DIGCF : uint
        {
            DIGCF_DEFAULT = 0x00000001,
            DIGCF_PRESENT = 0x00000002,
            DIGCF_ALLCLASSES = 0x00000004,
            DIGCF_PROFILE = 0x00000008,
            DIGCF_DEVICEINTERFACE = 0x00000010,
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid, string Enumerator, IntPtr hwndParent, DIGCF Flags);

        [DllImport("setupapi.dll")]
        private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("setupapi.dll")]
        private static extern int CM_Get_Device_ID(uint dnDevInst, IntPtr Buffer, int BufferLen, uint ulFlags);

        [DllImport("setupapi.dll")]
        private static extern int CM_Get_Child(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("setupapi.dll")]
        private static extern int CM_Get_Sibling(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("setupapi.dll")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        // vista and higher
        [DllImport("setupapi.dll", SetLastError = true, EntryPoint = "SetupDiGetDevicePropertyW")]
        private static extern bool SetupDiGetDeviceProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, ref DEVPROPKEY propertyKey, out int propertyType, IntPtr propertyBuffer, int propertyBufferSize, out int requiredSize, int flags);

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;

            // from devpkey.h
            public static readonly DEVPROPKEY DEVPKEY_Device_Parent = new DEVPROPKEY { fmtid = new Guid("{4340A6C5-93FA-4706-972C-7B648008A5A7}"), pid = 8 };
            public static readonly DEVPROPKEY DEVPKEY_Device_Children = new DEVPROPKEY { fmtid = new Guid("{4340A6C5-93FA-4706-972C-7B648008A5A7}"), pid = 9 };
        }

        public static readonly DEVPROPKEY DEVPKEY_Device_DeviceDesc = new DEVPROPKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 2 };
        public static readonly DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc = new DEVPROPKEY { fmtid = new Guid(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2), pid = 4 };
        public static readonly DEVPROPKEY DEVPKEY_Device_FriendlyName = new DEVPROPKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };

        private string[] GetStringListProperty(DEVPROPKEY key)
        {
            SetupDiGetDeviceProperty(_hDevInfo, ref _data, ref key, out int type, IntPtr.Zero, 0, out int size, 0);
            if (size == 0)
                return new string[0];

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!SetupDiGetDeviceProperty(_hDevInfo, ref _data, ref key, out type, buffer, size, out size, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                List<string> strings = new List<string>();
                IntPtr current = buffer;
                do
                {
                    string? s = Marshal.PtrToStringUni(current);
                    if (string.IsNullOrEmpty(s))
                        break;

                    strings.Add(s);
                    current += (1 + s.Length) * 2;
                }
                while (true);

                return strings.ToArray();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public string GetStringProperty(DEVPROPKEY key)
        {
            SetupDiGetDeviceProperty(_hDevInfo, ref _data, ref key, out int type, IntPtr.Zero, 0, out int size, 0);
            if (size == 0)
                return null;

            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!SetupDiGetDeviceProperty(_hDevInfo, ref _data, ref key, out type, buffer, size, out size, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                return Marshal.PtrToStringUni(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}