namespace NServiceBus.Transports.RabbitMQ
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Linq;

    public class DefaultRoutingKeyConvention
    {
        static bool IsClrType(byte[] a1)
        {
            IStructuralEquatable structuralEquatable = a1;
            return structuralEquatable.Equals(MsPublicKeyToken, StructuralComparisons.StructuralEqualityComparer);
        }

        static bool IsSystemType(Type type)
        {
            bool result;

            if (!IsSystemTypeCache.TryGetValue(type, out result))
            {
                var nameOfContainingAssembly = type.Assembly.GetName().GetPublicKeyToken();
                IsSystemTypeCache[type] = result = IsClrType(nameOfContainingAssembly);
            }

            return result;
        }

        static bool IsNServiceBusMarkerInterface(Type type)
        {
            return type == typeof(IMessage) ||
                   type == typeof(ICommand) ||
                   type == typeof(IEvent);
        }

        public static string GenerateRoutingKey(Type eventType)
        {
            return GetRoutingKey(eventType);
        }

        static string GetRoutingKey(Type type, string key = "")
        {
            var baseType = type.BaseType;


            if (baseType != null && !IsSystemType(baseType))
            {
                key = GetRoutingKey(baseType, key);
            }


            var interfaces = type.GetInterfaces()
                .Where(i => !IsSystemType(i) && !IsNServiceBusMarkerInterface(i)).ToList();

            var implementedInterface = interfaces.FirstOrDefault();

            if (implementedInterface != null)
            {
                key = GetRoutingKey(implementedInterface, key);
            }


            if (!string.IsNullOrEmpty(key))
            {
                key += ".";
            }

            return key + type.FullName.Replace(".", "-");
        }

        static readonly byte[] MsPublicKeyToken = typeof(string).Assembly.GetName().GetPublicKeyToken();

        static readonly ConcurrentDictionary<Type, bool> IsSystemTypeCache =
            new ConcurrentDictionary<Type, bool>();
    }
}