namespace NServiceBus.Transport.RabbitMQ;

public static class NativeIntegrationDispatchPropertiesExtensions
{
    internal static readonly string ContentTypeAttribute = "AMQP.content-type";

    /// <summary>
    /// Sets the content type to be used in the AMQP content-type field, overriding NServiceBus header.
    /// </summary>
    /// <param name="dispatchProperties">Dispatch properties object.</param>
    /// <param name="contentType">Content type value.</param>
    public static void SetContentType(this DispatchProperties dispatchProperties, string contentType)
    {
        dispatchProperties[ContentTypeAttribute] = contentType;
    }
}