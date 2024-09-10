// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform;

internal class HttpServer : IPushOnlyProtocol
{
    private readonly ICommandLineOptions _commandLineOptions;

    public HttpServer(ICommandLineOptions commandLineOptions) => _commandLineOptions = commandLineOptions;

    public string Name => "Http";

    public bool IsServerMode => _commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.ServerOptionKey, out string[]? serverOptions) &&
                                serverOptions.Contains(Name) &&
                                _commandLineOptions.TryGetOptionArgumentList(PlatformCommandLineProvider.HttpHostOptionKey, out string[]? httpHost) &&
                                httpHost?.Length == 1
                                ;

    public Task AfterCommonServiceSetupAsync() => Task.CompletedTask;

    public void Dispose() => throw new NotImplementedException();

    public Task<IPushOnlyProtocolConsumer> GetDataConsumerAsync() => throw new NotImplementedException();

    public Task HelpInvokedAsync() => throw new NotImplementedException();

    public Task<bool> IsCompatibleProtocolAsync(string testHostType) => throw new NotImplementedException();

#if NETCOREAPP
    public ValueTask DisposeAsync() => throw new NotImplementedException();
#endif
}

internal class HttpServerConsumer : IPushOnlyProtocolConsumer
{
    public Type[] DataTypesConsumed => throw new NotImplementedException();

    public string Uid => throw new NotImplementedException();

    public string Version => throw new NotImplementedException();

    public string DisplayName => throw new NotImplementedException();

    public string Description => throw new NotImplementedException();

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<bool> IsEnabledAsync() => throw new NotImplementedException();

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken) => throw new NotImplementedException();
}
