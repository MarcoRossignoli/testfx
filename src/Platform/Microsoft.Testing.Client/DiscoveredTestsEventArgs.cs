﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Client;

public class DiscoveredTestsEventArgs : EventArgs
{
    internal DiscoveredTestsEventArgs(TestNode[] discoveredNode) => DiscoveredNodes = discoveredNode;

    public TestNode[] DiscoveredNodes { get; }
}
