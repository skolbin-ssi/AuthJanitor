﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.UI.Shared.Models;
using AuthJanitor.UI.Shared.ViewModels;
using AuthJanitor.Providers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using AuthJanitor.IdentityServices;
using AuthJanitor.Providers.Capabilities;
using AuthJanitor.DataStores;

namespace AuthJanitor.UI.Shared
{
    public static class ViewModelFactory
    {
        private static IDictionary<Type, ProviderConfigurationItemViewModel.InputTypes> InputTypes { get; } = new Dictionary<Type, ProviderConfigurationItemViewModel.InputTypes>()
        {
            { typeof(string), ProviderConfigurationItemViewModel.InputTypes.Text },
            { typeof(string[]), ProviderConfigurationItemViewModel.InputTypes.TextArray },
            { typeof(int), ProviderConfigurationItemViewModel.InputTypes.Integer },
            { typeof(bool), ProviderConfigurationItemViewModel.InputTypes.Boolean },
            { typeof(Enum), ProviderConfigurationItemViewModel.InputTypes.Enumeration }
        };
        private static IDictionary<Type, Func<object, PropertyInfo, string>> ValueReaders { get; } = new Dictionary<Type, Func<object, PropertyInfo, string>>()
        {
            { typeof(string), (instance, property) => property.GetValue(instance) as string },
            { typeof(string[]), (instance, property) => property.GetValue(instance) == null ? string.Empty : string.Join(",", property.GetValue(instance) as string[]) },
            { typeof(int), (instance, property) => (property.GetValue(instance) as int?).GetValueOrDefault(0).ToString() },
            { typeof(bool), (instance, property) => (property.GetValue(instance) as bool?).GetValueOrDefault(false).ToString() },
            { typeof(Enum), (instance, property) => (property.GetValue(instance) as Enum).ToString() }
        };

        public static void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<Func<LoadedProviderMetadata, LoadedProviderViewModel>>(serviceProvider => provider => GetViewModel(serviceProvider, provider));
            serviceCollection.AddTransient<Func<ManagedSecret, ManagedSecretViewModel>>(serviceProvider => secret => GetViewModel(serviceProvider, secret, CancellationToken.None));
            serviceCollection.AddTransient<Func<Resource, ResourceViewModel>>(serviceProvider => resource => GetViewModel(serviceProvider, resource));
            serviceCollection.AddTransient<Func<RekeyingTask, RekeyingTaskViewModel>>(serviceProvider => rekeyingTask => GetViewModel(serviceProvider, rekeyingTask, CancellationToken.None));
            serviceCollection.AddTransient<Func<ScheduleWindow, ScheduleWindowViewModel>>(serviceProvider => scheduleWindow => GetViewModel(serviceProvider, scheduleWindow));
            serviceCollection.AddTransient<Func<AuthJanitorProviderConfiguration, ProviderConfigurationViewModel>>(serviceProvider => config => GetViewModel(serviceProvider, config));
            serviceCollection.AddTransient<Func<AuthJanitorAuthorizedUser, AuthJanitorAuthorizedUserViewModel>>(serviceProvider => authorizedUser => GetViewModel(serviceProvider, authorizedUser));
        }

#pragma warning disable IDE0060 // Remove unused parameter
        private static ProviderConfigurationViewModel GetViewModel(IServiceProvider serviceProvider, AuthJanitorProviderConfiguration config) =>
#pragma warning restore IDE0060 // Remove unused parameter
            new ProviderConfigurationViewModel()
            {
                ConfigurationItems = config.GetType().GetProperties()
                    .Select(property =>
                    {
                        if (!InputTypes.Any(t => t.Key.IsAssignableFrom(property.PropertyType)) ||
                            !ValueReaders.Any(v => v.Key.IsAssignableFrom(property.PropertyType)))
                            throw new NotImplementedException($"Provider Configuration includes Type '{property.PropertyType.Name}', which is not supported");

                        var inputType = InputTypes.First(t => t.Key.IsAssignableFrom(property.PropertyType)).Value;
                        var valueReader = ValueReaders.First(t => t.Key.IsAssignableFrom(property.PropertyType)).Value;

                        return new ProviderConfigurationItemViewModel()
                        {
                            Name = property.Name,
                            DisplayName = property.GetCustomAttribute<DisplayNameAttribute>() == null ?
                                          property.Name :
                                          property.GetCustomAttribute<DisplayNameAttribute>().DisplayName,
                            HelpText = property.GetCustomAttribute<DescriptionAttribute>() == null ?
                                          string.Empty :
                                          property.GetCustomAttribute<DescriptionAttribute>().Description,
                            InputType = inputType,
                            Options = inputType == ProviderConfigurationItemViewModel.InputTypes.Enumeration ?
                                      property.PropertyType.GetEnumValues().Cast<Enum>()
                                              .ToDictionary(
                                                    k => k.ToString(),
                                                    v => v.GetEnumValueAttribute<DescriptionAttribute>() == null ?
                                                         v.ToString() :
                                                         v.GetEnumValueAttribute<DescriptionAttribute>().Description)
                                              .Select(i => new ProviderConfigurationItemViewModel.SelectOption(i.Key, i.Value)) :
                                      new List<ProviderConfigurationItemViewModel.SelectOption>(),
                            Value = valueReader(config, property)
                        };
                    })
            };

#pragma warning disable IDE0060 // Remove unused parameter
        private static LoadedProviderViewModel GetViewModel(IServiceProvider serviceProvider, LoadedProviderMetadata provider) =>
#pragma warning restore IDE0060 // Remove unused parameter
                new LoadedProviderViewModel()
                {
                    AssemblyVersion = provider.AssemblyName.Version.ToString(),
                    Details = provider.Details,
                    OriginatingFile = Path.GetFileName(provider.OriginatingFile),
                    ProviderTypeName = provider.ProviderTypeName,
                    Capabilities = GetProviderCapabilities(provider.ProviderType)
                };

