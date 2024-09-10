// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform;

internal class HttpServerConsumer : IPushOnlyProtocolConsumer
{
    private readonly HttpServer _httpServer;
    private readonly Uri _uri;
    private readonly IMessageFormatter _messageFormatter = FormatterUtilities.CreateFormatter();

    public HttpServerConsumer(HttpServer httpServer, Uri uri)
    {
        _httpServer = httpServer;
        _uri = uri;
    }

    public Type[] DataTypesConsumed => new[]
    {
        typeof(TestNodeUpdateMessage),
        // TODO: handle more information
        // typeof(TestRequestExecutionTimeInfo),
        // typeof(SessionFileArtifact),
        // typeof(TestNodeFileArtifact),
        // typeof(FileArtifact),
    };

    public string Uid => nameof(HttpServerConsumer);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => nameof(HttpServerConsumer);

    public string Description => nameof(HttpServerConsumer);

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task OnTestSessionFinishingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task OnTestSessionStartingAsync(SessionUid sessionUid, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        RoslynDebug.Assert(_httpServer._httpClient is not null);

        switch (value)
        {
            case TestNodeUpdateMessage testNodeUpdateMessage:
                string json = await _messageFormatter.SerializeAsync(testNodeUpdateMessage);
                await _httpServer._httpClient.PostAsync(_uri, new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken);
                break;
        }
    }
}
