// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Client;

public class EntryPoint
{
    public static async Task Main(string[] args)
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

        // ============ Run ============
        Console.WriteLine("============ Run ============");
        RunRequest runRequest = testApp.CreateRunRequest();
        runRequest.RanTests += (sender, e) =>
        {
            foreach (TestNode node in e.RanNodes)
            {
                Console.WriteLine($"Ran node - {node.Node!.Uid} {node.Node!.DisplayName} {node.Node!.ExecutionState}");
                if (node.Node!.ExecutionState == "failed")
                {
                    Console.WriteLine($"Error message: {node.Node!.ErrorMessage}\n{node.Node!.ErrorStacktrace}");
                }
            }
        };
        // Execute
        runRequest.Execute();
        // Wait for completion
        Console.WriteLine($"ExitCode: {await runRequest.WaitCompletionAsync().ConfigureAwait(false)}");

        // ============ Run filtered ============
        Console.WriteLine("============ Run filtered ============");
        RunRequest runFilteredRequest = testApp.CreateRunRequest(testId.Take(1).ToArray());
        runFilteredRequest.RanTests += (sender, e) =>
        {
            foreach (TestNode node in e.RanNodes)
            {
                Console.WriteLine($"Ran node - {node.Node!.Uid} {node.Node!.DisplayName} {node.Node!.ExecutionState}");
                if (node.Node!.ExecutionState == "failed")
                {
                    Console.WriteLine($"Error message: {node.Node!.ErrorMessage}\n{node.Node!.ErrorStacktrace}");
                }
            }
        };
        // Execute
        runFilteredRequest.Execute();
        // Wait for completion
        Console.WriteLine($"ExitCode: {await runFilteredRequest.WaitCompletionAsync().ConfigureAwait(false)}");

        // ============ Run and cancel ============
        Console.WriteLine("============ Run and cancel ============");
        RunRequest cancelRequest = testApp.CreateRunRequest();
        cancelRequest.RanTests += (sender, e) =>
        {
            foreach (TestNode node in e.RanNodes)
            {
                Console.WriteLine($"Ran node - {node.Node!.Uid} {node.Node!.DisplayName} {node.Node!.ExecutionState}");
                if (node.Node!.ExecutionState == "failed")
                {
                    Console.WriteLine($"Error message: {node.Node!.ErrorMessage}\n{node.Node!.ErrorStacktrace}");
                }
            }
        };
        // Execute
        cancelRequest.Execute();
        Thread.Sleep(1000);
        cancelRequest.Cancel();
        // Wait for completion
        Console.WriteLine($"ExitCode: {await cancelRequest.WaitCompletionAsync().ConfigureAwait(false)}");

        // ============ Hot Reload - dotnet watch ============
        Console.WriteLine("============ Hot Reload - dotnet watch ============");

        testApp = new TestingApplication(@"C:\git\localPlayground\Contoso.Tests\Contoso.Tests.csproj")
        {
            EnableLogging = false,
            EnableMessagesLogging = false,
        };
        testApp.LogReceived += (sender, e) => Console.WriteLine(e.Log);
        testApp.LogMessageReceived += (sender, e) => Console.WriteLine(e.Message);

        RunRequest runHotReloadRequest = testApp.CreateRunRequest(hotReload: true);
        runHotReloadRequest.RanTests += (sender, e) =>
        {
            foreach (TestNode node in e.RanNodes)
            {
                Console.WriteLine($"Ran node - {node.Node!.Uid} {node.Node!.DisplayName} {node.Node!.ExecutionState}");
                if (node.Node!.ExecutionState == "failed")
                {
                    Console.WriteLine($"Error message: {node.Node!.ErrorMessage}\n{node.Node!.ErrorStacktrace}");
                }
            }
        };
        // Execute
        runHotReloadRequest.Execute();

        // Wait for completion
        Console.WriteLine($"ExitCode: {await runHotReloadRequest.WaitCompletionAsync().ConfigureAwait(false)}");
    }
}
