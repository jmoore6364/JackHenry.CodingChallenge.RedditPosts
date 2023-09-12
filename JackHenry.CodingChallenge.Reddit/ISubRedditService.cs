using Newtonsoft.Json.Linq;

public interface ISubRedditService
{
    Task AddPosts(string subreddit);
}
