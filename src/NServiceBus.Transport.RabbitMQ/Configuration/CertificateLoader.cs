#nullable enable

namespace NServiceBus.Transport.RabbitMQ;

using System;
using System.Security.Cryptography.X509Certificates;

static class CertificateLoader
{
    public static X509Certificate2 LoadCertificateFromFile(string path, string? password)
    {
        var contentType = X509Certificate2.GetCertContentType(path);

#pragma warning disable IDE0072 // Add missing cases
        var certificate = contentType switch
        {
            X509ContentType.Cert => X509CertificateLoader.LoadCertificateFromFile(path),
            X509ContentType.Pkcs12 => X509CertificateLoader.LoadPkcs12FromFile(path, password),
            _ => throw new NotSupportedException($"Certificate content type '{contentType}' is not supported.")
        };
#pragma warning restore IDE0072 // Add missing cases

        return certificate;
    }
}
