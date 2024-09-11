﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Client;

public class TestingApplication
{
    private readonly string _executable;

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public bool EnableLogging { get; set; }

    public bool EnableMessagesLogging { get; set; }

    public event EventHandler<LogEventArgs>? LogReceived;

    public event EventHandler<LogMessageEventArgs>? LogMessageReceived;

    public TestingApplication(string executable) => _executable = executable;

    public DiscoveryRequest CreateDiscoveryRequest()
        => new(_executable, this);

    public RunRequest CreateRunRequest(string[]? idFilter = null)
        => new(_executable, idFilter, this);

    internal void Log(string message)
    {
        if (EnableLogging)
        {
            LogReceived?.Invoke(this, new LogEventArgs(message));
        }
    }

    internal void LogMessage(string message)
    {
        if (EnableMessagesLogging)
        {
            LogMessageReceived?.Invoke(this, new LogMessageEventArgs(message));
        }
    }
}
