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

public class VODEventPager : V8Pager<PlatformLiveEvent>
{
    public int NextRequest { get; set; } = 1000;

    public VODEventPager(GrayjayPlugin plugin, IJavaScriptObject jobj) : base(plugin, jobj)
    {
    }

    public VODEventPager(GrayjayPlugin plugin, IJavaScriptObject jobj, Action<PlatformLiveEvent>? objectInitializer) : base(plugin, jobj, objectInitializer)
    {
    }

    public void NextPage(long ms)
    {
        try
        {
            var obj = _obj.InvokeV8("nextPage", ms);
            if (obj is IJavaScriptObject)
                _obj = (IJavaScriptObject)obj;

            UpdateResults();
        }
        catch (Exception ex)
        {
            //_hasMorePages = false;
            throw;
        }
        NextRequest = _obj.GetOrDefault(_plugin, "nextRequest", "LiveEventPager", 1000);
    }
    public override void NextPage()
    {
        base.NextPage();
        NextRequest = _obj.GetOrDefault(_plugin, "nextRequest", "LiveEventPager", 1000);
    }
}