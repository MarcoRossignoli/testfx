// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using Newtonsoft.Json;

namespace Microsoft.Testing.Client;

public sealed class RunRequest
{
    private readonly string _executable;
    private readonly TestingApplication _testingApplication;
    private readonly TaskCompletionSource<int> _completionSource = new();
    private string[]? _idFilter;

    public event EventHandler<RanTestsEventArgs>? RanTests;

    internal RunRequest(string executable, TestingApplication testingApplication)
    {
        _executable = executable;
        _testingApplication = testingApplication;
    }

    public RunRequest(string executable, string[]? idFilter, TestingApplication testingApplication)
    {
        _executable = executable;
        _idFilter = idFilter;
        _testingApplication = testingApplication;
    }

    public void Execute() =>
        _ = Task.Run(async () =>
        {
            try
            {
                // Start the server
                var httpServer = new HttpServer(_testingApplication, _idFilter);
                httpServer.StartListening();
                httpServer.OnMessage += (sender, e) =>
                {
                    if (e.Action != "run-tests")
                    {
                        return;
                    }

                    TestNode discoveredNode = JsonConvert.DeserializeObject<TestNode>(e.Message)!;
                    RanTests?.Invoke(this, new RanTestsEventArgs(new[] { discoveredNode }));
                };

                ProcessStartInfo processStart = GetProcessStartInfo(httpServer.GetHostName()!);
                _testingApplication.Log($"Starting {processStart.FileName} {processStart.Arguments}");
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
            Arguments = $"--server http --http-hostname {hostName} --exit-on-process-exit {Process.GetCurrentProcess()!.Id!}",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return psi;
    }
}
