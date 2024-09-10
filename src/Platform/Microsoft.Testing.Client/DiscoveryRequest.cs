// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Newtonsoft.Json;

namespace Microsoft.Testing.Client;

public sealed class DiscoveryRequest
{
    private readonly string _executable;
    private readonly TestingApplication _testingApplication;
    private readonly TaskCompletionSource<int> _completionSource = new();

    public event EventHandler<DiscoveredTestsEventArgs>? DiscoveredTests;

    internal DiscoveryRequest(string executable, TestingApplication testingApplication)
    {
        _executable = executable;
        _testingApplication = testingApplication;
    }

    public void Execute() =>
        _ = Task.Run(async () =>
        {
            try
            {
                // Start the server
                var httpServer = new HttpServer(_testingApplication);
                httpServer.StartListening();
                httpServer.OnMessage += (sender, e) =>
                {
                    DiscoveredNode discoveredNode = JsonConvert.DeserializeObject<DiscoveredNode>(e.Message)!;
                    DiscoveredTests?.Invoke(this, new DiscoveredTestsEventArgs(new[] { discoveredNode }));
                };

                ProcessStartInfo processStart = GetProcessStartInfo(httpServer.GetHostName()!);
                processStart.RedirectStandardOutput = true;
                processStart.RedirectStandardError = true;
                var process = Process.Start(processStart);
                process!.BeginErrorReadLine();
                process!.BeginOutputReadLine();

                process!.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        _testingApplication.LogMessage(e.Data);
                    }
                };
                process!.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        _testingApplication.LogMessage(e.Data);
                    }
                };

#if NETCOREAPP
                await process!.WaitForExitAsync().ConfigureAwait(false);
#else
                process!.WaitForExit();
                await Task.CompletedTask;
#endif

                // Dispose the server
                httpServer.Dispose();

                _completionSource.SetResult(process.ExitCode);
            }
            catch (Exception ex)
            {
                _completionSource.SetException(ex);
            }
        });

    public Task<int> WaitCompletionAsync() => _completionSource.Task;

    private ProcessStartInfo GetProcessStartInfo(string hostName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _executable,
            Arguments = $"--list-tests --server http --http-hostname {hostName} --exit-on-process-exit {Process.GetCurrentProcess()!.Id!}",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return psi;
    }
}
