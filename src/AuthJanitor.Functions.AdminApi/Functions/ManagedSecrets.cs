﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.Services;
using AuthJanitor.UI.Shared.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuthJanitor.Functions
{
    /// <summary>
    /// API functions to control the creation and management of AuthJanitor Managed Secrets.
    /// A Managed Secret is a grouping of Resources and Policies which describe the strategy around rekeying an object and the applications which consume it.
    /// </summary>
    public class ManagedSecrets
    {
        private readonly ManagedSecretsService _service;

        public ManagedSecrets(ManagedSecretsService service)
        {
            _service = service;
        }

        [FunctionName("ManagedSecrets-Create")]
        public async Task<IActionResult> Create([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "managedSecrets")] string secretJson, //ManagedSecretViewModel inputSecret, 
            CancellationToken cancellationToken)
        {
            var inputSecret = JsonConvert.DeserializeObject<ManagedSecretViewModel>(secretJson);
            return await _service.Create(inputSecret, cancellationToken);
        }

        [FunctionName("ManagedSecrets-List")]
        public async Task<IActionResult> List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "managedSecrets")] HttpRequest req, CancellationToken cancellationToken)
        {
            return await _service.List(req, cancellationToken);
        }

        [FunctionName("ManagedSecrets-Get")]
        public async Task<IActionResult> Get([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "managedSecrets/{secretId}")] HttpRequest req,
            string secretId, CancellationToken cancellationToken)
        {
            return await _service.Get(req, Guid.Parse(secretId), cancellationToken);
        }

        [FunctionName("ManagedSecrets-Delete")]
        public async Task<IActionResult> Delete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "managedSecrets/{secretId}")] HttpRequest req,
            string secretId, CancellationToken cancellationToken)
        {
            return await _service.Delete(req, Guid.Parse(secretId), cancellationToken);
        }

        [FunctionName("ManagedSecrets-Update")]
        public async Task<IActionResult> Update(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "managedSecrets/{secretId}")] string secretJson, //ManagedSecretViewModel inputSecret,
            string secretId, CancellationToken cancellationToken)
        {
            var inputSecret = JsonConvert.DeserializeObject<ManagedSecretViewModel>(secretJson);
            return await _service.Update(inputSecret, Guid.Parse(secretId), cancellationToken);
        }
    }
}
