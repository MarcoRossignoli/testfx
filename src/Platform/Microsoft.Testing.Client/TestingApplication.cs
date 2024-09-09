// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace Microsoft.Testing.Client;

public class TestingApplication
{
    private readonly string _executable;

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public bool EnableLogging { get; set; }

    public bool EnableMessagesLogging { get; set; }

    public event EventHandler<LogEventArgs>? LogReceived;

    public event EventHandler<LogMessageEventArgs>? LogMessageReceived;

    public TestingApplication(string executable) => _executable = executable;

    public DiscoveryRequest CreateDiscoveryRequest()
        => new(_executable, this);

    internal void Log(string message)
    {
        if (EnableLogging)
        {
            LogReceived?.Invoke(this, new LogEventArgs(message));
        }
    }

    internal void LogMessage(string message)
    {
        if (EnableMessagesLogging)
        {
            LogMessageReceived?.Invoke(this, new LogMessageEventArgs(message));
        }
    }
}

public sealed class DiscoveryRequest : IAsyncDisposable
{
    private readonly string _executable;
    private readonly TestingApplication _testingApplication;
    private readonly TaskCompletionSource<int> _completionSource = new();
    private readonly HttpServer _httpServer;
    private bool _isDisposed;

    public event EventHandler<DiscoveredTestsEventArgs>? DiscoveredTests;

    internal DiscoveryRequest(string executable, TestingApplication testingApplication)
    {
        _executable = executable;
        _testingApplication = testingApplication;
        _httpServer = new HttpServer(testingApplication);
    }

    public void Execute() =>
        _ = Task.Run(async () =>
        {
            try
            {
                // Start the server
                _httpServer.StartListening();
                var processStart = GetProcessStartInfo(_httpServer.GetHostName());
                Process? process = Process.Start(processStart);
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                _completionSource.SetException(ex);
            }
        });

    public Task<int> WaitCompletionAsync() => _completionSource.Task;


    ProcessStartInfo GetProcessStartInfo(string hostName)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _executable,
            Arguments = $"--list-tests --server http --http-hostname {hostName}",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return psi;

    public async ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            await _httpServer.DisposeAsync();
        }
    }
}
