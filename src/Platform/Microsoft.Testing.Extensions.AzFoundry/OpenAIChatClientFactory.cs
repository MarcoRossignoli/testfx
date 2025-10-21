// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ClientModel;

using Azure.AI.OpenAI;

using Microsoft.Extensions.AI;
using Microsoft.Testing.Platform.AI;

namespace Microsoft.Testing.Extensions.AzFoundry;

/// <summary>
/// Factory for creating Azure OpenAI chat clients.
/// </summary>
public sealed class AzureOpenAIChatClientFactory : IChatClientFactory
{
    /// <inheritdoc />
    public IChatClient CreateChatClient()
    {
        string? endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string? deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        string? apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

        if (string.IsNullOrEmpty(endpoint))
        {
            throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable is not set.");
        }

        if (string.IsNullOrEmpty(deploymentName))
        {
            throw new InvalidOperationException("AZURE_OPENAI_DEPLOYMENT_NAME environment variable is not set.");
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("AZURE_OPENAI_API_KEY environment variable is not set.");
        }

        var client = new AzureOpenAIClient(
            new Uri(endpoint),
            new ApiKeyCredential(apiKey));

        return client.GetChatClient(deploymentName).AsIChatClient();
    }
}
