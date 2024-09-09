// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Client;

public sealed class LogEventArgs : EventArgs
{
    public string Log { get; }

    internal LogEventArgs(string log) => Log = log;
}
