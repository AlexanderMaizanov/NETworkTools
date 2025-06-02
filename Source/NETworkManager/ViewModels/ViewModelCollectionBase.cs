using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.Controls.Dialogs;
using NETworkManager.Localization.Resources;
using NETworkManager.Models.Export;
using NETworkManager.Settings;
using NETworkManager.Views;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NETworkManager.ViewModels;

/// <summary>
/// Purpose: This classs represents base ViewModel object with Collection containing results os a certain type.
/// Created By: amaizanov
/// Created On: 5/20/2025 9:26:33 AM
/// </summary>
public abstract class ViewModelCollectionBase<T> : ViewModelBase where T : class
{
    private T _selectedResult;
    private IList _selectedResults = new ArrayList();
    private ObservableCollection<T> _results = [];
    public ICollectionView HostHistoryView { get; protected set; }
    public ICollectionView ResultsView { get; protected set; }
    public IList SelectedResults
    {
        get => _selectedResults;
        set => SetField(ref _selectedResults, value);
    }

    public T SelectedResult
    {
        get => _selectedResult;
        set => SetField(ref _selectedResult, value);
    }

    public ObservableCollection<T> Results 
    { 
        get => _results;
        protected set => SetField(ref _results, value);
    }

    public virtual IAsyncRelayCommand ExportCommand => new AsyncRelayCommand(Export);

    protected abstract Task Export();

    protected virtual Task Export(IDialogCoordinator dialogCoordinator)
    {
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive);

        var customDialog = new CustomDialog
        {
            Title = Strings.Export
        };
        var exportViewModel = new ExportViewModel(async instance =>
        {
            await dialogCoordinator.HideMetroDialogAsync(window, customDialog);

            try
            {
                var collection = instance.ExportAll
                        ? Results
                        : new ObservableCollection<T>(SelectedResults.Cast<T>());
                //ExportManager.Export(instance.FilePath, instance.FileType, collection);
            }
            catch (Exception ex)
            {
                var settings = AppearanceManager.MetroDialog;
                settings.AffirmativeButtonText = Strings.OK;

                await dialogCoordinator.ShowMessageAsync(window, Strings.Error,
                    Strings.AnErrorOccurredWhileExportingTheData + Environment.NewLine +
                    Environment.NewLine + ex.Message, MessageDialogStyle.Affirmative, settings);
            }

            SettingsManager.Current.IPScanner_ExportFileType = instance.FileType;
            SettingsManager.Current.IPScanner_ExportFilePath = instance.FilePath;
        }, _ => { dialogCoordinator.HideMetroDialogAsync(window, customDialog); }, [
            ExportFileType.Csv, ExportFileType.Xml, ExportFileType.Json
        ], true, SettingsManager.Current.IPScanner_ExportFileType, SettingsManager.Current.IPScanner_ExportFilePath);

        customDialog.Content = new ExportDialog
        {
            DataContext = exportViewModel
        };

        return dialogCoordinator.ShowMetroDialogAsync(window, customDialog);
    }
}
