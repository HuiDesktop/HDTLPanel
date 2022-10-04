using System.ComponentModel;

namespace HDTLPanel
{
    internal interface IPanelControl : INotifyPropertyChanged
    {
    }

    internal interface ISaveableControl : IPanelControl
    {
        void Save(ManagedIpc.IpcWriter writer);
    }
}
