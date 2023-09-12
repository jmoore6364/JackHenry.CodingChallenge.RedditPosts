using Newtonsoft.Json.Linq;

public interface ISubRedditRepository
{
    void Add(JToken post);
    Dictionary<string, JToken> GetPosts();
}