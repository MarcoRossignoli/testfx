// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Client;

public class RanTestsEventArgs : EventArgs
{
    internal RanTestsEventArgs(TestNode[] ranNode) => RanNodes = ranNode;

    public TestNode[] RanNodes { get; }
}
