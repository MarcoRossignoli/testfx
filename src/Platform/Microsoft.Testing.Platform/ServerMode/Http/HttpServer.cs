// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.ServerMode;

namespace Microsoft.Testing.Platform;

internal class HttpServer : IPushOnlyProtocol
{
    private readonly ICommandLineOptions _commandLineOptions;

    internal HttpClient? _httpClient;

    private string? _httpHost;

    public HttpServer(ICommandLineOptions commandLineOptions) => _commandLineOptions = commandLineOptions;

    public string Name => "http";

    public bool IsServerMode => _commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.ServerOptionKey, out string[]? serverOptions) &&
                                serverOptions.Contains(Name, StringComparer.OrdinalIgnoreCase) &&
                                _commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.HttpHostOptionKey, out string[]? httpHost) &&
                                httpHost?.Length == 1
                                ;

    public Task AfterCommonServiceSetupAsync()
    {
        if (_commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.HttpHostOptionKey, out string[]? httpHost) &&
                                httpHost?.Length == 1)
        {
            _httpClient = new HttpClient();
            _httpHost = httpHost[0];
        }

        return Task.CompletedTask;
    }

    public Task<IPushOnlyProtocolConsumer> GetDataConsumerAsync()
        => Task.FromResult((IPushOnlyProtocolConsumer)new HttpServerConsumer(this, new Uri(_httpHost!)));

    public Task<bool> IsCompatibleProtocolAsync(string testHostType)
        => Task.FromResult(true);

    public Task HelpInvokedAsync() => throw new NotImplementedException();

#if NETCOREAPP
    public ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        return ValueTask.CompletedTask;
    }
#endif

    public void Dispose() => _httpClient?.Dispose();

}
