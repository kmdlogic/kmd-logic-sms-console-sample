using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Kmd.Logic.Sms.ConsoleSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args)
                    .Build()
                    .Get<AppConfiguration>();

                await Run(config);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Caught a fatal unhandled exception");    
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static async Task Run(AppConfiguration config)
        {
            ValidateConfiguration(config);

            Log.Information("Logic environment is {LogicEnvironmentName}", config.LogicEnvironmentName);
            var logicEnvironment = config.LogicEnvironments.FirstOrDefault(e => e.Name == config.LogicEnvironmentName);
            if (logicEnvironment == null)
            {
                Log.Error("No logic environment named {LogicEnvironmentName}", config.LogicEnvironmentName);
                return;
            }

            Log.Information("Requesting auth token for account ClientID `{LogicAccount}` and subscription `{SubscriptionId}`",
                config.LogicAccount.ClientId, config.LogicAccount.SubscriptionId);

            var token = await RequestToken(
                 logicEnvironment.AuthorizationServerTokenIssuerUri,
                 config.LogicAccount.ClientId,
                 logicEnvironment.ScopeUri.ToString(),
                 config.LogicAccount.ClientSecret);

            Log.Debug("Got access token {@Token}", token);

            var smsSendUri = new Uri(logicEnvironment.ApiRootUri, $"subscriptions/{config.LogicAccount.SubscriptionId}/sms");

            var smsRequest = new SmsSendRequest
            {
                toPhoneNumber = config.Sms.ToPhoneNumber,
                body = config.Sms.SmsBodyText,
                providerConfigurationId = config.Sms.ProviderConfigurationId,
                callbackUrl = config.Sms.CallbackUrl?.ToString(),
            };

            Log.Information("Sending request {@SmsSendRequest} to {SmsSendUri}", smsRequest, smsSendUri);
            var smsResponse = await PostAsync<SmsSendRequest, SmsSendResponse>(smsSendUri, token, smsRequest);
            Log.Information("Got response {@SmsResponse}", smsResponse);
        }

        private static async Task<TResponse> PostAsync<TRequest, TResponse>(Uri url, TokenResponse token, TRequest request)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token.access_token);

                var httpResponse = await httpClient.PostAsJsonAsync(url.ToString(), request);

                Log.Information("Got response {HttpStatus} ({HttpStatusCode}) with HTTP headers {@Headers}",
                    httpResponse.StatusCode, (int)httpResponse.StatusCode,
                    httpResponse.Headers.ToDictionary(k => k.Key, v => v.Value));

                if (httpResponse.IsSuccessStatusCode)
                {
                    return await httpResponse.Content.ReadAsAsync<TResponse>();
                }
                else
                {                    
                    var content = await httpResponse.Content.ReadAsStringAsync();
                    Log.Error("Received non-successful status code {HttpStatusCode} with body {Content}",
                        httpResponse.StatusCode, content);
                    httpResponse.EnsureSuccessStatusCode();
                    throw new Exception("This should never happen");
                }
            }
        }

        private static async Task<TokenResponse> RequestToken(Uri uriAuthorizationServer, string clientId, string scope, string clientSecret)
        {
            HttpResponseMessage responseMessage;

            using (HttpClient client = new HttpClient())
            {
                HttpRequestMessage tokenRequest = new HttpRequestMessage(HttpMethod.Post, uriAuthorizationServer);
                HttpContent httpContent = new FormUrlEncodedContent(
                    new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "client_credentials"),
                        new KeyValuePair<string, string>("client_id", clientId),
                        new KeyValuePair<string, string>("scope", scope),
                        new KeyValuePair<string, string>("client_secret", clientSecret)
                    });
                tokenRequest.Content = httpContent;
                Log.Debug("Requesting an access token {@Request}", tokenRequest);
                responseMessage = await client.SendAsync(tokenRequest);
            }

            return await responseMessage.Content.ReadAsAsync<TokenResponse>();
        }

        private static void ValidateConfiguration(AppConfiguration config)
        {
            if (config.LogicAccount == null
                || string.IsNullOrWhiteSpace(config.LogicAccount?.ClientId)
                || string.IsNullOrWhiteSpace(config.LogicAccount?.ClientSecret)
                || config.LogicAccount?.SubscriptionId == null)
            {
                Log.Error("Please add your LogicAccount configuration to `appsettings.json`. You currently have {@LogicAccount}",
                    config.LogicAccount);
                return;
            }

            if (config.Sms == null
                || config.Sms?.ProviderConfigurationId == null
                || string.IsNullOrWhiteSpace(config.Sms?.ToPhoneNumber)
                || string.IsNullOrWhiteSpace(config.Sms?.SmsBodyText))
            {
                Log.Error("Please add your Sms configuration to `appsettings.json`. You currently have {@Sms}",
                    config.Sms);
                return;
            }
        }

        public class TokenResponse
        {
            public string token_type { get; set; }
            public int expires_in { get; set; }
            public int ext_expires_in { get; set; }
            public string access_token { get; set; }
        }

        public class SmsSendRequest
        {
            public string toPhoneNumber { get; set; }
            public string body { get; set; }
            public string callbackUrl { get; set; }
            public Guid? providerConfigurationId { get; set; }
        }

        public class SmsSendResponse
        {
            public Guid smsMessageId { get; set; }
        }
    }
}
