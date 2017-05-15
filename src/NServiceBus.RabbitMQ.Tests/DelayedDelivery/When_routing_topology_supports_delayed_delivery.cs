namespace NServiceBus.Transport.RabbitMQ.Tests.DelayedDelivery
{
    using Features;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    class When_routing_topology_supports_delayed_delivery
    {
        SettingsHolder settings;

        [SetUp]
        public void Setup()
        {
            settings = new SettingsHolder();
            settings.Set(SettingsKeys.RoutingTopologySupportsDelayedDelivery, true);
        }

        [Test]
        public void Should_allow_startup_if_DisableTimeoutManager_setting_is_not_set()
        {
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Active);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_allow_startup_if_DisableTimeoutManager_setting_is_set()
        {
            settings.Set(SettingsKeys.DisableTimeoutManager, true);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_allow_startup_if_timeout_manager_feature_is_active()
        {
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Active);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_prevent_startup_if_timeout_manager_feature_is_disabled_and_DisableTimeoutManager_setting_is_not_set()
        {
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Disabled);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.False(result.Succeeded);
            Assert.AreEqual("The timeout manager is not active, but the transport has not been properly configured for this. " +
                        "Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().DelayedDelivery().DisableTimeoutManager()' to ensure delayed messages can be sent properly.", result.ErrorMessage);
        }

        [Test]
        public void Should_allow_startup_if_timeout_manager_feature_is_disabled_and_DisableTimeoutManager_setting_is_set()
        {
            settings.Set(SettingsKeys.DisableTimeoutManager, true);
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Disabled);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_prevent_startup_if_timeout_manager_feature_is_deactivated_by_send_only_and_DisableTimeoutManager_setting_is_not_set()
        {
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Deactivated);
            settings.Set("Endpoint.SendOnly", true);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.False(result.Succeeded);
            Assert.AreEqual("The timeout manager is not active, but the transport has not been properly configured for this. " +
                     "Use 'EndpointConfiguration.UseTransport<RabbitMQTransport>().DelayedDelivery().DisableTimeoutManager()' to ensure delayed messages can be sent properly.", result.ErrorMessage);
        }

        [Test]
        public void Should_allow_startup_if_timeout_manager_feature_is_deactivated_by_send_only_and_DisableTimeoutManager_setting_set()
        {
            settings.Set(SettingsKeys.DisableTimeoutManager, true);
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Deactivated);
            settings.Set("Endpoint.SendOnly", true);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_prevent_startup_if_external_timeout_manager_address_is_configured()
        {
            settings.Set("NServiceBus.ExternalTimeoutManagerAddress", "endpoint");

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.False(result.Succeeded);
            Assert.AreEqual("An external timeout manager address cannot be configured because the timeout manager is not being used for delayed delivery.", result.ErrorMessage);
        }
    }
}
