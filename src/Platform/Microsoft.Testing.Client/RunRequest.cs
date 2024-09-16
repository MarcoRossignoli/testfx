// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;

using Newtonsoft.Json;

namespace Microsoft.Testing.Client;

public sealed class RunRequest
{
    private readonly string _executableOrProject;
    private readonly TestingApplication _testingApplication;
    private readonly TaskCompletionSource<int> _completionSource = new();
    private readonly string[]? _idFilter;
    private readonly bool _hotReload;
    private readonly ConcurrentBag<OnCancellationTokenEventArgs> _cancellationTokens = new();
    private readonly object _cancelLock = new();
    private bool _cancelled;

    public event EventHandler<RanTestsEventArgs>? RanTests;

    internal RunRequest(string executable, TestingApplication testingApplication)
    {
        _executableOrProject = executable;
        _testingApplication = testingApplication;
    }

    public RunRequest(string executableOrProject, string[]? idFilter, bool hotReload, TestingApplication testingApplication)
    {
        _executableOrProject = executableOrProject;
        _idFilter = idFilter;
        _hotReload = hotReload;
        _testingApplication = testingApplication;
    }

    public void Cancel()
    {
        lock (_cancelLock)
        {
            _cancelled = true;
            foreach (OnCancellationTokenEventArgs cancellation in _cancellationTokens)
            {
                cancellation.CancelTheRequest();
            }
        }
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

                httpServer.OnCancellationToken += (sender, e) =>
                {
                    lock (_cancelLock)
                    {
                        if (_cancelled)
                        {
                            e.CancelTheRequest();
                        }
                        else
                        {
                            _cancellationTokens.Add(e);
                        }
                    }
                };

                Process? process = null;
                int? exitCode = 0;

                httpServer.OnExit += (sender, e) =>
                {
                    foreach (OnCancellationTokenEventArgs cancellation in _cancellationTokens)
                    {
                        cancellation.CloseTheRequest();
                    }

                    exitCode = e.ExitCode;
                    if (_hotReload)
                    {
                        process!.Kill();
                    }
                };

                ProcessStartInfo processStart = GetProcessStartInfo(httpServer.GetHostName()!);
                _testingApplication.Log($"Starting {processStart.FileName} {processStart.Arguments}");
                processStart.RedirectStandardOutput = true;
                processStart.RedirectStandardError = true;
                process = Process.Start(processStart)!;
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

                _completionSource.SetResult(exitCode.Value);
            }
            catch (Exception ex)
            {
                _completionSource.SetException(ex);
            }
        });

    public Task<int> WaitCompletionAsync() => _completionSource.Task;

    private ProcessStartInfo GetProcessStartInfo(string hostName)
    {
        ProcessStartInfo psi;
        if (_hotReload)
        {
            psi = new ProcessStartInfo
            {
                FileName = @"dotnet",
#if NETCOREAPP
                Arguments = $"watch -- --server http --http-hostname {hostName} --exit-on-process-exit {Environment.ProcessId}",
#else
                Arguments = $"watch -- --server http --http-hostname {hostName} --exit-on-process-exit {Process.GetCurrentProcess()!.Id!}",
#endif
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_executableOrProject)!,
            };

            // psi.Environment["TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER"] = "1";
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = _executableOrProject,
#if NETCOREAPP
                Arguments = $"--server http --http-hostname {hostName} --exit-on-process-exit {Environment.ProcessId}",
#else
                Arguments = $"--server http --http-hostname {hostName} --exit-on-process-exit {Process.GetCurrentProcess()!.Id!}",
#endif
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // psi.Environment["TESTINGPLATFORM_LAUNCH_ATTACH_DEBUGGER"] = "1";
        }

        return psi;
    }
}
