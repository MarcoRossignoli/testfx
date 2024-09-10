﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Client;

public class EntryPoint
{
    public static async Task Main(string[] args)
    {
        var testApp = new TestingApplication(@"C:\git\testfx\artifacts\bin\Playground\Debug\net8.0\Playground.exe")
        {
            EnableLogging = true,
            EnableMessagesLogging = false,
        };
        testApp.LogReceived += (sender, e) => Console.WriteLine(e.Log);
        testApp.LogMessageReceived += (sender, e) => Console.WriteLine(e.Message);

        // Discovery
        DiscoveryRequest discoveryRequest = testApp.CreateDiscoveryRequest();
        discoveryRequest.DiscoveredTests += (sender, e) =>
        {
            foreach (DiscoveredNode node in e.DiscoveredNodes)
            {
                Console.WriteLine($"Discovered node - {node.node.uid} {node.node.displayname}");
            }
        };

        // Run
        discoveryRequest.Execute();

        // Wait for completion
        await discoveryRequest.WaitCompletionAsync().ConfigureAwait(false);
    }
}