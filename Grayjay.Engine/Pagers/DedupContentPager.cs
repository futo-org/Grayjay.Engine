using Grayjay.Engine.Models.Feed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.Pagers
{
    public class DedupContentPager : IPager<PlatformContent>//, IAsyncPager<IPlatformContent>, IReplacerPager<IPlatformContent>
    {
        private readonly IPager<PlatformContent> _basePager;
        private readonly List<PlatformContent> _pastResults = new List<PlatformContent>();
        private PlatformContent[] _currentResults;
        private readonly List<string> _preferredPlatform;
        public event Action<PlatformContent, PlatformContent> OnReplaced;

        public string ID { get; set; } = Guid.NewGuid().ToString();
        public DedupContentPager(IPager<PlatformContent> basePager, IEnumerable<string> preferredPlatform = null)
        {
            _preferredPlatform = preferredPlatform?.ToList() ?? new List<string>();
            _basePager = basePager;
            _currentResults = DedupResults(_basePager.GetResults()).ToArray();
        }

        public bool HasMorePages() => _basePager.HasMorePages();

        public void NextPage()
        {
            _basePager.NextPage();
            _currentResults = DedupResults(_basePager.GetResults()).ToArray();
        }

        public PlatformContent[] GetResults() => _currentResults;

        private List<PlatformContent> DedupResults(PlatformContent[] results)
        {
            var resultsToRemove = new List<PlatformContent>();

            foreach (var result in results)
            {
                if (resultsToRemove.Contains(result) || result is PlatformContentPlaceholder)
                    continue;

                var sameItems = results.Where(r => IsSameItem(result, r)).ToList();
                var platformItemMap = sameItems.GroupBy(r => r.ID.PluginID).ToDictionary(group => group.Key, group => group.First());
                var bestPlatform = _preferredPlatform.Select(p => p.ToLowerInvariant()).FirstOrDefault(platformItemMap.ContainsKey);
                var bestItem = (bestPlatform != null) ?
                    platformItemMap.TryGetValue(bestPlatform, out var item) ? item : sameItems.FirstOrDefault()
                    : sameItems.FirstOrDefault();

               resultsToRemove.AddRange(sameItems.Where(r => r != bestItem));
            }

            var toReturn = results.Where(r => !resultsToRemove.Contains(r)).Select(item =>
            {
                var olderItemIndex = _pastResults.FindIndex(r => IsSameItem(item, r));
                if (olderItemIndex >= 0)
                {
                    var olderItem = _pastResults[olderItemIndex];
                    var olderItemPriority = _preferredPlatform.IndexOf(olderItem.ID.PluginID);
                    var newItemPriority = _preferredPlatform.IndexOf(item.ID.PluginID);

                    if (newItemPriority < olderItemPriority)
                    {
                        _pastResults[olderItemIndex] = item;
                        OnReplaced?.Invoke(olderItem, item);
                    }

                    return null;
                }

                return item;
            }).Where(item => item != null).ToList();
            _pastResults.AddRange(toReturn);
            return toReturn;
        }

        private bool IsSameItem(PlatformContent item, PlatformContent item2)
        {
            var daysAgo = Math.Abs(((int)item.DateTime.Subtract(DateTime.Now).TotalDays));
            var maxDelta = Math.Max(2, ((int)(daysAgo / 1.5))); // TODO: Better scaling delta
            var isSame = item.Name.Equals(item2.Name, StringComparison.OrdinalIgnoreCase) &&
                         (item.DateTime == null || item2.DateTime == null ||
                          Math.Abs(item.DateTime.Subtract(item2.DateTime).TotalDays) < maxDelta);

            return isSame;
        }

        private int CalculateHash(PlatformContent item)
        {
            return 0;
        }

        private static readonly string TAG = "DedupContentPager";
    }
}
