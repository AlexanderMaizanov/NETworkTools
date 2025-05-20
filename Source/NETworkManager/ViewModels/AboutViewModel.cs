using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

using CommonUtilities.Net;
using CommunityToolkit.Mvvm.Input;
using NETworkManager.Documentation;
using NETworkManager.Localization.Resources;
using NETworkManager.Models.HyperV;
using NETworkManager.Models.Netbox;
using NETworkManager.Properties;
using NETworkManager.Settings;
using NETworkManager.Update;
using NETworkManager.Utilities;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace NETworkManager.ViewModels;

public class AboutViewModel : ViewModelBase1
{
    #region Constructor

    public AboutViewModel()
    {
        LibrariesView = CollectionViewSource.GetDefaultView(LibraryManager.List);
        LibrariesView.SortDescriptions.Add(new SortDescription(nameof(LibraryInfo.Name), ListSortDirection.Ascending));

        ExternalServicesView = CollectionViewSource.GetDefaultView(ExternalServicesManager.List);
        ExternalServicesView.SortDescriptions.Add(new SortDescription(nameof(ExternalServicesInfo.Name),
            ListSortDirection.Ascending));

        ResourcesView = CollectionViewSource.GetDefaultView(ResourceManager.List);
        ResourcesView.SortDescriptions.Add(new SortDescription(nameof(ResourceInfo.Name), ListSortDirection.Ascending));
        
        _jsonClientNetbox.DefaultRequestHeaders.Add("Authorization", "Token 3659827ac0448b018467dc608d60d14f8eec27c5");
        _jsonClientNetbox.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
    }

    #endregion

    #region Methods

    private void CheckForUpdates()
    {
        IsUpdateAvailable = false;
        ShowUpdaterMessage = false;

        IsUpdateCheckRunning = true;

        var updater = new Updater();

        updater.UpdateAvailable += Updater_UpdateAvailable;
        updater.NoUpdateAvailable += Updater_NoUpdateAvailable;
        updater.Error += Updater_Error;

        updater.CheckOnGitHub(Resources.NETworkManager_GitHub_User, Resources.NETworkManager_GitHub_Repo,
            AssemblyManager.Current.Version, SettingsManager.Current.Update_CheckForPreReleases);
    }

    #endregion

    #region Variables

    public static string Version => $"{Strings.Version} {AssemblyManager.Current.Version}";

    public static string DevelopedByText =>
        string.Format(Strings.DevelopedAndMaintainedByX + " ", Resources.NETworkManager_GitHub_User);

    private bool _isUpdateCheckRunning;

    public bool IsUpdateCheckRunning
    {
        get => _isUpdateCheckRunning;
        set
        {
            if (value == _isUpdateCheckRunning)
                return;

            _isUpdateCheckRunning = value;
            OnPropertyChanged();
        }
    }

    private bool _isUpdateNetboxRunning;
    public bool IsUpdateNetboxRunning
    {
        get => _isUpdateNetboxRunning;
        set
        {
            if (value == _isUpdateNetboxRunning)
                return;

            _isUpdateNetboxRunning = value;
            OnPropertyChanged();
        }
    }


