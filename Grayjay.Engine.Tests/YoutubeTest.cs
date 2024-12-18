using Newtonsoft.Json;
using System.Diagnostics;

namespace Grayjay.Engine.Tests
{
    [TestClass]
    public class YoutubeTest
    {
        [TestMethod]
        public void ReadResources()
        {
            Console.WriteLine("Resources:");
            Console.WriteLine(string.Join(',', Resources.GetResourceNames()));
        }
        [TestMethod]
        public void TestGetHome()
        {
            using (GrayjayPlugin plugin = GrayjayPlugin.FromUrl("https://plugins.grayjay.app/Youtube/YoutubeConfig.json"))
            {
                plugin.Initialize();

                plugin.Enable();

                Stopwatch w = new Stopwatch();
                w.Start();
                var content = plugin.GetHome();
                string json = JsonConvert.SerializeObject(content.GetResults()[0], Formatting.Indented);
                w.Stop();
                Console.WriteLine($"ContentDetails ({w.ElapsedMilliseconds}ms):\n" + json);

                content.NextPage();
                var moreContents = content.GetResults();
                if (moreContents.Length <= 0)
                    Console.WriteLine("Page2 is empty");
                else
                    Console.WriteLine("Page2: " + moreContents.Length.ToString() + "\n"
                        + JsonConvert.SerializeObject(content.GetResults()[0], Formatting.Indented));
            }
        }
        [TestMethod]
        public void TestContentDetails()
        {
            using(GrayjayPlugin plugin = GrayjayPlugin.FromUrl("https://plugins.grayjay.app/Youtube/YoutubeConfig.json"))
            {
                plugin.Initialize();

                plugin.Enable();

                Stopwatch w = new Stopwatch();
                w.Start();
                var content = plugin.GetContentDetails("https://www.youtube.com/watch?v=gggehz298L8");
                string json = JsonConvert.SerializeObject(content, Formatting.Indented);
                w.Stop();
                Console.WriteLine($"ContentDetails ({w.ElapsedMilliseconds}ms):\n" + json);
            }
        }
        [TestMethod]
        public void TestPerformance()
        {
            const int count = 100000;
            using (GrayjayPlugin plugin = GrayjayPlugin.FromUrl("https://plugins.grayjay.app/Youtube/YoutubeConfig.json"))
            {
                plugin.Initialize();

                plugin.Enable();


                Stopwatch w = new Stopwatch();
                w.Start();
                for (int i = 0; i < count; i++)
                    plugin.RawEvaluate("config.name");
                w.Stop();
                Console.WriteLine($"Fetch {count} times in {w.ElapsedMilliseconds}ms");
                Console.WriteLine($"Average Fetch time: {(((double)w.ElapsedMilliseconds) / count).ToString("0.###")}ms");
            }
        }
        [TestMethod]
        public void TestCipher()
        {
            string hash = "7ee36b0e";
            using (GrayjayPlugin plugin = GrayjayPlugin.FromUrl("https://plugins.grayjay.app/Youtube/YoutubeConfig.json"))
            {
                plugin.OnLog += (c, log) => Console.WriteLine($"Plugin[{c.Name}]: {log}");

                plugin.Initialize();

                plugin.Enable();

                var result = plugin.RawEvaluate($"source.prepareCipher(CIPHER_TEST_PREFIX + \"{hash}\" + CIPHER_TEST_SUFFIX)");

                Console.WriteLine(JsonConvert.SerializeObject(result));

                Assert.IsTrue(result);
            }
        }
    }
}