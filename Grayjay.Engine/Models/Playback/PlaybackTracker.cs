using Grayjay.Engine.Exceptions;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Playback
{
    public class PlaybackTracker
    {
        private GrayjayPlugin _plugin;

        private IJavaScriptObject _obj;
        private bool _hasOnInit = false;
        private bool _hasOnConcluded = false;

        private bool _hasCalledInit = false;

        private DateTime _lastRequest = DateTime.Now;
        private int _nextRequest = 1000;


        public PlaybackTracker(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            _plugin = plugin;
            //_config = config;
            _obj = obj;
            var objOnInit = obj.GetProperty("onInit");
            var objOnProgress = obj.GetProperty("onProgress");
            var objNextRequest = obj.GetProperty("nextRequest");
            var objOnConcluded = obj.GetProperty("onConcluded");

            if (objOnProgress == null || (objOnProgress is Undefined))
                throw new Exception("Missing onProgress on PlaybackTracker"); //TODO: Scriptexception if dep injection config implemented
            if (objNextRequest == null || (objNextRequest is Undefined))
                throw new Exception("Missing nextRequest on PlaybackTracker");

            _hasOnInit = objOnInit != null && !(objOnInit is Undefined);
            _hasOnConcluded = objOnConcluded != null && !(objOnConcluded is Undefined);
        }

        public void OnInit(double seconds)
        {
            lock (_obj)
            {
                if (_hasCalledInit)
                    return;
                if(_hasOnInit)
                    _obj.InvokeV8("onInit", new object[] { seconds });
                _nextRequest = Math.Max(100, _obj.GetOrThrow<int>(_plugin, "nextRequest", "PlaybackTracker", false));
                _lastRequest = DateTime.Now;
                _hasCalledInit = true;
            }
        }

        public void OnProgress(double seconds, bool isPlaying)
        {
            lock (_obj)
            {
                if (!_hasCalledInit && _hasOnInit)
                    OnInit(seconds);
                else
                {
                    _obj.InvokeV8("onProgress", Math.Floor(seconds), isPlaying);
                    _nextRequest = Math.Max(100, _obj.GetOrThrow<int>(_plugin, "nextRequest", "PlaybackTracker", false));
                    _lastRequest = DateTime.Now;
                }
            }
        }

        public void onConcluded()
        {
            if(_hasOnConcluded)
            {
                lock(_obj)
                {
                    _obj.InvokeV8("onConcluded", new object[] { -1 });
                }
            }
        }

        public bool ShouldUpdate()
        {
            return (DateTime.Now.Subtract(_lastRequest).TotalMilliseconds > _nextRequest);
        }
    }
}
