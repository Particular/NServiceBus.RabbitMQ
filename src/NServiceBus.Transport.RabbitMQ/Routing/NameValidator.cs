using System;

namespace NServiceBus.Transport.RabbitMQ
{
    static class NameValidator
    {
        public static void ThrowIfNameIsTooLong(string name)
        {
            if (name.Length > 255)
            {
                throw new Exception($"{name} exceeds 255 characters which is maximal length for a queue or an exchange.");
            }
        }
    }
}