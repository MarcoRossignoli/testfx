// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Net;

using Newtonsoft.Json;

namespace Microsoft.Testing.Client;

internal sealed class HttpServer : IDisposable
{
    private readonly byte[] _emptyResponse = System.Text.Encoding.UTF8.GetBytes("{}");
    private readonly TestingApplication _testingApplication;
    private readonly CancellationTokenSource _stopListener = new();
    private readonly string[]? _idFilter;
    private HttpListener? _listener;
    private Task? _connectionLoop;

    public event EventHandler<OnMessageEventArgs>? OnMessage;

    public HttpServer(TestingApplication testingApplication, string[]? idFilter = null)
    {
        _testingApplication = testingApplication;
        _idFilter = idFilter;
    }

    public void Dispose()
    {
        _stopListener.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _connectionLoop?.Wait();
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

            _testingApplication.LogMessage($"Listening on {_listener!.Prefixes.Single()} for test app {_testingApplication.Id}");

            try
            {
                while (!_stopListener.IsCancellationRequested)
                {
                    connectionLoopStarted.Set();
                    Debug.Assert(_listener != null, "Unexpected null _listener");
                    HttpListenerContext httpContext = await _listener!.GetContextAsync().ConfigureAwait(false);
                    _ = HandleMessageAsync(httpContext);
                }
            }
            catch (HttpListenerException)
            {
                // nothing to do here -- the listener disposes itself when Stop is called
            }
        });

        connectionLoopStarted.Wait();
    }

    public string? GetHostName() => _listener?.Prefixes.SingleOrDefault();

    public static bool TryBindListenerOnFreePort(out HttpListener? httpListener, out int port)
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

    private async Task HandleMessageAsync(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        using StreamReader reader = new(request.InputStream);
        string requestContent = await reader.ReadToEndAsync().ConfigureAwait(false);
        if (_testingApplication.EnableMessagesLogging)
        {
            _testingApplication.LogMessage($"Received message: {requestContent}");
        }

        string? action = null;

        action = request.Url!.PathAndQuery switch
        {
            "/list-tests" => "list-tests",
            "/run-tests" => "run-tests",
            "/getFilters" => "getFilters",
            _ => throw new InvalidOperationException("Unsupported action"),
        };

        OnMessage?.Invoke(this, new OnMessageEventArgs(requestContent, action!));

        if (action == "getFilters")
        {
            string filters = JsonConvert.SerializeObject(Array.Empty<string>());
            if (_idFilter is not null)
            {
                filters = JsonConvert.SerializeObject(_idFilter)!;
            }

            byte[] filtersBytes = System.Text.Encoding.UTF8.GetBytes(filters);
            HttpListenerResponse response = context.Response;
            response.ContentLength64 = filtersBytes.Length;
            Stream output = response.OutputStream;
#if NETCOREAPP
            await output.WriteAsync(filtersBytes).ConfigureAwait(false);
#else
            await output.WriteAsync(filtersBytes, 0, filtersBytes.Length).ConfigureAwait(false);
#endif
            output.Close();
        }
        else
        {
            HttpListenerResponse response = context.Response;
            response.ContentLength64 = _emptyResponse.Length;
            Stream output = response.OutputStream;
#if NETCOREAPP
            await output.WriteAsync(_emptyResponse).ConfigureAwait(false);
#else
            await output.WriteAsync(_emptyResponse, 0, _emptyResponse.Length).ConfigureAwait(false);
#endif
            output.Close();
        }
    }
}

public class OnMessageEventArgs : EventArgs
{
    public string Message { get; }

    public string Action { get; }

    public OnMessageEventArgs(string message, string action)
    {
        Message = message;
        Action = action;
    }
}
