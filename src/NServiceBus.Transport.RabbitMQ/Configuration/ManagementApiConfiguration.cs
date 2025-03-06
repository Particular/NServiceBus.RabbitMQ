#nullable enable

namespace NServiceBus
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The RabbitMQ management API configuration to use instead of inferring values from the connection string.
    /// </summary>
    public class ManagementApiConfiguration
    {
        ManagementApiConfiguration() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagementApiConfiguration"/> class.
        /// </summary>
        /// <param name="url">The URL to use when connecting to the RabbitMQ management API.</param>
        public ManagementApiConfiguration(string url)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ThrowIfNotValidUrl(url);

            Url = url;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagementApiConfiguration"/> class.
        /// </summary>
        /// <param name="userName">The user name to use when connecting to the RabbitMQ management API.</param>
        /// <param name="password">The password to use when connecting to the RabbitMQ management API.</param>
        public ManagementApiConfiguration(string userName, string password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userName);
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            UserName = userName;
            Password = password;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ManagementApiConfiguration"/> class.
        /// </summary>
        /// <param name="url">The URL to use when connecting to the RabbitMQ management API.</param>
        /// <param name="userName">The user name to use when connecting to the RabbitMQ management API.</param>
        /// <param name="password">The password to use when connecting to the RabbitMQ management API.</param>
        public ManagementApiConfiguration(string url, string userName, string password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ThrowIfNotValidUrl(url);
            ArgumentException.ThrowIfNullOrWhiteSpace(userName);
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            Url = url;
            UserName = userName;
            Password = password;
        }

        /// <summary>
        /// The URL to use when connecting to the RabbitMQ management API.
        /// </summary>
        public string? Url { get; private init; }

        /// <summary>
        /// The user name to use when connecting to the RabbitMQ management API.
        /// </summary>
        public string? UserName { get; private init; }

        /// <summary>
        /// The password to use when connecting to the RabbitMQ management API.
        /// </summary>
        public string? Password { get; private init; }

        internal static ManagementApiConfiguration Create(string? url, string? userName, string? password)
        {
            ThrowIfWhiteSpace(url);

            if (url is not null)
            {
                ThrowIfNotValidUrl(url);
            }

            ThrowIfWhiteSpace(userName);
            ThrowIfWhiteSpace(password);

            return new ManagementApiConfiguration
            {
                Url = url,
                UserName = userName,
                Password = password
            };
        }

        static void ThrowIfWhiteSpace(string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is not null && string.IsNullOrWhiteSpace(argument))
            {
                throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", paramName);
            }
        }

        static void ThrowIfNotValidUrl(string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (!Uri.IsWellFormedUriString(argument, UriKind.RelativeOrAbsolute))
            {
                throw new ArgumentException("The value is not a valid URL.", paramName);
            }
        }

    }
}
