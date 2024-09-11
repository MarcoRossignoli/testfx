// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
using System.Text.Json;
#endif

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform;

internal class HttpServer : IPushOnlyProtocol
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly ServiceProvider _serviceProvider;
    private HttpClient? _httpClient;
    private string? _httpHost;

    public HttpServer(ICommandLineOptions commandLineOptions, ServiceProvider serviceProvider)
    {
        _commandLineOptions = commandLineOptions;
        _serviceProvider = serviceProvider;
    }

    public string Name => "http";

    public bool IsServerMode => _commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.ServerOptionKey, out string[]? serverOptions) &&
                                serverOptions.Contains(Name, StringComparer.OrdinalIgnoreCase) &&
                                _commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.HttpHostOptionKey, out string[]? httpHost) &&
                                httpHost?.Length == 1;

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
        => Task.FromResult((IPushOnlyProtocolConsumer)new HttpServerConsumer(this, new Uri(_httpHost!), _httpClient, _commandLineOptions));

    public async Task<bool> IsCompatibleProtocolAsync(string testHostType)
    {
        if (testHostType == "TestHost")
        {
            string filterId = await _httpClient!.GetStringAsync(new Uri(new Uri(_httpHost!), "getFilters"));
#if NETCOREAPP
            string[] idFilter = JsonDocument.Parse(filterId).RootElement.EnumerateArray().Select(e => e.GetString()).ToArray()!;
#else
            string[] idFilter = (string[])Jsonite.Json.Deserialize(filterId, null!);
#endif

            if (idFilter.Length > 0)
            {
                _serviceProvider.AddService(new HttpProtocolTestExecutionFilterFactory(idFilter));
            }
        }

        return true;
    }

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

internal class HttpProtocolTestExecutionFilterFactory : ITestExecutionFilterFactory
{
    private readonly string[] _idFilters;

    public HttpProtocolTestExecutionFilterFactory(string[] idFilters) => _idFilters = idFilters;

    public string Uid => nameof(HttpProtocolTestExecutionFilterFactory);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(HttpProtocolTestExecutionFilterFactory);

    public string Description => nameof(HttpProtocolTestExecutionFilterFactory);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<(bool Success, ITestExecutionFilter? TestExecutionFilter)> TryCreateAsync()
        => Task.FromResult(((bool, ITestExecutionFilter?))(true, new TestNodeUidListFilter(_idFilters.Select(x => new TestNodeUid(x)).ToArray())));
}
