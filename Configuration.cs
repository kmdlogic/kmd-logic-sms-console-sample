using System;

namespace Kmd.Logic.Sms.ConsoleSample
{
    class AppConfiguration
    {
        public LogicEnvironmentConfiguration[] LogicEnvironments { get; set; }
        public string LogicEnvironmentName { get; set; }
        public LogicAccountConfiguration LogicAccount { get; set; }
        public LogicSmsConfiguration Sms { get; set; }
    }

    class LogicEnvironmentConfiguration
    {
        public string Name { get; set; }
        public Uri AuthorizationServerTokenIssuerUri { get; set; }
        public Uri ScopeUri { get; set; }
        public Uri ApiRootUri { get; set; }
    }

    class LogicAccountConfiguration
    {
        public Guid? SubscriptionId { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }

    class LogicSmsConfiguration
    {
        public Guid? ProviderConfigurationId { get; set; }
        public string ToPhoneNumber { get; set; }
        public string SmsBodyText { get; set; }
        public Uri CallbackUrl { get; set; }
    }
}