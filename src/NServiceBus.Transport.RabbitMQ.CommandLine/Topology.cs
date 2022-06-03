namespace NServiceBus
{
    /// <summary>
    /// Defines built-in topologies.
    /// </summary>
    public enum Topology
    {
        /// <summary>
        /// The conventional routing topology.
        ///
        /// Uses an exchange cascade convention to route published messages.
        /// </summary>
        Conventional,
        /// <summary>
        /// The direct routing topology.
        ///
        /// Uses topic exchange to route published messages.
        /// </summary>
        Direct,
    }
}