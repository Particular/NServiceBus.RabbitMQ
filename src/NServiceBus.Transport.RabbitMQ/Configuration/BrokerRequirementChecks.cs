namespace NServiceBus;

using System;

//Any changes to BrokerRequirementChecks need to be synchronzied with the `all` value in BrokerVerifier.Initialize.

/// <summary>
/// The broker requirements that the transport will verify.
/// </summary>
[Flags]
public enum BrokerRequirementChecks
{
    /// <summary>
    ///  None
    /// </summary>
    None = 0,

    /// <summary>
    /// The transport requires broker version 3.10 or newer.
    /// </summary>
    Version310OrNewer = 1,

    /// <summary>
    /// The 'stream-queue' feature flag needs to be enabled.
    /// </summary>
    StreamsEnabled = 2,
}

