﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.ComponentModel;

namespace AuthJanitor.Providers.Azure
{
    public class AzureAuthJanitorProviderConfiguration : AuthJanitorProviderConfiguration
    {
        [DisplayName("Resource Name")]
        public string ResourceName { get; set; }

        [DisplayName("Resource Group Name")]
        public string ResourceGroup { get; set; }

        [DisplayName("Subscription ID")]
        [Description("If this is left blank, the default subscription will be used.")]
        public string SubscriptionId { get; set; }

        public override int GenerateResourceIdentifierHashCode() =>
            HashCode.Combine(SubscriptionId, ResourceGroup, ResourceName);
    }
}
