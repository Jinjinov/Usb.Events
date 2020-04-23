using System;
using System.Collections.ObjectModel;

namespace Usb.Events
{
    public interface IUsbEventWatcher
    {
        ObservableCollection<string> RemovableDriveNameList { get; }

        event EventHandler<string>? DriveInserted;
        event EventHandler<string>? DriveRemoved;

        event EventHandler<string>? PnPEntityInstanceCreation;
        event EventHandler<string>? PnPEntityInstanceDeletion;
    }
}
