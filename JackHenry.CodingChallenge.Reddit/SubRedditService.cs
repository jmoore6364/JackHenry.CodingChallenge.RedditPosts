using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

public class SubRedditService : ISubRedditService
{
    private readonly IRedditClient redditClient;
    private readonly ISubRedditRepository subRedditRepository;
    private readonly ILogger<SubRedditService> logger;

    public SubRedditService(IRedditClient redditClient, ISubRedditRepository subRedditRepository, ILogger<SubRedditService> logger)
    {
        this.redditClient = redditClient;
        this.subRedditRepository = subRedditRepository;
        this.logger = logger;
    }

    public async Task AddPosts(string subreddit)
    {
        await AddPosts(subreddit, null);
    }

    private async Task AddPosts(string subreddit, string? after)
    {
        HttpResponseMessage response = await redditClient.SendAsync($"r/{subreddit}/new?after={after}&limit=100");
        if (!response.IsSuccessStatusCode)
            return;
        if (response.Content == null)
            return;
        string result = await response.Content.ReadAsStringAsync();
        if (result == null)
            return;
        JObject? jsonResult;
        try
        {
            jsonResult = JObject.Parse(result);            
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, ex);
            return;
        }
        JToken? data = jsonResult["data"];
        if (data == null)
            return;
        JToken? children = data["children"];
        if (children == null)
            return;
        foreach (JToken child in children)
            subRedditRepository.Add(child);
        JToken? afterToken = data["after"];
        if (afterToken == null) 
            return;
        await AddPosts(subreddit, afterToken.ToString());
    }
}
