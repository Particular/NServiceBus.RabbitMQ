#nullable enable

namespace NServiceBus
{
    using System;

    /// <summary>
    /// The RabbitMQ management API configuration to use instead of inferring values from the connection string.
    /// </summary>
    public class ManagementApiConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ManagementApiConfiguration"/> class.
        /// </summary>
        /// <param name="url">The URL to use when connecting to the RabbitMQ management API.</param>
        public ManagementApiConfiguration(string url)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

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
            ArgumentException.ThrowIfNullOrWhiteSpace(userName);
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            Url = url;
            UserName = userName;
            Password = password;
        }

        /// <summary>
        /// The URL to use when connecting to the RabbitMQ management API.
        /// </summary>
        public string? Url { get; }

        /// <summary>
        /// The user name to use when connecting to the RabbitMQ management API.
        /// </summary>
        public string? UserName { get; }

        /// <summary>
        /// The password to use when connecting to the RabbitMQ management API.
        /// </summary>
        public string? Password { get; }
    }
}
