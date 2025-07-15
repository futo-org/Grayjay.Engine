using Grayjay.Engine.Models.Live;
using Microsoft.ClearScript.JavaScript;
using System;

namespace Grayjay.Engine.Pagers;

public class LiveEventPager : V8Pager<PlatformLiveEvent>
{
    public int NextRequest { get; set; } = 1000;

    public LiveEventPager(GrayjayPlugin plugin, IJavaScriptObject jobj) : base(plugin, jobj)
    {
    }

    public LiveEventPager(GrayjayPlugin plugin, IJavaScriptObject jobj, Action<PlatformLiveEvent>? objectInitializer) : base(plugin, jobj, objectInitializer)
    {
    }

    public override void NextPage()
    {
        base.NextPage();
        NextRequest = _obj.GetOrDefault(_plugin, "nextRequest", "LiveEventPager", 1000);
    }
}