        private static IEnumerable<ProviderCapabilities> GetProviderCapabilities(Type providerType)
        {
            var capabilities = new List<ProviderCapabilities>();
            if (typeof(ICanEnumerateResourceCandidates).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanEnumerateResourceCandidates);
            if (typeof(ICanRunSanityTests).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanRunSanityTests);
            if (typeof(ICanCleanup).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanCleanup);
            if (typeof(ICanDistributeTemporarySecretValues).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanDistributeTemporarySecrets);
            if (typeof(ICanGenerateTemporarySecretValue).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanGenerateTemporarySecrets);
            if (typeof(ICanPerformUnifiedCommit).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanPerformUnifiedCommits);
            if (typeof(ICanPerformUnifiedCommitForTemporarySecretValues).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanPerformUnifiedCommitForTemporarySecret);
            if (typeof(ICanCleanup).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanCleanup);
            if (typeof(ICanRekey).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanRekey);
            if (typeof(ICanDistributeLongTermSecretValues).IsAssignableFrom(providerType))
                capabilities.Add(ProviderCapabilities.CanDistributeLongTermSecrets);
            return capabilities;
        }

        private static ManagedSecretViewModel GetViewModel(IServiceProvider serviceProvider, ManagedSecret secret, CancellationToken cancellationToken)
        {
            var providerManagerService = serviceProvider.GetRequiredService<ProviderManagerService>();
            var resources = secret.ResourceIds
                                .Select(resourceId => serviceProvider.GetRequiredService<IDataStore<Resource>>()
                                                                    .GetOne(resourceId, cancellationToken).Result)
                                .Select(resource => serviceProvider.GetRequiredService<Func<Resource, ResourceViewModel>>()(resource));
            foreach (var resource in resources)
            {
                var provider = providerManagerService.GetProviderInstance(resource.ProviderType, resource.SerializedProviderConfiguration);
                resource.Risks = provider.GetRisks(secret.ValidPeriod);
                resource.Description = provider.GetDescription();
            }
            return new ManagedSecretViewModel()
            {
                ObjectId = secret.ObjectId,
                Name = secret.Name,
                Description = secret.Description,
                TaskConfirmationStrategies = secret.TaskConfirmationStrategies,
                ExecutingAgentId = secret.ExecutingAgentId,
                LastChanged = secret.LastChanged,
                ValidPeriodMinutes = (int)secret.ValidPeriod.TotalMinutes,
                Nonce = secret.Nonce,
                Resources = resources,
                AdminEmails = secret.AdminEmails
            };
        }

