#nullable enable

namespace NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

using System;
using System.Collections.Generic;
using System.Linq;

class FeatureFlagList : List<FeatureFlag>
{
    public bool HasEnabledFeature(string featureName) =>
        this.Any(feature => feature.Name.Equals(featureName, StringComparison.OrdinalIgnoreCase) && feature.IsEnabled);
}

