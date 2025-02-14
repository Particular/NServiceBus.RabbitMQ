#nullable enable

namespace NServiceBus.Transport.RabbitMQ.ManagementApi;

using System;
using System.Collections.Generic;
using System.Linq;

static class FeatureFlagExtensions
{
    public static bool HasEnabledFeature(this List<FeatureFlag> list, string featureName) =>
        list.Any(feature => feature.Name.Equals(featureName, StringComparison.OrdinalIgnoreCase) && feature.Enabled);
}

