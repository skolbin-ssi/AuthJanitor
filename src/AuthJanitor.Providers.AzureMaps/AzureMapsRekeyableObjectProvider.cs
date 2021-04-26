﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.Maps;
using Microsoft.Azure.Management.Maps.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AzureMaps
{
    [Provider(Name = "Azure Maps Key",
              Description = "Regenerates a key for an Azure Maps instance",
              SvgImage = ProviderImages.AZURE_MAPS_SVG)]
    public class AzureMapsRekeyableObjectProvider : 
        AuthJanitorProvider<AzureMapsConfiguration>,
        ICanRekey,
        ICanRunSanityTests,
        ICanGenerateTemporarySecretValue,
        ICanEnumerateResourceCandidates
    {
        private const string PRIMARY_KEY = "primary";
        private const string SECONDARY_KEY = "secondary";

        public AzureMapsRekeyableObjectProvider(ProviderWorkflowActionLogger<AzureMapsRekeyableObjectProvider> logger) : base(logger) { }

        public async Task Test()
        {
            var keys = await ManagementClient.Accounts.ListKeysAsync(
                Configuration.ResourceGroup,
                Configuration.ResourceName);
            if (keys == null) throw new Exception("Could not access Azure Maps keys");
        }

        public async Task<RegeneratedSecret> GenerateTemporarySecretValue()
        {
            Logger.LogInformation("Getting temporary secret to use during rekeying from other ({OtherKeyType}) key...", GetOtherKeyType);
            var keys = await ManagementClient.Accounts.ListKeysAsync(
                Configuration.ResourceGroup,
                Configuration.ResourceName);
            Logger.LogInformation("Successfully retrieved temporary secret!");
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(10),
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, GetOtherKeyType).GetSecureString()
            };
        }

        public async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            Logger.LogInformation("Regenerating Azure Maps key type '{KeyType}'", GetKeyType);
            var keys = await ManagementClient.Accounts.RegenerateKeysAsync(
                Configuration.ResourceGroup,
                Configuration.ResourceName,
                new MapsKeySpecification(GetKeyType));
            Logger.LogInformation("Successfully regenerated Azure Maps key type '{KeyType}'", GetKeyType);
            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = GetKeyValue(keys, GetKeyType).GetSecureString()
            };
        }

        public async Task Cleanup()
        {
            if (!Configuration.SkipScramblingOtherKey)
            {
                Logger.LogInformation("Scrambling Azure Maps key type '{OtherKeyType}'", GetOtherKeyType);
                await ManagementClient.Accounts.RegenerateKeysAsync(
                    Configuration.ResourceGroup,
                    Configuration.ResourceName,
                    new MapsKeySpecification(GetOtherKeyType));
            }
            else
                Logger.LogInformation("Skipping scrambling Azure Maps key type '{OtherKeyType}'", GetOtherKeyType);
        }

        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.SkipScramblingOtherKey)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The other (unused) Azure Maps Key is not being scrambled during key rotation",
                    Recommendation = "Unless other services use the alternate key, consider allowing the scrambling of the unused key to 'fully' rekey Azure Maps and maintain a high degree of security."
                });
            }

            return issues;
        }

        public override string GetDescription() =>
            $"Regenerates the {GetKeyType} key for an Azure Maps instance " +
            $"called '{Configuration.ResourceName}' (Resource Group '{Configuration.ResourceGroup}'). " +
            $"The {GetOtherKeyType} key is used as a temporary " +
            $"key while rekeying is taking place. The {GetOtherKeyType} " +
            $"key will {(Configuration.SkipScramblingOtherKey ? "not" : "also")} be rotated.";

        private MapsManagementClient ManagementClient => new MapsManagementClient(Credential.CreateAzureCredentials());

        private string GetKeyValue(MapsAccountKeys accountKeys, string keyType) => keyType switch
        {
            PRIMARY_KEY => accountKeys.PrimaryKey,
            SECONDARY_KEY => accountKeys.SecondaryKey,
            _ => throw new NotImplementedException()
        };

        public async Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IEnumerable<MapsAccount> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await ManagementClient.Accounts.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await ManagementClient.Accounts.ListBySubscriptionAsync();

            return items.Select(i =>
            new ProviderResourceSuggestion()
            {
                Configuration = new AzureMapsConfiguration()
                {
                    ResourceName = i.Name,
                    KeyType = AzureMapsConfiguration.AzureMapsKeyType.Primary
                },
                Name = $"Azure Maps - {i.Name}",
                ProviderType = this.GetType().AssemblyQualifiedName,
                AddressableNames = new[] { i.Name }
            }).ToList();
        }

        private string GetKeyType => Configuration.KeyType switch
        {
            AzureMapsConfiguration.AzureMapsKeyType.Primary => PRIMARY_KEY,
            AzureMapsConfiguration.AzureMapsKeyType.Secondary => SECONDARY_KEY,
            _ => throw new NotImplementedException()
        };

        private string GetOtherKeyType => Configuration.KeyType switch
        {
            AzureMapsConfiguration.AzureMapsKeyType.Primary => SECONDARY_KEY,
            AzureMapsConfiguration.AzureMapsKeyType.Secondary => PRIMARY_KEY,
            _ => throw new NotImplementedException()
        };
    }
}
