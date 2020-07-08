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
                throw new ArgumentOutOfRangeException(nameof(name), name, "Value exceeds the maximum allowed length of 255 bytes.");
            }
        }
    }
}
