using Grayjay.Engine.Models.Feed;

namespace Grayjay.Engine.Tests
{
    [TestClass]
    public class UserHistoryTests
    {
        private const string Script = """
            function buildVideo(url) {
                return new PlatformVideo({
                    id: new PlatformID("Test", url, plugin.config.id),
                    name: url,
                    thumbnails: new Thumbnails([]),
                    author: new PlatformAuthorLink(new PlatformID("Test", "author", plugin.config.id), "author", "author-url"),
                    datetime: 1,
                    url: url,
                    duration: 1200
                });
            }
            source.enable = function () { };
            source.getUserPlaylists = function () { return []; };
            source.getUserHistory = function () {
                const watched = buildVideo("watched");
                watched.playbackTime = 600;
                watched.playbackDate = 1700000000;
                return new ContentPager([watched, buildVideo("undated")], false);
            };
            """;

        [TestMethod]
        public void PlaybackDateConvertsToNullableDateTime()
        {
            var config = new PluginConfig { ID = Guid.NewGuid().ToString(), Name = "Test" };
            using var plugin = new GrayjayPlugin(config, Script);
            plugin.Initialize();
            plugin.Enable();

            var history = plugin.getUserHistory().GetResults().Cast<PlatformVideo>().ToList();

            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(600, history[0].PlaybackTime);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, history[0].PlaybackDate?.ToUniversalTime());
            Assert.IsNull(history[1].PlaybackDate);
        }
    }
}
