// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable IDE0052 // Remove unread private members

using System.Diagnostics;

using Newtonsoft.Json;

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

public class DiscoveredNode
{
    [JsonProperty("node")]
    public Node node { get; set; }

    [JsonProperty("parent")]
    public object parent { get; set; }
}

public class Node
{
    [JsonProperty("uid")]
    public string uid { get; set; }

    [JsonProperty("display-name")]
    public string displayname { get; set; }

    [JsonProperty("node-type")]
    public string nodetype { get; set; }

    [JsonProperty("execution-state")]
    public string executionstate { get; set; }
}
