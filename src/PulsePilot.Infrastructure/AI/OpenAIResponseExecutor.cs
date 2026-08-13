#pragma warning disable OPENAI001

using System.ClientModel;
using OpenAI.Responses;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Infrastructure.AI;

public sealed class OpenAIResponseExecutor(ResponsesClient responsesClient)
{
    public async Task<ResponseResult> CreateResponseAsync(
        CreateResponseOptions createOptions,
        CancellationToken cancellationToken = default)
    {
        ResponseResult response;

        try
        {
            var clientResult = await responsesClient.CreateResponseAsync(
                createOptions,
                cancellationToken);
            response = clientResult.Value;
        }
        catch (ClientResultException exception) when (!cancellationToken.IsCancellationRequested)
        {
            var isTransient = IsTransientStatus(exception.Status);

            throw new LlmProviderException(
                isTransient
                    ? LlmProviderFailureKind.ProviderUnavailable
                    : LlmProviderFailureKind.ProviderFailure,
                isTransient
                    ? "The AI provider is temporarily unavailable."
                    : "The AI provider rejected the request.",
                isTransient);
        }
        catch (HttpRequestException exception)
        {
            throw new LlmProviderException(
                LlmProviderFailureKind.ProviderUnavailable,
                "The AI provider is temporarily unavailable.",
                isTransient: true,
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmProviderException(
                LlmProviderFailureKind.ProviderUnavailable,
                "The AI provider request timed out.",
                isTransient: true,
                exception);
        }

        ThrowForNonCompletedResponse(response);

        return response;
    }

    private static void ThrowForNonCompletedResponse(ResponseResult response)
    {
        var hasRefusal = response.OutputItems
            .OfType<MessageResponseItem>()
            .SelectMany(message => message.Content)
            .Any(content => content.Kind == ResponseContentPartKind.Refusal);

        if (hasRefusal)
        {
            throw new LlmProviderException(
                LlmProviderFailureKind.Refused,
                "The AI provider refused the requested output.");
        }

        if (response.Status == ResponseStatus.Incomplete)
        {
            throw new LlmProviderException(
                LlmProviderFailureKind.Incomplete,
                "The AI provider returned incomplete output.");
        }

        if (response.Status == ResponseStatus.Failed)
        {
            throw new LlmProviderException(
                LlmProviderFailureKind.ProviderFailure,
                "The AI provider failed to produce the requested output.");
        }

        if (response.Status != ResponseStatus.Completed)
        {
            throw new LlmProviderException(
                LlmProviderFailureKind.Incomplete,
                "The AI provider did not complete the requested output.");
        }
    }

    private static bool IsTransientStatus(int status)
    {
        return status is 0 or 408 or 409 or 429 || status >= 500;
    }
}

#pragma warning restore OPENAI001
