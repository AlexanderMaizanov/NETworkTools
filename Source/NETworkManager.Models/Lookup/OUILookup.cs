using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace NETworkManager.Models.Lookup;

/// <summary>
///     Class for looking up OUI information.
/// </summary>
public static partial class OUILookup
{
    #region Constructor

    /// <summary>
    ///     Loads the OUI XML file and creates the lookup.
    /// </summary>
    static OUILookup()
    {
        OUIInfoList = [];

        var document = new XmlDocument();
        document.Load(OuiFilePath);

        foreach (XmlNode node in document.SelectNodes("/OUIs/OUI")!)
            if (node != null)
                OUIInfoList.Add(new OUIInfo(node.SelectSingleNode("MACAddress")?.InnerText,
                    node.SelectSingleNode("Vendor")?.InnerText));

        OUIInfoLookup = (Lookup<string, OUIInfo>)OUIInfoList.ToLookup(x => x.MACAddress);
    }

    #endregion

    #region Variables


    /// <summary>
    ///     
    /// </summary>
    public static bool ExternalLookup { get; set; }

    /// <summary>
    ///     Path to the xml file with the oui information's located in the resources folder.
    /// </summary>
    private static readonly string OuiFilePath =
        Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)!, "Resources", "OUI.xml");

    /// <summary>
    ///     List of <see cref="OUIInfo" /> with OUI information.
    /// </summary>
    private static readonly List<OUIInfo> OUIInfoList;

    /// <summary>
    ///     Lookup of <see cref="OUIInfo" /> with OUI information. Key is the MAC address.
    /// </summary>
    private static Lookup<string, OUIInfo> OUIInfoLookup;

    /// <summary>
    ///     
    /// 
    /// </summary>
    private static readonly HttpClient _httpClientMCL = new() 
    {
        BaseAddress = new Uri("https://www.macvendorlookup.com/api/v2/")
    };
    #endregion

    #region Methods

    /// <summary>
    ///     Get the <see cref="OUIInfo" /> for the given MAC address async.
    /// </summary>
    /// <param name="macAddress">MAC address to get the OUI information's for.</param>
    /// <returns>List of <see cref="OUIInfo" />. Empty if nothing was found.</returns>
    public static Task<List<OUIInfo>> LookupByMacAddressAsync(string macAddress, CancellationToken cancellationToken)
    {
        var lookupTask = Task.FromResult(LookupByMacAddress(macAddress));

        if(ExternalLookup && !cancellationToken.IsCancellationRequested)
        {
            lookupTask = LookupMacVendorApiAsync(macAddress, cancellationToken);
        }

        return lookupTask;
    }

    private static async Task<List<OUIInfo>> LookupMacVendorApiAsync(string macAddress, CancellationToken cancellationToken)
    {
        var result = new List<OUIInfo>();
        string ouiKey = PrepareMacAddress(macAddress);
        result = OUIInfoLookup[ouiKey].ToList();
        if (!OUIInfoLookup.Contains(ouiKey))
        {
            var response = await _httpClientMCL.GetStringAsync(macAddress, cancellationToken);
            result = JsonSerializer.Deserialize<List<OUIInfo>>(response);
            result.ForEach(i => i.MACAddress = ouiKey);
            //var list = OUIInfoLookup.ToList();
             //   list.Add(new [] { ouiKey, result });
        }

        return result;
    }

    /// <summary>
    ///     Get the <see cref="OUIInfo" /> for the given MAC address.
    /// </summary>
    /// <param name="macAddress">MAC address to get the OUI information's for.</param>
    /// <returns>List of <see cref="OUIInfo" />. Empty if nothing was found.</returns>
    public static List<OUIInfo> LookupByMacAddress(string macAddress)
    {
        string ouiKey = PrepareMacAddress(macAddress);
        return OUIInfoLookup[ouiKey].ToList();
    }

    private static string PrepareMacAddress(string macAddress)
    {
        return PreMacRegex().Replace(macAddress, "")[..6].ToUpper();
    }

    /// <summary>
    ///     Search <see cref="OUIInfo" /> by the given vendor async.
    /// </summary>
    /// <param name="vendor">Vendor to look up.</param>
    /// <returns><see cref="OUIInfo" /> or null if not found.</returns>
    public static Task<List<OUIInfo>> SearchByVendorAsync(string vendor)
    {
        return Task.Run(() => SearchByVendor(vendor));
    }

    /// <summary>
    ///     Search <see cref="OUIInfo" /> by the given vendor.
    /// </summary>
    /// <param name="vendor">Vendor to look up.</param>
    /// <returns><see cref="OUIInfo" /> or null if not found.</returns>
    public static List<OUIInfo> SearchByVendor(string vendor)
    {
        return (from info in OUIInfoList
                where info.Vendor.IndexOf(vendor, StringComparison.OrdinalIgnoreCase) > -1
                select info
            ).ToList();
    }

    /// <summary>
    ///     Search <see cref="OUIInfo" /> by the given vendors async.
    /// </summary>
    /// <param name="vendors">Vendors to look up.</param>
    /// <returns>List of <see cref="OUIInfo" />. Empty if nothing was found.</returns>
    public static Task<List<OUIInfo>> SearchByVendorsAsync(IReadOnlyCollection<string> vendors)
    {
        return Task.Run(() => SearchByVendors(vendors));
    }

    /// <summary>
    ///     Search <see cref="OUIInfo" /> by the given vendors.
    /// </summary>
    /// <param name="vendors">Vendors to look up.</param>
    /// <returns>List of <see cref="OUIInfo" />. Empty if nothing was found.</returns>
    public static List<OUIInfo> SearchByVendors(IReadOnlyCollection<string> vendors)
    {
        return (from info in OUIInfoList
                from vendor in vendors
                where info.Vendor.IndexOf(vendor, StringComparison.OrdinalIgnoreCase) > -1
                select info
            ).ToList();
    }

    [GeneratedRegex("[-|:|.]")]
    private static partial Regex PreMacRegex();

    #endregion
}