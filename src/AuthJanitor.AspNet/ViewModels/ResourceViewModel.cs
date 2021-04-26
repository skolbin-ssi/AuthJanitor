﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace AuthJanitor.UI.Shared.ViewModels
{
    public class ResourceViewModel : IAuthJanitorViewModel
    {
        public Guid ObjectId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public string ProviderType { get; set; }
        public LoadedProviderViewModel Provider { get; set; }
        public string SerializedProviderConfiguration { get; set; }
        public IEnumerable<RiskyConfigurationItem> Risks { get; set; } = new List<RiskyConfigurationItem>();
        public string RuntimeDescription { get; set; }
        public int RiskScore => Risks.Sum(r => r.Score);

        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public ProviderConfigurationViewModel ProviderConfiguration { get; set; } = new ProviderConfigurationViewModel();
    }
}
