﻿namespace NServiceBus.Transport.RabbitMQ.Tests;

using System;
using System.Linq;
using NServiceBus.Transport.RabbitMQ.Administration.ManagementClient.Models;

static class FeatureFlagListExtensions
{
    public static bool Contains(this FeatureFlagList featureFlagList, string featureName) =>
        featureFlagList.Any(featureFlag => featureFlag.Name.Equals(featureName, StringComparison.OrdinalIgnoreCase));
}
