using System;
using System.Text;

namespace NServiceBus.Transport.RabbitMQ
{
    static class NameValidator
    {
        public static void ThrowIfNameIsTooLong(string name)
        {
            if (Encoding.UTF8.GetByteCount(name) > 255)
            {
                throw new Exception($"{name} exceeds 255 bytes which is maximal length for a queue or an exchange.");
            }
        }
    }
}
