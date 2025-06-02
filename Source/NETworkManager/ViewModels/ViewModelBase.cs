using CommunityToolkit.Mvvm.Input;
using NETworkManager.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NETworkManager.ViewModels;

/// <summary>
/// Purpose: This class represents base ViewModel in the App. This approach helps to maintane the same UI logic for all Views.
/// Created By: amaizanov
/// Created On: 5/20/2025 11:59:19 AM
/// </summary>
public abstract class ViewModelBase : PropertyChangedBase
{
    private bool _firstLoad = true;
    private bool _closed;
    private bool _isRunning;
    private bool _isCompleted;
    private bool _isCanceling;
    private bool _isStatusMessageDisplayed;

    private string _inputEntry;
    private string _statusMessage;
    private string _status;

    protected CancellationTokenSource CancellationTokenSource = new();
    public string InputEntry
    {
        get => _inputEntry;
        set => SetField(ref _inputEntry, value);
    }
    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }
    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetField(ref _isRunning, value);
    }
    public bool IsCanceling
    {
        get => _isCanceling;
        set => SetField(ref _isCanceling, value);
    }
    public bool IsStatusMessageDisplayed
    {
        get => _isStatusMessageDisplayed;
        set => SetField(ref _isStatusMessageDisplayed, value);
    }
    public string StatusMessage
    {
        get => _statusMessage;
        protected set => SetField(ref _statusMessage, value);
    }
    public abstract IAsyncRelayCommand StartCommand { get; }
    public abstract IAsyncRelayCommand StopCommand { get; }

    public virtual Task Start(CancellationToken cancellationToken)
    {
        if (StartCommand.IsCancellationRequested)
        {
            CancellationTokenSource.Dispose();
            CancellationTokenSource = new CancellationTokenSource();
        }
        IsStatusMessageDisplayed = false;
        IsCanceling = cancellationToken.IsCancellationRequested;
        return Task.CompletedTask;
    }
    public virtual async Task Stop()
    {
        await CancellationTokenSource.CancelAsync();
        StartCommand.Cancel();
        IsCanceling = CancellationTokenSource.IsCancellationRequested;
    }
    public virtual async Task OnClose()
    {
        // Prevent multiple calls
        if (_closed)
            return;

        _closed = true;

        // Stop scan
        if (StartCommand.IsRunning)
            await StartCommand.ExecuteAsync(null);
    }
    public virtual async Task OnLoaded()
    {
        if (!_firstLoad)
            return;

        if (!string.IsNullOrEmpty(InputEntry))
            await StartCommand.ExecuteAsync(InputEntry);
        _firstLoad = false;
    }


    public IRelayCommand CopyDataToClipboardCommand => new RelayCommand<object>(CopyDataToClipboardAction);

    private static void CopyDataToClipboardAction(object data)
    {
        ClipboardHelper.SetClipboard(data.ToString());
    }

    protected void DisplayStatusMessage(string message)
    {
        if (!string.IsNullOrEmpty(StatusMessage))
            StatusMessage += Environment.NewLine;

        StatusMessage += message;
        IsStatusMessageDisplayed = true;
    }
}
