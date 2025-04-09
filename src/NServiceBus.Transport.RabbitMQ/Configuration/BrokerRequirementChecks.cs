namespace NServiceBus;

using System;

/// <summary>
///
/// </summary>
[Flags]
public enum BrokerRequirementChecks
{
    /// <summary>
    ///
    /// </summary>
    None = 0,

    /// <summary>
    ///
    /// </summary>
    Version310OrNewer = 1,

    /// <summary>
    ///
    /// </summary>
    StreamsEnabled = 2,
}

