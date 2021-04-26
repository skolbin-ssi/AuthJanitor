﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using AuthJanitor.EventSinks;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AuthJanitor.EventSinks
{
    public interface IEventSink : IAuthJanitorExtensibilityPoint
    {
        Task LogEvent(LogLevel logLevel, string source, string eventMessage);
        Task LogEvent(AuthJanitorSystemEvents systemEvent, string source, string details);
        Task LogEvent<T>(AuthJanitorSystemEvents systemEvent, string source, T detailObject);
    }
}
