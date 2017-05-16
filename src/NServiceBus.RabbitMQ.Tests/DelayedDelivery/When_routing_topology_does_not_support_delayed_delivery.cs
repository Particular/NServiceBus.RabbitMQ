namespace NServiceBus.Transport.RabbitMQ.Tests.DelayedDelivery
{
    using Features;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    class When_routing_topology_does_not_support_delayed_delivery
    {
        const string coreSendOnlyEndpointKey = "Endpoint.SendOnly";
        const string coreExternalTimeoutManagerAddressKey = "NServiceBus.ExternalTimeoutManagerAddress";

        SettingsHolder settings;

        [SetUp]
        public void Setup()
        {
            settings = new SettingsHolder();
            settings.Set(SettingsKeys.RoutingTopologySupportsDelayedDelivery, false);
        }

        [Test]
        public void Should_allow_startup_if_DisableTimeoutManager_setting_is_not_set()
        {
            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_prevent_startup_if_DisableTimeoutManager_setting_is_set()
        {
            settings.Set(SettingsKeys.DisableTimeoutManager, true);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.False(result.Succeeded);
            Assert.AreEqual("Cannot disable the timeout manager when the specified routing topology does not implement ISupportDelayedDelivery.", result.ErrorMessage);
        }

        [Test]
        public void Should_allow_startup_if_timeout_manager_feature_is_active()
        {
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Active);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_allow_startup_if_timeout_manager_feature_is_disabled()
        {
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Disabled);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_allow_startup_if_timeout_manager_feature_is_deactivated_by_send_only()
        {
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Deactivated);
            settings.Set(coreSendOnlyEndpointKey, true);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_allow_startup_if_external_timeout_manager_address_is_configured()
        {
            settings.Set(coreExternalTimeoutManagerAddressKey, "endpoint");

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }
    }
}
