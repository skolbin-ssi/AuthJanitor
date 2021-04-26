﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.CryptographicImplementations;
using AuthJanitor.Providers.Azure;
using AuthJanitor.Providers.Capabilities;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using Microsoft.Azure.Management.Sql.Fluent;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.AzureSql
{
    [Provider(Name = "Azure SQL Server Administrator Password",
          Description = "Regenerates the administrator password of an Azure SQL Server",
          SvgImage = ProviderImages.SQL_SERVER_SVG)]
    public class AzureSqlAdministratorPasswordRekeyableObjectProvider : 
        AzureAuthJanitorProvider<AzureSqlAdministratorPasswordConfiguration, ISqlServer>,
        ICanRekey,
        ICanRunSanityTests,
        ICanEnumerateResourceCandidates
    {
        private readonly ICryptographicImplementation _cryptographicImplementation;
        
        public AzureSqlAdministratorPasswordRekeyableObjectProvider(
            ILogger<AzureSqlAdministratorPasswordRekeyableObjectProvider> logger,
            ICryptographicImplementation cryptographicImplementation) : base(logger)
        {
            _cryptographicImplementation = cryptographicImplementation;
        }

        public async Task Test()
        {
            var sqlServer = await GetResourceAsync();
            if (sqlServer == null)
                throw new Exception($"Cannot locate Azure Sql server called '{Configuration.ResourceName}' in group '{Configuration.ResourceGroup}'");
        }
        
        public async Task<RegeneratedSecret> Rekey(TimeSpan requestedValidPeriod)
        {
            Logger.LogInformation("Generating new password of length {PasswordLength}", Configuration.PasswordLength);
            var newPassword = await _cryptographicImplementation.GenerateCryptographicallyRandomString(Configuration.PasswordLength);
            var sqlServer = await GetResourceAsync();
            Logger.LogInformation("Updating administrator password...");
            await sqlServer.Update()
                           .WithAdministratorPassword(newPassword)
                           .ApplyAsync();
            Logger.LogInformation("Password update complete");

            return new RegeneratedSecret()
            {
                Expiry = DateTimeOffset.UtcNow + requestedValidPeriod,
                UserHint = Configuration.UserHint,
                NewSecretValue = newPassword.GetSecureString(),
                NewConnectionString = $"Server=tcp:{Configuration.ResourceName}.database.windows.net,1433;Database={Configuration.DatabaseName};User ID={sqlServer.AdministratorLogin}@{Configuration.ResourceName};Password={newPassword};Trusted_Connection=False;Encrypt=True;"
                                       .GetSecureString()
            };
        }

        public override IList<RiskyConfigurationItem> GetRisks()
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (Configuration.PasswordLength < 16)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The specified password length is extremely short ({Configuration.PasswordLength} characters), making it easier to compromise through brute force attacks",
                    Recommendation = "Increase the length of the password to over 32 characters; prefer 64 or up."
                });
            }
            else if (Configuration.PasswordLength < 32)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 40,
                    Risk = $"The specified password length is somewhat short ({Configuration.PasswordLength} characters), making it easier to compromise through brute force attacks",
                    Recommendation = "Increase the length of the password to over 32 characters; prefer 64 or up."
                });
            }
            return issues;
        }

        public override IList<RiskyConfigurationItem> GetRisks(TimeSpan requestedValidPeriod)
        {
            List<RiskyConfigurationItem> issues = new List<RiskyConfigurationItem>();
            if (requestedValidPeriod == TimeSpan.MaxValue)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 80,
                    Risk = $"The specified Valid Period is TimeSpan.MaxValue, which is effectively Infinity; it is dangerous to allow infinite periods of validity because it allows an object's prior version to be available after the object has been rotated",
                    Recommendation = "Specify a reasonable value for Valid Period"
                });
            }
            else if (requestedValidPeriod == TimeSpan.Zero)
            {
                issues.Add(new RiskyConfigurationItem()
                {
                    Score = 100,
                    Risk = $"The specified Valid Period is zero, so this object will never be allowed to be used",
                    Recommendation = "Specify a reasonable value for Valid Period"
                });
            }
            return issues.Union(GetRisks()).ToList();
        }

        public override string GetDescription() =>
            $"Regenerates the administrator password for an Azure SQL server called " +
            $"'{Configuration.ResourceName}' (Resource Group '{Configuration.ResourceGroup}'). " +
            $"There is no interim key available while this is taking place, so some downtime may occur.";

        protected override ISupportsGettingByResourceGroup<ISqlServer> GetResourceCollection(IAzure azure) => azure.SqlServers;

        public async Task<List<ProviderResourceSuggestion>> EnumerateResourceCandidates(AuthJanitorProviderConfiguration baseConfig)
        {
            var azureConfig = baseConfig as AzureAuthJanitorProviderConfiguration;

            IPagedCollection<ISqlServer> items;
            if (!string.IsNullOrEmpty(azureConfig.ResourceGroup))
                items = await (await GetAzureAsync()).SqlServers.ListByResourceGroupAsync(azureConfig.ResourceGroup);
            else
                items = await (await GetAzureAsync()).SqlServers.ListAsync();

            return (await Task.WhenAll(items.Select(async i =>
            {
                return (await i.Databases.ListAsync()).Select(db =>
                new ProviderResourceSuggestion()
                {
                    Configuration = new AzureSqlAdministratorPasswordConfiguration()
                    {
                        ResourceGroup = i.ResourceGroupName,
                        ResourceName = i.Name,
                        DatabaseName = db.Name,
                        PasswordLength = 32
                    },
                    Name = $"SQL Admin - {i.ResourceGroupName} - {i.Name} (DB: {db.Name})",
                    ProviderType = this.GetType().AssemblyQualifiedName,
                    AddressableNames = new[] { i.FullyQualifiedDomainName }
                });
            }))).SelectMany(f => f).ToList();
        }
    }
}