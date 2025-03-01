﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using NETworkManager.Models.Network;

namespace NETworkManager.Models.Export;

public static partial class ExportManager
{
    /// <summary>
    ///     Method to export objects from type <see cref="WiFiNetworkInfo" /> to a file.
    /// </summary>
    /// <param name="filePath">Path to the export file.</param>
    /// <param name="fileType">Allowed <see cref="ExportFileType" /> are CSV, XML or JSON.</param>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{T}" /> to export.</param>
    public static void Export(string filePath, ExportFileType fileType, IReadOnlyList<WiFiNetworkInfo> collection)
    {
        switch (fileType)
        {
            case ExportFileType.Csv:
                CreateCsv(collection, filePath);
                break;
            case ExportFileType.Xml:
                CreateXml(collection, filePath);
                break;
            case ExportFileType.Json:
                CreateJson(collection, filePath);
                break;
            case ExportFileType.Txt:
            default:
                throw new ArgumentOutOfRangeException(nameof(fileType), fileType, null);
        }
    }

    /// <summary>
    ///     Creates a CSV file from the given <see cref="WiFiNetworkInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{WiFiNetworkInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateCsv(IEnumerable<WiFiNetworkInfo> collection, string filePath)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendJoin(",",
            nameof(WiFiNetworkInfo.AvailableNetwork.Bssid), nameof(WiFiNetworkInfo.AvailableNetwork.Ssid), nameof(WiFiNetworkInfo.AvailableNetwork.ChannelCenterFrequencyInKilohertz), nameof(WiFiNetworkInfo.AvailableNetwork.SignalBars),
            nameof(WiFiNetworkInfo.AvailableNetwork.IsWiFiDirect), nameof(WiFiNetworkInfo.AvailableNetwork.NetworkRssiInDecibelMilliwatts), nameof(WiFiNetworkInfo.AvailableNetwork.PhyKind), nameof(WiFiNetworkInfo.AvailableNetwork.NetworkKind),
            nameof(WiFiNetworkInfo.AvailableNetwork.SecuritySettings.NetworkAuthenticationType), nameof(WiFiNetworkInfo.AvailableNetwork.SecuritySettings.NetworkEncryptionType), nameof(WiFiNetworkInfo.AvailableNetwork.BeaconInterval), nameof(WiFiNetworkInfo.AvailableNetwork.Uptime)
        ).AppendLine();

        foreach (var info in collection)
            stringBuilder.AppendJoin(",",
                info.AvailableNetwork.Bssid, info.AvailableNetwork.Ssid, info.AvailableNetwork.ChannelCenterFrequencyInKilohertz, info.AvailableNetwork.SignalBars,
                info.AvailableNetwork.IsWiFiDirect, info.AvailableNetwork.NetworkRssiInDecibelMilliwatts, info.AvailableNetwork.PhyKind, info.AvailableNetwork.NetworkKind,
                info.AvailableNetwork.SecuritySettings.NetworkAuthenticationType, info.AvailableNetwork.SecuritySettings.NetworkEncryptionType, info.AvailableNetwork.BeaconInterval, info.AvailableNetwork.Uptime
            ).AppendLine();

        File.WriteAllText(filePath, stringBuilder.ToString(), Encoding.UTF8);
    }

    /// <summary>
    ///     Creates a XML file from the given <see cref="WiFiNetworkInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{WiFiNetworkInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateXml(IEnumerable<WiFiNetworkInfo> collection, string filePath)
    {
        var document = new XDocument(DefaultXDeclaration,
            new XElement(ApplicationName.IPScanner.ToString(),
                new XElement(nameof(IPScannerHostInfo) + "s",
                    from info in collection
                    select
                        new XElement(nameof(IPScannerHostInfo),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.Bssid), info.AvailableNetwork.Bssid),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.Ssid), info.AvailableNetwork.Ssid),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.ChannelCenterFrequencyInKilohertz),
                                info.AvailableNetwork.ChannelCenterFrequencyInKilohertz),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.SignalBars),
                                info.AvailableNetwork.SignalBars),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.IsWiFiDirect),
                                info.AvailableNetwork.IsWiFiDirect),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.NetworkRssiInDecibelMilliwatts),
                                info.AvailableNetwork.NetworkRssiInDecibelMilliwatts),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.PhyKind),
                                info.AvailableNetwork.PhyKind),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.NetworkKind),
                                info.AvailableNetwork.NetworkKind),
                            new XElement(
                                nameof(WiFiNetworkInfo.AvailableNetwork.SecuritySettings.NetworkAuthenticationType),
                                info.AvailableNetwork.SecuritySettings.NetworkAuthenticationType),
                            new XElement(
                                nameof(WiFiNetworkInfo.AvailableNetwork.SecuritySettings.NetworkEncryptionType),
                                info.AvailableNetwork.SecuritySettings.NetworkEncryptionType),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.BeaconInterval),
                                info.AvailableNetwork.BeaconInterval),
                            new XElement(nameof(WiFiNetworkInfo.AvailableNetwork.Uptime),
                                info.AvailableNetwork.Uptime)))));

        document.Save(filePath);
    }

    /// <summary>
    ///     Creates a JSON file from the given <see cref="WiFiNetworkInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{WiFiNetworkInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateJson(IReadOnlyList<WiFiNetworkInfo> collection, string filePath)
    {
        var rawData = new object[collection.Count];

        for (var i = 0; i < collection.Count; i++)
            rawData[i] = new
            {
                collection[i].AvailableNetwork.Bssid,
                collection[i].AvailableNetwork.Ssid,
                ChannelCenterFrequencyInKilohertz = collection[i].AvailableNetwork.Ssid,
                SignalBars = collection[i].AvailableNetwork.SignalBars.ToString(),
                IsWiFiDirect = collection[i].AvailableNetwork.IsWiFiDirect.ToString(),
                NetworkRssiInDecibelMilliwatts = collection[i].AvailableNetwork.NetworkRssiInDecibelMilliwatts
                    .ToString(CultureInfo.CurrentCulture),
                PhyKind = collection[i].AvailableNetwork.PhyKind.ToString(),
                NetworkKind = collection[i].AvailableNetwork.NetworkKind.ToString(),
                NetworkAuthenticationType =
                    collection[i].AvailableNetwork.SecuritySettings.NetworkAuthenticationType.ToString(),
                NetworkEncryptionType =
                    collection[i].AvailableNetwork.SecuritySettings.NetworkEncryptionType.ToString(),
                BeaconInterval = collection[i].AvailableNetwork.BeaconInterval.ToString(),
                Uptime = collection[i].AvailableNetwork.Uptime.ToString()
            };

        File.WriteAllText(filePath, JsonSerializer.Serialize(rawData, typeof(object[]), jsonSerializerOptions), Encoding.UTF8);
    }
}