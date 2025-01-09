using Fizzler.Systems.HtmlAgilityPack;
using Grayjay.Engine.Web;
using HtmlAgilityPack;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using static Grayjay.Engine.Extensions;

namespace Grayjay.Engine.Packages
{
    [NoDefaultScriptAccess]
    public class PackageDOMParser: Package
    {
        public override string VariableName => "domParser";

        public PackageDOMParser(GrayjayPlugin plugin) : base(plugin)
        {

        }

        public override void Initialize(V8ScriptEngine engine)
        {
            engine.AddHostObject("domParser", this);
        }

        public override void Dispose()
        {

        }


        [ScriptMember("parseFromString")]
        public DOMNode parseFromString(string html, string type = null)
        {
            return DOMNode.Parse(this, html);
        }


    }

    [NoDefaultScriptAccess]
    public class DOMNode
    {
        private PackageDOMParser _package;
        private HtmlNode _node = null;

        [ScriptMember("nodeType")]
        public string NodeType => _node.Name;

        [ScriptMember("childNodes")]
        public object ChildNodes => _node.ChildNodes.Select(x => new DOMNode(_package, x)).ToScriptArray();

        [ScriptMember("firstChild")]
        public DOMNode FirstChild => new DOMNode(_package, _node.FirstChild);

        [ScriptMember("lastChild")]
        public DOMNode LastChild => new DOMNode(_package, _node.LastChild);

        [ScriptMember("parentNode")]
        public DOMNode ParentNode => new DOMNode(_package, _node.ParentNode);

        [ScriptMember("attributes")]
        public Dictionary<string, string> Attributes => _node.Attributes.ToDictionary(x => x.Name, y => y.Value);

        [ScriptMember("innerHTML")]
        public string InnerHTML => _node.InnerHtml;

        [ScriptMember("outerHTML")]
        public string OuterHtml => _node.OuterHtml;

        [ScriptMember("textContent")]
        public string TextContent => _node.InnerText;

        [ScriptMember("text")]
        public string Text => _node.InnerText;

        [ScriptMember("data")]
        public string Data => _node.InnerText;

        [ScriptMember("classList")]
        public object ClassList => ScriptEngine.Current.Script.Array.from(_node.GetClasses());

        [ScriptMember("className")]
        public string ClassName => string.Join(" ", ClassList);


        public DOMNode(PackageDOMParser package, HtmlNode node)
        {
            _package = package;
            _node = node;
        }

        [ScriptMember("getAttribute")]
        public string GetAttribute(string name)
        {
            return _node.Attributes[name]?.Value;
        }
        [ScriptMember("getElementById")]
        public DOMNode GetElementByID(string id)
        {
            var node = _node.OwnerDocument.GetElementbyId(id);
            if (node != null)
                return new DOMNode(_package, node);
            return null;
        }

        [ScriptMember("getElementsByClassName")]
        public object GetElementsByClassName(string className)
        {
            string[] classParts = className.Split(" ");
            var results = _node.Descendants(0)
                .Where(x => classParts.All(y=>x.HasClass(y)))
                .Select(x => new DOMNode(_package, x))
                .ToList();

            return results.ToScriptArray(ScriptEngine.Current);
        }

        [ScriptMember("getElementsByTagName")]
        public object GetElementsByTagName(string tagName)
        {
            return _node.Descendants(tagName)
                .Select(x => new DOMNode(_package, x))
                .ToScriptArray(ScriptEngine.Current);
        }

        [ScriptMember("getElementsByName")]
        public object GetElementsByName(string name)
        {
            return _node.Descendants()
                .Where(x => x.GetAttributeValue("name", null) == name)
                .Select(x => new DOMNode(_package, x))
                .ToScriptArray(ScriptEngine.Current);
        }

        [ScriptMember("querySelector")]
        public DOMNode QuerySelector(string query)
        {
            return new DOMNode(_package, _node.QuerySelector(query));
        }
        [ScriptMember("querySelectorAll")]
        public object QuerySelectorAll(string query)
        {
            return _node.QuerySelectorAll(query)
                .Select(x => new DOMNode(_package, x))
                .ToScriptArray();
        }

        [ScriptMember("dispose")]
        public void Dispose(string query) { }

        public static DOMNode Parse(PackageDOMParser package, string html)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            return new DOMNode(package, htmlDoc.DocumentNode);
        }
    }
}
