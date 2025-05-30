﻿using System;
using System.Collections.Generic;
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
    ///     Method to export objects from type <see cref="ARPInfo" /> to a file.
    /// </summary>
    /// <param name="filePath">Path to the export file.</param>
    /// <param name="fileType">Allowed <see cref="ExportFileType" /> are CSV, XML or JSON.</param>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ARPInfo}" /> to export.</param>
    public static void Export<T, TCollection>(string filePath, ExportFileType fileType, TCollection collection)
        where TCollection : IReadOnlyList<T>
        where T : ARPInfo
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
    ///     Creates a CSV file from the given <see cref="ARPInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ARPInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateCsv(IEnumerable<ARPInfo> collection, string filePath)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendJoin(",",
            nameof(ARPInfo.IPAddress),nameof(ARPInfo.MACAddress),nameof(ARPInfo.IsMulticast)
        ).AppendLine();

        foreach (var info in collection)
            stringBuilder.AppendJoin(",", info.IPAddress, info.MACAddress, info.IsMulticast).AppendLine();

        File.WriteAllText(filePath, stringBuilder.ToString());
    }

    /// <summary>
    ///     Creates a XML file from the given <see cref="ARPInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ARPInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateXml(IEnumerable<ARPInfo> collection, string filePath)
    {
        var document = new XDocument(DefaultXDeclaration,
            new XElement(ApplicationName.ARPTable.ToString(),
                new XElement(nameof(ARPInfo) + "s",
                    from info in collection
                    select
                        new XElement(nameof(ARPInfo),
                            new XElement(nameof(ARPInfo.IPAddress), info.IPAddress),
                            new XElement(nameof(ARPInfo.MACAddress), info.MACAddress),
                            new XElement(nameof(ARPInfo.IsMulticast), info.IsMulticast)))));

        document.Save(filePath);
    }

    /// <summary>
    ///     Creates a JSON file from the given <see cref="ARPInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ARPInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    
    private static void CreateJson(IReadOnlyList<ARPInfo> collection, string filePath)
    {
        var rawData = new object[collection.Count];

        for (var i = 0; i < collection.Count; i++)
            rawData[i] = new
            {
                IPAddress = collection[i].IPAddress.ToString(),
                MACAddress = collection[i].MACAddress.ToString(),
                collection[i].IsMulticast
            };

        File.WriteAllText(filePath, JsonSerializer.Serialize(rawData, typeof(object[]), jsonSerializerOptions), Encoding.UTF8);
    }
}