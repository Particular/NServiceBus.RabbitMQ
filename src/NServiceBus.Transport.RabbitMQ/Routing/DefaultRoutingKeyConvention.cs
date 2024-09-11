#nullable disable
namespace NServiceBus.Transport.RabbitMQ
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Linq;
    using Logging;

    static class DefaultRoutingKeyConvention
    {
        public static string GenerateRoutingKey(Type eventType) =>
            EventTypeToRoutingKeyCache.GetOrAdd(eventType, static eventType => GetRoutingKey(eventType));

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

            if (interfaces.Count > 1)
            {
                Logger.WarnFormat("The direct routing topology cannot properly publish a message type that implements more than one relevant interface. The type '{0}' implements the following interfaces: {1}. The interface that will be used is '{2}'. The others will be ignored, and any endpoints that subscribe to those interfaces will not receive a copy of the message.", type, string.Join(", ", interfaces), implementedInterface);
            }

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

        static bool IsSystemType(Type type) =>
            IsSystemTypeCache.GetOrAdd(type, static type =>
            {
                var nameOfContainingAssembly = type.Assembly.GetName().GetPublicKeyToken();
                return IsClrType(nameOfContainingAssembly);
            });

        static bool IsClrType(byte[] a1)
        {
            IStructuralEquatable structuralEquatable = a1;
            return structuralEquatable.Equals(MsPublicKeyToken, StructuralComparisons.StructuralEqualityComparer);
        }

        static bool IsNServiceBusMarkerInterface(Type type) => type == typeof(IMessage) || type == typeof(ICommand) || type == typeof(IEvent);

        static readonly byte[] MsPublicKeyToken = typeof(string).Assembly.GetName().GetPublicKeyToken();
        static readonly ConcurrentDictionary<Type, bool> IsSystemTypeCache = new();
        static readonly ConcurrentDictionary<Type, string> EventTypeToRoutingKeyCache = new();
        static readonly ILog Logger = LogManager.GetLogger(typeof(DefaultRoutingKeyConvention));
    }
}