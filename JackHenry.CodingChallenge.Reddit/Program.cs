using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

const string ApiUrl = "https://oauth.reddit.com/";
const int RefreshReportInSeconds = 10;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
var configBuilder = new ConfigurationBuilder()
                      .SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: false);
IConfiguration config = configBuilder.Build();
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services
    .AddHttpClient("reddit", q =>
    {
        q.BaseAddress = new Uri(ApiUrl);
    });
builder.Services.Configure<RedditClientOptions>(builder.Configuration.GetSection("RedditClient"));
builder.Services
    .AddSingleton<ISubRedditService, SubRedditService>()
    .AddSingleton<ISubRedditRepository, SubRedditRepository>()
    .AddSingleton<IRedditClient, RedditClient>();
IHost host = builder.Build();
IServiceScope scope = host.Services.CreateScope();
ISubRedditService subRedditService = scope.ServiceProvider.GetService<ISubRedditService>();
ILogger<Program> logger = scope.ServiceProvider.GetService<ILogger<Program>>();
ISubRedditRepository subRedditRepository = scope.ServiceProvider.GetService<ISubRedditRepository>();

ThreadStart threadStart = new ThreadStart(async () =>
{
    while (true)
    {
        try
        {
            await subRedditService.AddPosts("gaming");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, ex);
        }
    }
});

logger.LogInformation("Starting...Initializing Posts");

Thread thread = new Thread(threadStart);
thread.Start();


while (true)
{
    Dictionary<string, JToken> posts = subRedditRepository.GetPosts();
    if (!posts.Any())
        continue;
    try
    {
        
        var orderedPosts = posts.OrderByDescending(q => Convert.ToInt32(q.Value["data"]["ups"])).Take(10);
        var usersWithMostPosts = posts.GroupBy(q => q.Value["data"]["author"].ToString()).OrderByDescending(q => q.Count()).Take(10);
        Console.WriteLine("Top 10 Posts________");
        foreach (var post in orderedPosts)
        {
            Console.WriteLine($"Post Title: {post.Value["data"]["title"]}, Up Votes: {post.Value["data"]["ups"]}");
        }
        Console.WriteLine("Top 10 Posting Users________");
        foreach (var post in usersWithMostPosts)
        {
            Console.WriteLine($"Author: {post.First().Value["data"]["author"]}");
        }
        Thread.Sleep(RefreshReportInSeconds * 1000);
    }
    catch (Exception ex)
    {
       logger.LogError(ex.Message, ex);
    }
}