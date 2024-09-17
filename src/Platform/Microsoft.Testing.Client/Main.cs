// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Client;

internal sealed class EntryPoint
{
#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods
    public static async Task Main(string[] _)
#pragma warning restore VSTHRD200 // Use "Async" suffix for async methods
    {
        var testApp = new TestingApplication(@"C:\git\localPlayground\Contoso.Tests\bin\Debug\net8.0\Contoso.Tests.exe")
        {
            EnableLogging = true,
            EnableMessagesLogging = false,
        };
        testApp.LogReceived += (sender, e) => Console.WriteLine(e.Log);
        testApp.LogMessageReceived += (sender, e) => Console.WriteLine(e.Message);

        // ============ Discovery ============
        Console.WriteLine("============ Discovery ============");
        DiscoveryRequest discoveryRequest = testApp.CreateDiscoveryRequest();
        var testId = new List<string>();
        discoveryRequest.DiscoveredTests += (sender, e) =>
        {
            foreach (TestNode node in e.DiscoveredNodes)
            {
                testId.Add(node.Node!.Uid!);
                Console.WriteLine($"Discovered node - {node.Node!.Uid} {node.Node!.DisplayName}");
            }
        };
        // Execute
        discoveryRequest.Execute();
        // Wait for completion
        Console.WriteLine($"ExitCode: {await discoveryRequest.WaitCompletionAsync().ConfigureAwait(false)}");
    }
}
