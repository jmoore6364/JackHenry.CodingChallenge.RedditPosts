public interface IRedditClient
{
    Task<HttpResponseMessage> SendAsync(string url, HttpContent? content = null, HttpMethod? httpMethod = null);
}
