using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public class V8Pager<T> : IPager<T>
    {
        protected GrayjayPlugin _plugin;
        private bool _hasMorePages = false;
        protected IJavaScriptObject _obj;
        private Action<T>? _objInitializer;

        public T[] Results { get; set; }
        public string ID { get; set; } = Guid.NewGuid().ToString();

        public V8Pager(GrayjayPlugin plugin, IJavaScriptObject jobj) : this(plugin, jobj, null) {}

        public V8Pager(GrayjayPlugin plugin, IJavaScriptObject jobj, Action<T>? objectInitializer)
        {
            _plugin = plugin;
            _obj = jobj;

            _objInitializer = objectInitializer;

            _hasMorePages = (bool?)jobj.InvokeV8("hasMorePagers") ?? false;
            UpdateResults();
        }

        public bool HasMorePages()
        {
            return _hasMorePages;
        }

        public virtual void NextPage()
        {
            try
            {
                var obj = _obj.InvokeV8("nextPage");
                if (obj is IJavaScriptObject)
                    _obj = (IJavaScriptObject)obj;

                UpdateResults();
            }
            catch(Exception ex)
            {
                //_hasMorePages = false;
                throw;
            }
        }

        private void UpdateResults()
        {
            var results = _obj.GetProperty("results");
            Results = V8Converter.ConvertValue<T[]>(_plugin, results);

            if(_objInitializer != null)
                foreach (var obj in Results)
                    _objInitializer?.Invoke(obj);

            _hasMorePages = (bool?)_obj.InvokeV8("hasMorePagers") ?? false;
        }

        public T[] GetResults()
        {
            return Results;
        }
    }
    public class V8Pager<T, R> : IPager<R>
    {
        private bool _hasMorePages = false;
        private IJavaScriptObject _obj;
        private GrayjayPlugin _plugin;
        private Func<T, R> _objInitializer;

        public R[] Results { get; set; }
        public string ID { get; set; } = Guid.NewGuid().ToString();

        public V8Pager(GrayjayPlugin plugin, IJavaScriptObject jobj, Func<T, R> objectInitializer)
        {
            _obj = jobj;
            _plugin = plugin;
            _objInitializer = objectInitializer;

            _hasMorePages = (bool?)jobj.InvokeV8("hasMorePagers") ?? false;
            UpdateResults();
        }

        public bool HasMorePages()
        {
            return _hasMorePages;
        }

        public void NextPage()
        {
            try
            {
                var obj = _obj.InvokeV8("nextPage");
                if (obj is IJavaScriptObject)
                    _obj = (IJavaScriptObject)obj;

                UpdateResults();
            }
            catch (Exception ex)
            {
                //_hasMorePages = false;
                throw;
            }
        }

        private void UpdateResults()
        {
            var results = _obj.GetProperty("results");
            Results = V8Converter.ConvertValue<T[]>(_plugin, results)
                .Select(x => _objInitializer(x))
                .ToArray();


            _hasMorePages = (bool?)_obj.InvokeV8("hasMorePagers") ?? false;
        }

        public R[] GetResults()
        {
            return Results;
        }
    }
}
