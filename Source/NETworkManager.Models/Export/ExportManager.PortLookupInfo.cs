﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NETworkManager.Models.Lookup;
using Newtonsoft.Json;

namespace NETworkManager.Models.Export;

public static partial class ExportManager
{
    /// <summary>
    /// Method to export objects from type <see cref="PortLookupInfo"/> to a file.
    /// </summary>
    /// <param name="filePath">Path to the export file.</param>
    /// <param name="fileType">Allowed <see cref="ExportFileType"/> are CSV, XML or JSON.</param>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{PortLookupInfo}"/> to export.</param>
    public static void Export(string filePath, ExportFileType fileType, IReadOnlyList<PortLookupInfo> collection)
    {
        switch (fileType)
        {
            case ExportFileType.CSV:
                CreateCsv(collection, filePath);
                break;
            case ExportFileType.XML:
                CreateXml(collection, filePath);
                break;
            case ExportFileType.JSON:
                CreateJson(collection, filePath);
                break;
            case ExportFileType.TXT:
            default:
                throw new ArgumentOutOfRangeException(nameof(fileType), fileType, null);
        }
    }
    
    private static void CreateCsv(IEnumerable<PortLookupInfo> collection, string filePath)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine(
            $"{nameof(PortLookupInfo.Number)},{nameof(PortLookupInfo.Protocol)},{nameof(PortLookupInfo.Service)},{nameof(PortLookupInfo.Description)}");

        foreach (var info in collection)
            stringBuilder.AppendLine($"{info.Number},{info.Protocol},{info.Service},\"{info.Description}\"");

        System.IO.File.WriteAllText(filePath, stringBuilder.ToString());
    }

    private static void CreateXml(IEnumerable<PortLookupInfo> collection, string filePath)
    {
        var document = new XDocument(DefaultXDeclaration,
            new XElement(ApplicationName.Lookup.ToString(),
                new XElement(nameof(PortLookupInfo) + "s",
                    from info in collection
                    select
                        new XElement(nameof(PortLookupInfo),
                            new XElement(nameof(PortLookupInfo.Number), info.Number),
                            new XElement(nameof(PortLookupInfo.Protocol), info.Protocol),
                            new XElement(nameof(PortLookupInfo.Service), info.Service),
                            new XElement(nameof(PortLookupInfo.Description), info.Description)))));

        document.Save(filePath);
    }

    private static void CreateJson(IReadOnlyList<PortLookupInfo> collection, string filePath)
    {
        var jsonData = new object[collection.Count];

        for (var i = 0; i < collection.Count; i++)
        {
            jsonData[i] = new
            {
                collection[i].Number,
                Protocol = collection[i].Protocol.ToString(),
                collection[i].Service,
                collection[i].Description
            };
        }

        System.IO.File.WriteAllText(filePath, JsonConvert.SerializeObject(jsonData, Formatting.Indented));
    }
}