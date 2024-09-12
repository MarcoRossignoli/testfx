// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace Microsoft.Testing.Client;

public class Node
{
    [JsonProperty("uid")]
    public string? Uid { get; set; }

    [JsonProperty("display-name")]
    public string? DisplayName { get; set; }

    [JsonProperty("node-type")]
    public string? NodeType { get; set; }

    [JsonProperty("execution-state")]
    public string? ExecutionState { get; set; }

    [JsonProperty("time.startutc")]
    public DateTime TimestartUtc { get; set; }

    [JsonProperty("time.stoputc")]
    public DateTime TimeStopUtc { get; set; }

    [JsonProperty("error.message")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("error.stacktrace")]
    public string? ErrorStacktrace { get; set; }

    [JsonProperty("assert.actual")]
    public string? Assertactual { get; set; }

    [JsonProperty("assert.expected")]
    public string? Assertexpected { get; set; }

    [JsonProperty("time.durationms")]
    public float TimeDurationMs { get; set; }
}
