#nullable enable

namespace NServiceBus
{
    using System;

    /// <summary>
    ///
    /// </summary>
    public class ManagementApiConfiguration
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="url"></param>
        public ManagementApiConfiguration(string url)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            Url = url;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="url"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
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
        ///
        /// </summary>
        public string Url { get; }

        /// <summary>
        ///
        /// </summary>
        public string? UserName { get; }

        /// <summary>
        ///
        /// </summary>
        public string? Password { get; }
    }
}
