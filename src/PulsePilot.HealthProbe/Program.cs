const string DefaultEndpoint = "http://127.0.0.1:8080/health/ready";

var endpoint = args.Length == 0 ? DefaultEndpoint : args[0];
if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)
    || (endpointUri.Scheme != Uri.UriSchemeHttp
        && endpointUri.Scheme != Uri.UriSchemeHttps))
{
    return 2;
}

using var handler = new SocketsHttpHandler
{
    ConnectTimeout = TimeSpan.FromSeconds(1),
};
using var client = new HttpClient(handler)
{
    Timeout = TimeSpan.FromSeconds(2),
};

try
{
    using var response = await client.GetAsync(
        endpointUri,
        HttpCompletionOption.ResponseHeadersRead);
    return response.IsSuccessStatusCode ? 0 : 1;
}
catch (HttpRequestException)
{
    return 1;
}
catch (TaskCanceledException)
{
    return 1;
}
