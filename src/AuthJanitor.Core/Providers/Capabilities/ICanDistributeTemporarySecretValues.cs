// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthJanitor.Providers.Capabilities
{
    public interface ICanDistributeTemporarySecretValues : IAuthJanitorCapability
    {
        Task DistributeTemporarySecretValues(List<RegeneratedSecret> secretValues);
    }
}