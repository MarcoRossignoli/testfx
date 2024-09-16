// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Web;

using Newtonsoft.Json;

namespace Microsoft.Testing.Client;

internal sealed class HttpServer : IDisposable
{
    private static readonly byte[] EmptyResponse = System.Text.Encoding.UTF8.GetBytes("{}");
    private readonly TestingApplication _testingApplication;
    private readonly CancellationTokenSource _stopListener = new();
    private readonly string[]? _idFilter;
    private HttpListener? _listener;
    private Task? _connectionLoop;
    private bool _disposed;

    public event EventHandler<OnMessageEventArgs>? OnMessage;

    public event EventHandler<OnCancellationTokenEventArgs>? OnCancellationToken;

    public event EventHandler<OnExitEventArgs>? OnExit;

    public HttpServer(TestingApplication testingApplication, string[]? idFilter = null)
    {
        _testingApplication = testingApplication;
        _idFilter = idFilter;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _stopListener.Cancel();
            _listener?.Stop();
            _listener?.Close();
            _connectionLoop?.Wait();
        }
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
            catch (ObjectDisposedException)
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

        action = request.Url!.AbsolutePath switch
        {
            "/list-tests" => "list-tests",
            "/run-tests" => "run-tests",
            "/getFilters" => "getFilters",
            "/cancellationToken" => "cancellationToken",
            "/exit" => "exit",
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
            HttpListenerResponse filterResponse = context.Response;
            filterResponse.ContentLength64 = filtersBytes.Length;
            Stream filterOutput = filterResponse.OutputStream;
#if NETCOREAPP
            await filterOutput.WriteAsync(filtersBytes).ConfigureAwait(false);
#else
            await filterOutput.WriteAsync(filtersBytes, 0, filtersBytes.Length).ConfigureAwait(false);
#endif
            filterOutput.Close();
            return;
        }
        else if (action == "cancellationToken")
        {
            OnCancellationToken?.Invoke(this, new(context));
            return;
        }
        else if (action == "exit")
        {
            OnExit?.Invoke(this, new(int.Parse(request.QueryString["exitCode"]!, CultureInfo.InvariantCulture)));
        }

        HttpListenerResponse response = context.Response;
        response.ContentLength64 = EmptyResponse.Length;
        Stream output = response.OutputStream;
#if NETCOREAPP
        await output.WriteAsync(EmptyResponse).ConfigureAwait(false);
#else
        await output.WriteAsync(EmptyResponse, 0, EmptyResponse.Length).ConfigureAwait(false);
#endif
        output.Close();
    }
}

public sealed class OnExitEventArgs : EventArgs
{
    public OnExitEventArgs(int exitCode) => ExitCode = exitCode;

    public int ExitCode { get; }
}

public sealed class OnMessageEventArgs : EventArgs
{
    public string Message { get; }

    public string Action { get; }

    public OnMessageEventArgs(string message, string action)
    {
        Message = message;
        Action = action;
    }
}

public sealed class OnCancellationTokenEventArgs : EventArgs
{
    private readonly object _contextConsumed = new();
    private static readonly byte[] EmptyResponse = System.Text.Encoding.UTF8.GetBytes("{}");
    private static readonly byte[] CancelResponse = System.Text.Encoding.UTF8.GetBytes("cancel");
    private readonly HttpListenerContext _context;
    private bool _consumed;

    internal OnCancellationTokenEventArgs(HttpListenerContext context)
        => _context = context;

    public void CancelTheRequest()
    {
        if (_consumed)
        {
            return;
        }

        lock (_contextConsumed)
        {
            if (_consumed)
            {
                return;
            }

            HttpListenerResponse response = _context.Response;
            response.ContentLength64 = CancelResponse.Length;
            Stream output = response.OutputStream;
#if NETCOREAPP
            output.Write(CancelResponse);
#else
            output.Write(CancelResponse, 0, CancelResponse.Length);
#endif
            output.Close();
            _consumed = true;
        }
    }

    public void CloseTheRequest()
    {
        if (_consumed)
        {
            return;
        }

        lock (_contextConsumed)
        {
            if (_consumed)
            {
                return;
            }

            HttpListenerResponse response = _context.Response;
            response.ContentLength64 = EmptyResponse.Length;
            Stream output = response.OutputStream;
#if NETCOREAPP
            output.Write(EmptyResponse);
#else
            output.Write(EmptyResponse, 0, EmptyResponse.Length);
#endif
            output.Close();
            _consumed = true;
        }
    }
}
