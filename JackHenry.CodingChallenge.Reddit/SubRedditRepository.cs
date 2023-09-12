using Newtonsoft.Json.Linq;

public class SubRedditRepository : ISubRedditRepository
{
    private Dictionary<string, JToken> posts = new Dictionary<string, JToken>();

    public void Add(JToken post)
    {
        string id = post["data"]["id"].ToString();
        if (posts.ContainsKey(id))
        {
            posts[id] = post;
            return;
        }
        posts.Add(id, post);
    }

    public Dictionary<string, JToken> GetPosts()
    {
        return posts;
    }
}
