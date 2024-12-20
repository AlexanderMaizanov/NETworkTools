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
    ///     Method to export objects from type <see cref="ListenerInfo" /> to a file.
    /// </summary>
    /// <param name="filePath">Path to the export file.</param>
    /// <param name="fileType">Allowed <see cref="ExportFileType" /> are CSV, XML or JSON.</param>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ListenerInfo}" /> to export.</param>
    public static void Export(string filePath, ExportFileType fileType, IReadOnlyList<ListenerInfo> collection)
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
    ///     Creates a CSV file from the given <see cref="ListenerInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ListenerInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateCsv(IEnumerable<ListenerInfo> collection, string filePath)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendJoin(",",
            nameof(ListenerInfo.Protocol),nameof(ListenerInfo.IPAddress),nameof(ListenerInfo.Port)
        ).AppendLine();

        foreach (var info in collection)
            stringBuilder.AppendJoin(",", info.Protocol,info.IPAddress,info.Port).AppendLine();

        File.WriteAllText(filePath, stringBuilder.ToString(), Encoding.UTF8);
    }

    /// <summary>
    ///     Creates a XML file from the given <see cref="ListenerInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ListenerInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateXml(IEnumerable<ListenerInfo> collection, string filePath)
    {
        var document = new XDocument(DefaultXDeclaration,
            new XElement(ApplicationName.Listeners.ToString(),
                new XElement(nameof(ListenerInfo) + "s",
                    from info in collection
                    select
                        new XElement(nameof(ListenerInfo),
                            new XElement(nameof(ListenerInfo.Protocol), info.Protocol),
                            new XElement(nameof(ListenerInfo.IPAddress), info.IPAddress),
                            new XElement(nameof(ListenerInfo.Port), info.Port)))));

        document.Save(filePath);
    }

    /// <summary>
    ///     Creates a JSON file from the given <see cref="ListenerInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ListenerInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateJson(IReadOnlyList<ListenerInfo> collection, string filePath)
    {
        var rawData = new object[collection.Count];

        for (var i = 0; i < collection.Count; i++)
            rawData[i] = new
            {
                Protocol = collection[i].Protocol.ToString(),
                IPAddress = collection[i].IPAddress.ToString(),
                collection[i].Port
            };

        File.WriteAllText(filePath, JsonSerializer.Serialize(rawData, typeof(object[]),jsonSerializerOptions), Encoding.UTF8);
    }
}