    private bool _isUpdateAvailable;

    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set
        {
            if (value == _isUpdateAvailable)
                return;

            _isUpdateAvailable = value;
            OnPropertyChanged();
        }
    }

    private string _updateText;

    public string UpdateText
    {
        get => _updateText;
        private set
        {
            if (value == _updateText)
                return;

            _updateText = value;
            OnPropertyChanged();
        }
    }

    private string _updateReleaseUrl;

    public string UpdateReleaseUrl
    {
        get => _updateReleaseUrl;
        private set
        {
            if (value == _updateReleaseUrl)
                return;

            _updateReleaseUrl = value;
            OnPropertyChanged();
        }
    }

    private bool _showUpdaterMessage;

    public bool ShowUpdaterMessage
    {
        get => _showUpdaterMessage;
        set
        {
            if (value == _showUpdaterMessage)
                return;

            _showUpdaterMessage = value;
            OnPropertyChanged();
        }
    }

    private string _updaterMessage;

    public string UpdaterMessage
    {
        get => _updaterMessage;
        private set
        {
            if (value == _updaterMessage)
                return;

            _updaterMessage = value;
            OnPropertyChanged();
        }
    }

    public ICollectionView LibrariesView { get; }

    private LibraryInfo _selectedLibraryInfo;

    public LibraryInfo SelectedLibraryInfo
    {
        get => _selectedLibraryInfo;
        set
        {
            if (value == _selectedLibraryInfo)
                return;

            _selectedLibraryInfo = value;
            OnPropertyChanged();
        }
    }

    public ICollectionView ExternalServicesView { get; }

    private ExternalServicesInfo _selectedExternalServicesInfo;

    public ExternalServicesInfo SelectedExternalServicesInfo
    {
        get => _selectedExternalServicesInfo;
        set
        {
            if (value == _selectedExternalServicesInfo)
                return;

            _selectedExternalServicesInfo = value;
            OnPropertyChanged();
        }
    }

    public ICollectionView ResourcesView { get; }

    private ResourceInfo _selectedResourceInfo;

    public ResourceInfo SelectedResourceInfo
    {
        get => _selectedResourceInfo;
        set
        {
            if (value == _selectedResourceInfo)
                return;

            _selectedResourceInfo = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Commands & Actions
    private static readonly JsonClient _jsonClientNetbox = new()
    {
        BaseAddress = new Uri("http://scfnetb01:8000/api/"),
        
    };    
    public IAsyncRelayCommand UpdateNetboxCommand => new AsyncRelayCommand(UpdateNetboxCommandAction);
    private async Task UpdateNetboxCommandAction(CancellationToken cancellationToken)
    {
        ShowUpdaterMessage = false;
        var prefixes = await _jsonClientNetbox.GetAsync<NetboxJsonResult<Prefix>>("ipam/prefixes/", cancellationToken: cancellationToken);
        var response = await _jsonClientNetbox.GetAsync<NetboxJsonResult<Cluster>>("virtualization/clusters/", cancellationToken: cancellationToken);
        var clusters = response.Results;
        var scf = clusters[0];

        var hvClient = new HyperVClient(scf.Name);
        //var clust = hvClient.GetCluster(scf.Name);
        string clusterName = scf.Name; // cluster alias
        //string custerGroupResource = "FS_Resource1"; // Cluster group name
        ConnectionOptions options = new() { 
        ////    Username = "ClusterAdmin", //could be in domain\user format
        ////    Password = "HisPassword"
        };
        //::ExecQuery - root\mscluster : select * from mscluster_cluster
        //// Connect with the mscluster WMI namespace on the cluster named "MyCluster"
        //var s = new ManagementScope("\\\\" + clusterName + "\\root\\mscluster", options);
        //var p = new ManagementPath("Mscluster_Clustergroup.Name='" + custerGroupResource + "'");
        //p.
        var machines = new ConcurrentBag<VirtualMachine>();
        var servers = new List<string>();
        var clustHv2Scope = new ManagementScope($@"\\{scf.Name}\root\HyperVCluster\v2");
        try
        {

            var members = HyperVClient.WmiQuery("select * from Msvm_MemberOfCollection", clustHv2Scope);
            foreach (ManagementObject member in members)
            {
                var srv = member.GetPropertyValue("Member");
                servers.Add(srv.ToString());
            }
            var wmi_result = HyperVClient.WmiQuery("select * from CIM_View", clustHv2Scope);
            var taskList = new List<Task>();
            foreach (var wmi in wmi_result)
            {
                taskList.Add(Task.Run(() =>
                {
                    var vmID = wmi.GetPropertyValue("Name").ToString();
                    var vmOwnerHost = wmi.GetPropertyValue("HostComputerSystemName").ToString();
                    var cim2Scope = new ManagementScope($@"\\{vmOwnerHost}\ROOT\StandardCimv2");
                    var vir2Scope = new ManagementScope($@"\\{vmOwnerHost}\ROOT\virtualization\v2");

                    var vmMemory = HyperVClient.WmiQuery($"select * from msvm_memory where ElementName=\"Memory\" and SystemName=\"{vmID}\"", vir2Scope).ToList().FirstOrDefault()?.GetPropertyValue("NumberOfBlocks");
                    var vmProc = HyperVClient.WmiQuery($"select * from Msvm_Processor WHERE elementname=\"Processor\" and SystemName=\"{vmID}\"", vir2Scope).ToList();
                    var etherPort = HyperVClient.WmiQuery($"select * from Msvm_SyntheticEthernetPort where elementname=\"Network Adapter\" and SystemName=\"{vmID}\"", vir2Scope).ToList().FirstOrDefault()?.GetPropertyValue("Description").ToString();
                    var vSwitchName = HyperVClient.WmiQuery($"select * from Msvm_EthernetPortAllocationSettingData where InstanceID like \"Microsoft:{vmID}%\"", vir2Scope).ToList().FirstOrDefault()?.GetPropertyValue("LastKnownSwitchName").ToString();
                    var vmState = Convert.ToInt32(HyperVClient.WmiQuery($"select * from CIM_ComputerSystem where Name=\'{vmID}\'", vir2Scope).Single()?.GetPropertyValue("EnabledState"));
                    
                    //WmiUtilities.GetVirtualMachine()
                    var vm = new VirtualMachine()
                    {
                        Name = wmi.GetPropertyValue("ElementName").ToString(),
                        MemorySizeBytes = Convert.ToInt64(vmMemory),
                        ProcessorCount = vmProc.Count,
                        NetAdapterName = etherPort,
                        SwitchName = vSwitchName,
                        State = vmState switch
                                {
                                    2 => VirtualMachineState.Running,
                                    3 => VirtualMachineState.Off,
                                    9 => VirtualMachineState.Paused,
                                    8 => VirtualMachineState.Saved,
                                    _ => VirtualMachineState.Unknown
                                }
                    };
                    machines.Add(vm);
                }, cancellationToken));                
            }
            await Task.WhenAll(taskList).ConfigureAwait(false);
            UpdaterMessage = string.Join(Environment.NewLine, machines.Where(m => m.State == VirtualMachineState.Running).Select(machine => machine.Name).ToArray()); //
            ShowUpdaterMessage = true;
            //hvClient.ListVms();
        }
        catch (Exception e)
        {
            UpdaterMessage = e.Message;
            ShowUpdaterMessage = true;
        }
        
        
    }

    public IRelayCommand CheckForUpdatesCommand => new RelayCommand(CheckForUpdates);

    public IRelayCommand OpenWebsiteCommand => new RelayCommand<object>(OpenWebsiteAction);

    private static void OpenWebsiteAction(object url)
    {
        ExternalProcessStarter.OpenUrl((string)url);
    }

    public IRelayCommand OpenDocumentationCommand
    {
        get { return new RelayCommand(OpenDocumentationAction); }
    }

    private void OpenDocumentationAction()
    {
        DocumentationManager.OpenDocumentation(DocumentationIdentifier.Default);
    }

    public IRelayCommand OpenLicenseFolderCommand => new RelayCommand(OpenLicenseFolderAction);

    private void OpenLicenseFolderAction()
    {
        Process.Start("explorer.exe", LibraryManager.GetLicenseLocation());
    }

    #endregion

    #region Events

    private void Updater_UpdateAvailable(object sender, UpdateAvailableArgs e)
    {
        UpdateText = string.Format(Strings.VersionxxIsAvailable, e.Release.TagName);
        UpdateReleaseUrl = e.Release.Prerelease ? e.Release.HtmlUrl : Resources.NETworkManager_LatestReleaseUrl;

        IsUpdateCheckRunning = false;
        IsUpdateAvailable = true;
    }

    private void Updater_NoUpdateAvailable(object sender, EventArgs e)
    {
        UpdaterMessage = Strings.NoUpdateAvailable;

        IsUpdateCheckRunning = false;
        ShowUpdaterMessage = true;
    }

    private void Updater_Error(object sender, EventArgs e)
    {
        UpdaterMessage = Strings.ErrorCheckingApiGithubComVerifyYourNetworkConnection;

        IsUpdateCheckRunning = false;
        ShowUpdaterMessage = true;
    }

    #endregion
}