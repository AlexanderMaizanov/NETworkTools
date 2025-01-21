using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using NETworkManager.Models.Network;

namespace NETworkManager.Models.Lookup;

public static class PortLookup
{
    #region Constructor

    /// <summary>
    ///     Loads the ports XML file and creates the lookup.
    /// </summary>
    static PortLookup()
    {
        _portList = [];

        var document = new XmlDocument();
        document.Load(_portsFilePath);

        foreach (XmlNode node in document.SelectNodes("/Ports/Port")!)
        {
            if (node == null)
                continue;

            if (int.TryParse(node.SelectSingleNode("Number")?.InnerText, out var port) &&
                Enum.TryParse<TransportProtocol>(node.SelectSingleNode("Protocol")?.InnerText, true, out var protocol))
                _portList.Add(new PortLookupInfo(port, protocol, node.SelectSingleNode("Name")?.InnerText,
                    node.SelectSingleNode("Description")?.InnerText));
        }

        _ports = (Lookup<int, PortLookupInfo>)_portList.ToLookup(x => x.Number);
    }

    #endregion

    #region Variables

    /// <summary>
    ///     Path to the xml file with all ports, protocols and services located in the resources folder.
    /// </summary>
    private static readonly string _portsFilePath =
        Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)!, "Resources", "Ports.xml");

    /// <summary>
    ///     List of <see cref="PortLookupInfo" /> with all ports, protocols and services.
    /// </summary>
    private static readonly List<PortLookupInfo> _portList;

    /// <summary>
    ///     Lookup of <see cref="PortLookupInfo" /> with all ports, protocols and services. Key is the port number.
    /// </summary>
    private static readonly Lookup<int, PortLookupInfo> _ports;

    #endregion

    #region Methods

    /// <summary>
    ///     Get <see cref="PortLookupInfo" /> by port number async.
    ///     For e.g. port 22 will return 22/tcp, 22/udp and 22/sctp.
    /// </summary>
    /// <param name="port">Port number to get.</param>
    /// <returns>List of <see cref="PortLookupInfo" />. Empty if nothing was found.</returns>
    public static Task<List<PortLookupInfo>> LookupByPortAsync(int port, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<List<PortLookupInfo>>(cancellationToken);
        return Task.Run(() => LookupByPort(port), cancellationToken);
    }

    /// <summary>
    ///     Get <see cref="PortLookupInfo" /> by port number.
    ///     For e.g. port 22 will return 22/tcp, 22/udp and 22/sctp.
    /// </summary>
    /// <param name="port">Port number to get.</param>
    /// <returns>List of <see cref="PortLookupInfo" />. Empty if nothing was found.</returns>
    public static List<PortLookupInfo> LookupByPort(int port)
    {
        return _ports[port].ToList();
    }

    /// <summary>
    ///     Search for <see cref="PortLookupInfo" /> by service or description async.
    ///     For e.g. "ssh" will return 22/tcp, 22/udp and 22/sctp.
    /// </summary>
    /// <param name="search">Service or description to search for.</param>
    /// <returns>List of <see cref="PortLookupInfo" />. Empty if nothing was found.</returns>
    public static Task<List<PortLookupInfo>> SearchByServiceAsync(string search, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<List<PortLookupInfo>>(cancellationToken);
        }
        return Task.Run(() => SearchByService(search), cancellationToken);
    }

    /// <summary>
    ///     Search for <see cref="PortLookupInfo" /> by service or description.
    ///     For e.g. "ssh" will return 22/tcp, 22/udp and 22/sctp.
    /// </summary>
    /// <param name="search">Service or description to search for.</param>
    /// <returns>List of <see cref="PortLookupInfo" />. Empty if nothing was found.</returns>
    public static List<PortLookupInfo> SearchByService(string search)
    {
        return _portList.Where(info =>
            info.Service.IndexOf(search, StringComparison.OrdinalIgnoreCase) > -1 ||
            info.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) > -1
        ).ToList();
    }

    /// <summary>
    ///     Get <see cref="PortLookupInfo" /> by port and protocol async.
    /// </summary>
    /// <param name="port">Port number to get.</param>
    /// <param name="protocol">Port protocol to get. Default is <see cref="TransportProtocol.Tcp" />.</param>
    /// <returns>Port and protocol as <see cref="PortLookupInfo" />. Empty if nothing was found.</returns>
    public static Task<PortLookupInfo> LookupByPortAndProtocolAsync(int port, CancellationToken cancellationToken,
        TransportProtocol protocol = TransportProtocol.Tcp)
    {
        if (cancellationToken.IsCancellationRequested)
        { 
            return Task.FromCanceled<PortLookupInfo>(cancellationToken);
        }
        return Task.Run(() => LookupByPortAndProtocol(port, protocol), cancellationToken);
    }

    /// <summary>
    ///     Get <see cref="PortLookupInfo" /> by port and protocol.
    /// </summary>
    /// <param name="port">Port number to get.</param>
    /// <param name="protocol">Port protocol to get. Default is <see cref="TransportProtocol.Tcp" />.</param>
    /// <returns>Port and protocol as <see cref="PortLookupInfo" />. Empty if nothing was found.</returns>
    public static PortLookupInfo LookupByPortAndProtocol(int port, TransportProtocol protocol = TransportProtocol.Tcp)
    {
        return _ports[port].FirstOrDefault(x => x.Protocol.Equals(protocol)) ??
               new PortLookupInfo(port, protocol);
    }

    #endregion
}