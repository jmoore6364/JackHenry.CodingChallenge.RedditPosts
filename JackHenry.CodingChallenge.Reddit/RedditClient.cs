using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

public class RedditClient : IRedditClient
{
    private const int RateLimitDelayThreshold = 3;
    private const int RateLimitRemainingDefault = 100;
    private const int RateLimitResetDefault = 60;
    private readonly RedditClientOptions options;
    private int rateLimitRemaining = RateLimitRemainingDefault;
    private int rateLimitReset = RateLimitResetDefault;
    private string? accessToken = null;
    private DateTime tokenExpiration = DateTime.MaxValue;
    private string base64EncodedAuthenticationString;
    private HttpClient httpClient;

    public RedditClient(IHttpClientFactory httpClientFactory, IOptions<RedditClientOptions> options)
    {
        this.options = options.Value;
        base64EncodedAuthenticationString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{this.options.ClientId}:{this.options.ClientSecret}"));
        httpClient = httpClientFactory.CreateClient("reddit");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "ChangeMeClient/0.1 by user");
    }

    public async Task<HttpResponseMessage> SendAsync(string url, HttpContent? content = null, HttpMethod? httpMethod = null)
    {
        ArgumentNullException.ThrowIfNull(nameof(url));
        await Delay();
        string? token = await GetAccessToken();
        if (string.IsNullOrEmpty(token))
        {
            throw new ApplicationException("Unable to retrieve an access token");
        }
        HttpRequestMessage request = new HttpRequestMessage
        {
            RequestUri = new Uri(httpClient.BaseAddress + url),
            Content = content,
            Method = httpMethod == null ? HttpMethod.Get : httpMethod
        };
        request.Headers.Add("Authorization", $"Bearer {token}");
        HttpResponseMessage response = response = await httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            await Delay();
            return await SendAsync(url);
        }
        rateLimitRemaining = (int)Convert.ToDecimal(response.Headers.FirstOrDefault(q => q.Key == "x-ratelimit-remaining").Value.First());
        rateLimitReset = Convert.ToInt32(response.Headers.FirstOrDefault(q => q.Key == "x-ratelimit-reset").Value.First());
        return response;
    }

    private async Task Delay()
    {
        if (rateLimitRemaining < RateLimitDelayThreshold)
        {
            await Task.Delay(1000 * rateLimitReset);
            rateLimitRemaining = RateLimitRemainingDefault;
            rateLimitReset = RateLimitResetDefault;
        }
    }

    private async Task<string?> GetAccessToken()
    {
        if (accessToken != null && tokenExpiration.CompareTo(DateTime.Now) > -1)
            return accessToken;
        HttpContent content = new StringContent($"grant_type=password&username={options.UserName}&password={options.Password}", 
            new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded") { CharSet = "UTF-8" });
        HttpRequestMessage requestMessage = new HttpRequestMessage
        {
            RequestUri = new Uri(options.TokenEndpoint),
            Method = HttpMethod.Post,
            Content = content
        };
        requestMessage.Headers.Add("Authorization", $"Basic {base64EncodedAuthenticationString}");
        var result = await httpClient.SendAsync(requestMessage);
        if (!result.IsSuccessStatusCode)
            throw new ApplicationException("Unable to retrieve an access token");
        string resultContent = await result.Content.ReadAsStringAsync();
        var bearerData = JObject.Parse(resultContent);
        accessToken = bearerData["access_token"].ToString();
        tokenExpiration = DateTime.Now.AddSeconds(Convert.ToInt32(bearerData["expires_in"]));
        return accessToken;
    }
}
