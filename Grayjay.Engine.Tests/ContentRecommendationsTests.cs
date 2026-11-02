using Grayjay.Engine.Models.Detail;

namespace Grayjay.Engine.Tests
{
    [TestClass]
    public class ContentRecommendationsTests
    {
        private const string Script = """
            function buildVideo(url) {
                return {
                    id: new PlatformID("Test", url, plugin.config.id),
                    name: url,
                    thumbnails: new Thumbnails([]),
                    author: new PlatformAuthorLink(new PlatformID("Test", "author", plugin.config.id), "author", "author-url"),
                    datetime: 1,
                    url: url,
                    shareUrl: url
                };
            }
            source.enable = function () { };
            source.getContentDetails = function (url) {
                return new PlatformVideoDetails({
                    ...buildVideo(url),
                    video: new VideoSourceDescriptor([]),
                    getContentRecommendations: () => new ContentPager([new PlatformVideo(buildVideo("recommended"))], false)
                });
            };
            """;

        [TestMethod]
        public void ConstructorFunctionIsCallableFromDetails()
        {
            var config = new PluginConfig { ID = Guid.NewGuid().ToString(), Name = "Test" };
            using var plugin = new GrayjayPlugin(config, Script);
            plugin.Initialize();
            plugin.Enable();

            var details = (PlatformVideoDetails)plugin.GetContentDetails("video");
            var recommendations = details.GetContentRecommendations();

            Assert.IsNotNull(recommendations);
            Assert.AreEqual("recommended", recommendations.GetResults().Single().Url);
        }
    }
}