        private static RekeyingTaskViewModel GetViewModel(IServiceProvider serviceProvider, RekeyingTask rekeyingTask, CancellationToken cancellationToken)
        {
            ManagedSecretViewModel secret;
            try
            {
                secret = serviceProvider.GetRequiredService<Func<ManagedSecret, ManagedSecretViewModel>>()(
                             serviceProvider.GetRequiredService<IDataStore<ManagedSecret>>().GetOne(rekeyingTask.ManagedSecretId, cancellationToken).Result);
            }
            catch (Exception) { secret = new ManagedSecretViewModel() { ObjectId = Guid.Empty }; }
            string errorMessage = string.Empty;
            var mostRecentAttempt = rekeyingTask?.Attempts.OrderByDescending(a => a.StartedExecution).FirstOrDefault();

            if (mostRecentAttempt != null)
                errorMessage = mostRecentAttempt.HasBeenExecutedSuccessfully ?
                                 string.Empty : mostRecentAttempt.GetLastException();

            return new RekeyingTaskViewModel()
            {
                ObjectId = rekeyingTask.ObjectId,
                Queued = rekeyingTask.Queued,
                Expiry = rekeyingTask.Expiry,
                PersistedCredentialUser = rekeyingTask.PersistedCredentialUser,
                ConfirmationType = rekeyingTask.ConfirmationType,
                RekeyingCompleted = rekeyingTask.RekeyingCompleted,
                RekeyingErrorMessage = errorMessage,
                RekeyingInProgress = rekeyingTask.RekeyingInProgress,
                ManagedSecret = secret,
                Attempts = rekeyingTask.Attempts
            };
        }

        private static ResourceViewModel GetViewModel(IServiceProvider serviceProvider, Resource resource)
        {
            var providerManagerService = serviceProvider.GetRequiredService<ProviderManagerService>();
            var provider = providerManagerService.GetProviderInstance(resource.ProviderType, resource.ProviderConfiguration);

            return new ResourceViewModel()
            {
                ObjectId = resource.ObjectId,
                Name = resource.Name,
                Description = resource.Description,
                ProviderType = resource.ProviderType,
                Provider = GetViewModel(serviceProvider, providerManagerService.GetProviderMetadata(resource.ProviderType)),
                SerializedProviderConfiguration = resource.ProviderConfiguration,
                RuntimeDescription = provider.GetDescription(),
                Risks = provider.GetRisks()
            };
        }

#pragma warning disable IDE0060 // Remove unused parameter
        private static ScheduleWindowViewModel GetViewModel(IServiceProvider serviceProvider, ScheduleWindow scheduleWindow) =>
#pragma warning restore IDE0060 // Remove unused parameter
            new ScheduleWindowViewModel()
            {
                ObjectId = scheduleWindow.ObjectId,
                CronStrings = new List<string>(scheduleWindow.CronStrings)
            };

        private static AuthJanitorAuthorizedUserViewModel GetViewModel(IServiceProvider serviceProvider, AuthJanitorAuthorizedUser authorizedUser) =>
                new AuthJanitorAuthorizedUserViewModel()
                {
                    UPN = authorizedUser.UPN,
                    DisplayName = authorizedUser.DisplayName,
                    RoleValue = authorizedUser.RoleValue
                };
    }
}
