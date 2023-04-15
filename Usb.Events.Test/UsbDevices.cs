using System;
using System.Collections.Generic;
using System.Management;

/*
https://learn.microsoft.com/en-us/windows-hardware/drivers/install/guid-devinterface-usb-hub

ClassGuid for USB hubs: {36fc9e60-c465-11cf-8056-444553540000} The identifier is GUID_DEVINTERFACE_USB_HUB
ClassGuid for network adapters: {4d36e972-e325-11ce-bfc1-08002be10318} The identifier is GUID_DEVINTERFACE_NET
ClassGuid for HID devices: {745a17a0-74d3-11d0-b6fe-00a0c90f57da} The identifier is GUID_DEVINTERFACE_HID
ClassGuid for mice: {4d36e96f-e325-11ce-bfc1-08002be10318} The identifier is GUID_DEVINTERFACE_MOUSE
ClassGuid for keyboards: {4d36e96b-e325-11ce-bfc1-08002be10318} The identifier is GUID_DEVINTERFACE_KEYBOARD

https://learn.microsoft.com/en-us/windows-hardware/drivers/install/system-defined-device-setup-classes-available-to-vendors
/**/

class UsbDevices
{
    public static void ListUsbDevices()
    {
        IList<ManagementBaseObject> usbDevices = GetUsbDevices();

        foreach (ManagementBaseObject usbDevice in usbDevices)
        {
            string classGuid = (string)usbDevice["ClassGuid"];

            // you can exclude a device class, for example USB hubs:
            if (classGuid == "{36fc9e60-c465-11cf-8056-444553540000}")
                continue;

            Console.WriteLine("----- DEVICE -----");
            foreach (var property in usbDevice.Properties)
            {
                Console.WriteLine(string.Format("{0}: {1}", property.Name, property.Value));
            }

            string deviceID = (string)usbDevice["DeviceID"];

            if (deviceID.Contains("VID_") && deviceID.Contains("PID_"))
            {
                string[] splitDeviceID = deviceID.Split('\\');
                string[] splitVidPid = splitDeviceID[splitDeviceID.Length - 2].Split('&');

                string vid = splitVidPid[0].Split('_')[1];
                string pid = splitVidPid[1].Split('_')[1];
                string serialNumber = splitDeviceID[splitDeviceID.Length - 1];

                Console.WriteLine("VID: {0}", vid);
                Console.WriteLine("PID: {0}", pid);
                Console.WriteLine("Serial Number: {0}", serialNumber);
            }

            Console.WriteLine("------------------");
        }
    }

    public static IList<ManagementBaseObject> GetUsbDevices()
    {
        IList<string> usbDeviceAddresses = LookUpUsbDeviceAddresses();

        List<ManagementBaseObject> usbDevices = new List<ManagementBaseObject>();

        foreach (string usbDeviceAddress in usbDeviceAddresses)
        {
            // query MI for the PNP device info
            // address must be escaped to be used in the query; luckily, the form we extracted previously is already escaped
            ManagementObjectCollection curMoc = QueryMi("Select * from Win32_PnPEntity where PNPDeviceID = " + usbDeviceAddress);
            foreach (ManagementBaseObject device in curMoc)
            {
                usbDevices.Add(device);
            }
        }

        return usbDevices;
    }

    public static IList<string> LookUpUsbDeviceAddresses()
    {
        // this query gets the addressing information for connected USB devices
        ManagementObjectCollection usbDeviceAddressInfo = QueryMi(@"Select * from Win32_USBControllerDevice");

        List<string> usbDeviceAddresses = new List<string>();

        foreach (var device in usbDeviceAddressInfo)
        {
            string curPnpAddress = (string)device.GetPropertyValue("Dependent");
            // split out the address portion of the data; note that this includes escaped backslashes and quotes
            curPnpAddress = curPnpAddress.Split(new String[] { "DeviceID=" }, 2, StringSplitOptions.None)[1];

            usbDeviceAddresses.Add(curPnpAddress);
        }

        return usbDeviceAddresses;
    }

    // run a query against Windows Management Infrastructure (MI) and return the resulting collection
    public static ManagementObjectCollection QueryMi(string query)
    {
        ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(query);
        ManagementObjectCollection result = managementObjectSearcher.Get();

        managementObjectSearcher.Dispose();
        return result;
    }
}
