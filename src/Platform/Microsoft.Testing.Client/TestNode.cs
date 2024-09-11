// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json;

namespace Microsoft.Testing.Client;

public class TestNode
{
    [JsonProperty("node")]
    public Node? Node { get; set; }

    [JsonProperty("parent")]
    public Node? Parent { get; set; }
}
