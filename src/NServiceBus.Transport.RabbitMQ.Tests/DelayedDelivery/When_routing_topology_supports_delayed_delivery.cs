namespace NServiceBus.Transport.RabbitMQ.Tests.DelayedDelivery
{
    using Features;
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    class When_routing_topology_supports_delayed_delivery
    {
        const string coreExternalTimeoutManagerAddressKey = "NServiceBus.ExternalTimeoutManagerAddress";

        SettingsHolder settings;

        [SetUp]
        public void Setup()
        {
            settings = new SettingsHolder();
        }

        [Test]
        public void Should_allow_startup_if_EnableTimeoutManager_setting_is_not_set()
        {
            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_allow_startup_if_EnableTimeoutManager_setting_is_not_set_and_timeout_manager_feature_is_disabled()
        {
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Disabled);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_allow_startup_if_EnableTimeoutManager_setting_is_not_set_and_timeout_manager_feature_is_deactivated()
        {
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Deactivated);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_allow_startup_if_EnableTimeoutManager_setting_is_set_and_timeout_manager_feature_is_active()
        {
            settings.Set(SettingsKeys.EnableTimeoutManager, true);
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Active);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.True(result.Succeeded);
        }

        [Test]
        public void Should_prevent_startup_if_EnableTimeoutManager_setting_is_set_and_timeout_feature_is_disabled()
        {
            settings.Set(SettingsKeys.EnableTimeoutManager, true);
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Disabled);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.False(result.Succeeded);
            Assert.AreEqual("The transport has been configured to enable the timeout manager, but the timeout manager is not active." +
                        "Ensure that the timeout manager is active or remove the call to 'EndpointConfiguration.UseTransport<RabbitMQTransport>().DelayedDelivery().EnableTimeoutManager()'.", result.ErrorMessage);
        }

        [Test]
        public void Should_prevent_startup_if_EnableTimeoutManager_setting_is_set_and_timeout_feature_is_deactivated()
        {
            settings.Set(SettingsKeys.EnableTimeoutManager, true);
            settings.Set(typeof(TimeoutManager).FullName, FeatureState.Deactivated);

            var result = DelayInfrastructure.CheckForInvalidSettings(settings);

            Assert.False(result.Succeeded);
            Assert.AreEqual("The transport has been configured to enable the timeout manager, but the timeout manager is not active." +
                        "Ensure that the timeout manager is active or remove the call to 'EndpointConfiguration.UseTransport<RabbitMQTransport>().DelayedDelivery().EnableTimeoutManager()'.", result.ErrorMessage);
        }
    }
}
