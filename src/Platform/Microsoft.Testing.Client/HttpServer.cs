// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Microsoft.Testing.Client;

internal class HttpServer : IAsyncDisposable
{
    private HttpListener? _listener;
    private readonly TestingApplication _testingApplication;
    private Task? _connectionLoop;

    public HttpServer(TestingApplication testingApplication) => _testingApplication = testingApplication;

    public async ValueTask DisposeAsync()
    {
        if (_connectionLoop != null)
        {
            await _connectionLoop.WaitAsync(CancellationToken.None);
        }

        _listener?.Stop();
        _listener?.Close();
    }

    public void StartListening()
    {
        using ManualResetEventSlim connectionLoopStarted = new(false);

        _connectionLoop = Task.Run(async () =>
        {
            int port = 0;
            while (!TryBindListenerOnFreePort(out _listener, out port))
            {
                _testingApplication.LogMessage($"Failed to bind listener on a free port for test app {_testingApplication.Id}. Retrying...");
            }

            _testingApplication.LogMessage($"Listening on {_listener.Prefixes.Single()} for test app {_testingApplication.Id}");

            while (true)
            {
                connectionLoopStarted.Set();
                Debug.Assert(_listener != null, "Unexpected null _listener");
                HttpListenerContext httpContext = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = HandleMessageAsync(httpContext);
            }
        });

        connectionLoopStarted.Wait();
    }

    public string? GetHostName() => _listener?.Prefixes.SingleOrDefault();

    public static bool TryBindListenerOnFreePort([NotNullWhen(true)] out HttpListener? httpListener, out int port)
    {
        // IANA suggested range for dynamic or private ports
        const int MinPort = 49215;
        const int MaxPort = 65535;

        for (port = MinPort; port < MaxPort; port++)
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://localhost:{port}/");
            try
            {
                httpListener.Start();
                return true;
            }
            catch
            {
                // nothing to do here -- the listener disposes itself when Start throws
            }
        }

        port = 0;
        httpListener = null;
        return false;
    }

    ValueTask IAsyncDisposable.DisposeAsync() => throw new NotImplementedException();

    private Task HandleMessageAsync(HttpListenerContext client)
    {
        return Task.CompletedTask;
    }
}
