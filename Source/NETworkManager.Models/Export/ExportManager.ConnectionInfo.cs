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
    ///     Method to export objects from type <see cref="ConnectionInfo" /> to a file.
    /// </summary>
    /// <param name="filePath">Path to the export file.</param>
    /// <param name="fileType">Allowed <see cref="ExportFileType" /> are CSV, XML or JSON.</param>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ConnectionInfo}" /> to export.</param>
    public static void Export(string filePath, ExportFileType fileType, IReadOnlyList<ConnectionInfo> collection)
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
    ///     Creates a CSV file from the given <see cref="ConnectionInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ConnectionInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateCsv(IEnumerable<ConnectionInfo> collection, string filePath)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendJoin(",",
               nameof(ConnectionInfo.Protocol), nameof(ConnectionInfo.LocalIPAddress), nameof(ConnectionInfo.LocalPort), nameof(ConnectionInfo.RemoteIPAddress),
               nameof(ConnectionInfo.RemotePort), nameof(ConnectionInfo.RemoteHostname), nameof(ConnectionInfo.TcpState), nameof(ConnectionInfo.ProcessId), 
               nameof(ConnectionInfo.ProcessName), nameof(ConnectionInfo.ProcessPath)
        ).AppendLine();

        foreach (var info in collection)
            stringBuilder.AppendJoin(",", 
                info.Protocol, info.LocalIPAddress, info.LocalPort, info.RemoteIPAddress,
                info.RemotePort,info.RemoteHostname,info.TcpState, info.ProcessId,
                info.ProcessName, info.ProcessPath
            ).AppendLine();

        File.WriteAllText(filePath, stringBuilder.ToString());
    }

    /// <summary>
    ///     Creates a XML file from the given <see cref="ConnectionInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ConnectionInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateXml(IEnumerable<ConnectionInfo> collection, string filePath)
    {
        var document = new XDocument(DefaultXDeclaration,
            new XElement(ApplicationName.Connections.ToString(),
                new XElement(nameof(ConnectionInfo) + "s",
                    from info in collection
                    select
                        new XElement(nameof(ConnectionInfo),
                            new XElement(nameof(ConnectionInfo.Protocol), info.Protocol),
                            new XElement(nameof(ConnectionInfo.LocalIPAddress), info.LocalIPAddress),
                            new XElement(nameof(ConnectionInfo.LocalPort), info.LocalPort),
                            new XElement(nameof(ConnectionInfo.RemoteIPAddress), info.RemoteIPAddress),
                            new XElement(nameof(ConnectionInfo.RemotePort), info.RemotePort),
                            new XElement(nameof(ConnectionInfo.RemoteHostname), info.RemoteHostname),
                            new XElement(nameof(ConnectionInfo.TcpState), info.TcpState),
                            new XElement(nameof(ConnectionInfo.ProcessId), info.ProcessId),
                            new XElement(nameof(ConnectionInfo.ProcessName), info.ProcessName),
                            new XElement(nameof(ConnectionInfo.ProcessPath), info.ProcessPath)
                        ))));

        document.Save(filePath);
    }

    /// <summary>
    ///     Creates a JSON file from the given <see cref="ConnectionInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{ConnectionInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateJson(IReadOnlyList<ConnectionInfo> collection, string filePath)
    {
        var rawData = new object[collection.Count];

        for (var i = 0; i < collection.Count; i++)
            rawData[i] = new
            {
                Protocol = collection[i].Protocol.ToString(),
                LocalIPAddress = collection[i].LocalIPAddress.ToString(),
                collection[i].LocalPort,
                RemoteIPAddress = collection[i].RemoteIPAddress.ToString(),
                collection[i].RemotePort,
                collection[i].RemoteHostname,
                TcpState = collection[i].TcpState.ToString(),
                collection[i].ProcessId,
                collection[i].ProcessName,
                collection[i].ProcessPath
            };

        File.WriteAllText(filePath, JsonSerializer.Serialize(rawData, typeof(object[]), jsonSerializerOptions), Encoding.UTF8);
    }
}