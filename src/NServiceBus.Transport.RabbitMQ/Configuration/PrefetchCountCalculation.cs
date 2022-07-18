namespace NServiceBus
{
    /// <summary>
    /// Calculates the value for the prefetch count based on the endpoint's maximum concurrency setting.
    /// </summary>
    /// <param name="maximumConcurrency">Maximum concurrency of the message receiver.</param>
    /// <returns>The prefetch count to use for the receiver.</returns>
    public delegate long PrefetchCountCalculation(int maximumConcurrency);
}