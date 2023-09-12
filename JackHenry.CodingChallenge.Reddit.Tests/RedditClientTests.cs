using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Reflection.PortableExecutable;

namespace JackHenry.CodingChallenge.Reddit.Tests
{
    [TestClass]
    public class RedditClientTests
    {
        private const string TestRoute = "test";
        private const string RateLimitRemainingHeader = "x-ratelimit-remaining";
        private const string RateLimitResetHeader = "x-ratelimit-reset";
        private const string RedditClientName = "reddit";
        private string testEndpointBase = "http://1.0.0.0/";
        private string tokenEndpoint;
        private string testEndpoint;
        
        private HttpResponseMessage tokenResponse = new HttpResponseMessage()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{'access_token': 'token','expires_in':'1'}")
        };
        private HttpResponseMessage sendResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{}")
        };
        private Mock<IOptions<RedditClientOptions>> redditClientOptionsMock;

        [TestInitialize]
        public void SetupTest()
        {
            tokenEndpoint = $"{testEndpointBase}token";
            testEndpoint = $"{testEndpointBase}{TestRoute}";
            sendResponse.Headers.Add(RateLimitRemainingHeader, "4.0");
            sendResponse.Headers.Add(RateLimitResetHeader, "1");
            redditClientOptionsMock = new Mock<IOptions<RedditClientOptions>>();
            redditClientOptionsMock.Setup(q => q.Value).Returns(new RedditClientOptions
            {
                ClientId = "",
                ClientSecret = "",
                UserName = "",
                Password = "",
                TokenEndpoint = tokenEndpoint
            });
        }

        [TestMethod]
        public async Task Send_AfterExpiration_RefreshesToken()
        {
            Mock<IHttpClientFactory> httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(a => a.RequestUri.ToString() == tokenEndpoint),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(tokenResponse)
               .Verifiable(Times.Exactly(2));
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(a => a.RequestUri.ToString() == testEndpoint),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(sendResponse)
               .Verifiable();
            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri(testEndpointBase),               
            };
           
            httpClientFactoryMock.Setup(q => q.CreateClient(RedditClientName)).Returns(httpClient);
            RedditClient redditClient = new RedditClient(httpClientFactoryMock.Object, redditClientOptionsMock.Object);
            await redditClient.SendAsync(TestRoute);
            await Task.Delay(1000);
            await redditClient.SendAsync(TestRoute);
            handlerMock.Verify();
        }

        [TestMethod]
        public async Task Send_Test_ReturnsSuccess()
        {
            Mock<IHttpClientFactory> httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            string testSendResponseContent = "{'test':'data'}";
            var sendResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(testSendResponseContent)
            };
            sendResponse.Headers.Add(RateLimitRemainingHeader, "3.0");
            sendResponse.Headers.Add(RateLimitResetHeader, "4");
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(a => a.RequestUri.ToString() == tokenEndpoint),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(tokenResponse)
               .Verifiable();
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(a => a.RequestUri.ToString() == testEndpoint),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(sendResponse)
               .Verifiable();
            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri(testEndpointBase),
            };
            httpClientFactoryMock.Setup(q => q.CreateClient(RedditClientName)).Returns(httpClient);
            RedditClient redditClient = new RedditClient(httpClientFactoryMock.Object, redditClientOptionsMock.Object);
            var result = await redditClient.SendAsync(TestRoute);
            string resultContent = await result.Content.ReadAsStringAsync();
            Assert.AreEqual(testSendResponseContent, resultContent);
        }

        [TestMethod]
        public async Task Send_RateLimitHit_WaitsForReset()
        {
            Mock<IHttpClientFactory> httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var sendResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            };
            sendResponse.Headers.Add(RateLimitRemainingHeader, "2");
            sendResponse.Headers.Add(RateLimitResetHeader, "1");
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(a => a.RequestUri.ToString() == tokenEndpoint),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(tokenResponse)
               .Verifiable();
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(a => a.RequestUri.ToString() == testEndpoint),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(sendResponse)
               .Verifiable();
            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri(testEndpointBase),
            };
            httpClientFactoryMock.Setup(q => q.CreateClient(RedditClientName)).Returns(httpClient);
            RedditClient redditClient = new RedditClient(httpClientFactoryMock.Object, redditClientOptionsMock.Object);
            await redditClient.SendAsync(TestRoute);
            var before = DateTime.Now;            
            await redditClient.SendAsync(TestRoute);
            var x = DateTime.Now - before;
            Assert.IsTrue(x.TotalSeconds >= 1);
        }
    }
}