using System.Threading;
using System.Windows.Input;
using NETworkManager.Utilities;

namespace NETworkManager.ViewModels;

public abstract class ViewModelBase1 : PropertyChangedBase
{

    protected CancellationTokenSource CancellationTokenSource = new();
    public ICommand CopyDataToClipboardCommand => new RelayCommand(CopyDataToClipboardAction);

    private static void CopyDataToClipboardAction(object data)
    {
        ClipboardHelper.SetClipboard(data.ToString());
    }
}