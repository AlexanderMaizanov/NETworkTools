﻿using NETworkManager.Models.Lookup;

namespace NETworkManager.Models.Network;

/// <summary>
///     Class representing a port info.
/// </summary>
public class PortInfo
{
    /// <summary>
    ///     Create an instance of <see cref="PortInfo" /> with parameters.
    /// </summary>
    /// <param name="port"></param>
    /// <param name="lookupInfo"></param>
    /// <param name="state"></param>
    public PortInfo(int port, PortLookupInfo lookupInfo, PortState state)
    {
        Port = port;
        LookupInfo = lookupInfo;
        State = state;
    }
    public PortInfo() { State = PortState.None; }

    /// <summary>
    ///     Port number.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    ///     Port lookup info like service and description.
    /// </summary>
    public PortLookupInfo LookupInfo { get; set; }

    /// <summary>
    ///     State of the port.
    /// </summary>
    public PortState State { get; set; }
}