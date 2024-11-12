using System;
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
    ///     Method to export objects from type <see cref="BitCalculatorInfo" /> to a file.
    /// </summary>
    /// <param name="filePath">Path to the export file.</param>
    /// <param name="fileType">Allowed <see cref="ExportFileType" /> are CSV, XML or JSON.</param>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{BitCaluclatorInfo}" /> to export.</param>
    public static void Export(string filePath, ExportFileType fileType, IReadOnlyList<BitCalculatorInfo> collection)
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
    ///     Creates a CSV file from the given <see cref="BitCalculatorInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{BitCaluclatorInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateCsv(IEnumerable<BitCalculatorInfo> collection, string filePath)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendJoin(",",
            nameof(BitCalculatorInfo.Bits),nameof(BitCalculatorInfo.Bytes),nameof(BitCalculatorInfo.Kilobits),nameof(BitCalculatorInfo.Kilobytes),
            nameof(BitCalculatorInfo.Megabits),nameof(BitCalculatorInfo.Megabytes),nameof(BitCalculatorInfo.Gigabits),nameof(BitCalculatorInfo.Gigabytes),
            nameof(BitCalculatorInfo.Terabits),nameof(BitCalculatorInfo.Terabytes),nameof(BitCalculatorInfo.Petabits),nameof(BitCalculatorInfo.Petabytes)
        ).AppendLine();

        foreach (var info in collection)
            stringBuilder.AppendJoin(",",
                info.Bits,info.Bytes,info.Kilobits,info.Kilobytes,
                info.Megabits,info.Megabytes,info.Gigabits,info.Gigabytes,
                info.Terabits,info.Terabytes,info.Petabits,info.Petabytes
            ).AppendLine();

        File.WriteAllText(filePath, stringBuilder.ToString());
    }

    /// <summary>
    ///     Creates a XML file from the given <see cref="BitCalculatorInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{BitCaluclatorInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    private static void CreateXml(IEnumerable<BitCalculatorInfo> collection, string filePath)
    {
        var document = new XDocument(DefaultXDeclaration,
            new XElement(ApplicationName.BitCalculator.ToString(),
                new XElement(nameof(BitCalculatorInfo) + "s",
                    from info in collection
                    select
                        new XElement(nameof(BitCalculatorInfo),
                            new XElement(nameof(BitCalculatorInfo.Bits), info.Bits),
                            new XElement(nameof(BitCalculatorInfo.Bytes), info.Bytes),
                            new XElement(nameof(BitCalculatorInfo.Kilobits), info.Kilobits),
                            new XElement(nameof(BitCalculatorInfo.Kilobytes), info.Kilobytes),
                            new XElement(nameof(BitCalculatorInfo.Megabits), info.Megabits),
                            new XElement(nameof(BitCalculatorInfo.Megabytes), info.Megabytes),
                            new XElement(nameof(BitCalculatorInfo.Gigabits), info.Gigabits),
                            new XElement(nameof(BitCalculatorInfo.Gigabytes), info.Gigabytes),
                            new XElement(nameof(BitCalculatorInfo.Terabits), info.Terabits),
                            new XElement(nameof(BitCalculatorInfo.Terabytes), info.Terabytes),
                            new XElement(nameof(BitCalculatorInfo.Petabits), info.Petabits),
                            new XElement(nameof(BitCalculatorInfo.Petabytes), info.Petabytes)))));

        document.Save(filePath);
    }

    /// <summary>
    ///     Creates a JSON file from the given <see cref="BitCalculatorInfo" /> collection.
    /// </summary>
    /// <param name="collection">Objects as <see cref="IReadOnlyList{BitCaluclatorInfo}" /> to export.</param>
    /// <param name="filePath">Path to the export file.</param>
    
    private static void CreateJson(IReadOnlyList<BitCalculatorInfo> collection, string filePath)
    {
        var rawData = new object[collection.Count];

        for (var i = 0; i < collection.Count; i++)
            rawData[i] = new
            {
                collection[i].Bits,
                collection[i].Bytes,
                collection[i].Kilobits,
                collection[i].Kilobytes,
                collection[i].Megabits,
                collection[i].Megabytes,
                collection[i].Gigabits,
                collection[i].Gigabytes,
                collection[i].Terabits,
                collection[i].Terabytes,
                collection[i].Petabits,
                collection[i].Petabytes
            };

        File.WriteAllText(filePath, JsonSerializer.Serialize(rawData, typeof(object[]), jsonSerializerOptions), Encoding.UTF8);
    }
}