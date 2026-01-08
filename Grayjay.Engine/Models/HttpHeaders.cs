using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.ClearScript;

namespace Grayjay.Engine.Models;

public sealed class HttpHeaders : IReadOnlyCollection<KeyValuePair<string, string>>
{
    public static HttpHeaders FromScriptObject(IScriptObject? headers)
    {
        var dict = headers?.ToDictionary<string>();
        return dict != null ? new HttpHeaders(dict) : new HttpHeaders();
    }

    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;
    private readonly List<KeyValuePair<string, string>> _items;

    public HttpHeaders() => _items = new();

    public HttpHeaders(IEnumerable<KeyValuePair<string, string>> items)
        => _items = items is List<KeyValuePair<string, string>> list ? new(list) : new(items);

    public HttpHeaders(HttpHeaders other)
    {
        if (other is null) throw new ArgumentNullException(nameof(other));
        _items = new(other._items);
    }

    public HttpHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> items)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));

        _items = new List<KeyValuePair<string, string>>();
        foreach (var (name, values) in items)
        {
            if (values is null)
            {
                Add(name, string.Empty);
                continue;
            }

            bool any = false;
            foreach (var v in values)
            {
                Add(name, v ?? string.Empty);
                any = true;
            }

            if (!any)
                Add(name, string.Empty);
        }
    }

    public HttpHeaders(WebHeaderCollection headers)
    {
        if (headers is null) throw new ArgumentNullException(nameof(headers));

        _items = new List<KeyValuePair<string, string>>();

        foreach (var name in headers.AllKeys)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var values = headers.GetValues(name);
            if (values is null || values.Length == 0)
            {
                Add(name, string.Empty);
                continue;
            }

            for (int i = 0; i < values.Length; i++)
                Add(name, values[i] ?? string.Empty);
        }
    }

    public IEnumerable<KeyValuePair<string, string>> Items => _items;

    public int Count => _items.Count;

    public string? this[string name]
    {
        get => TryGetFirst(name, out var v) ? v : null;
        set
        {
            if (value is null) Remove(name);
            else Set(name, value);
        }
    }

    /// <summary>
    /// Converts headers to a dictionary of single values.
    /// If a header appears multiple times, the last value wins.
    /// NOTE: This can lose data for multi-valued headers (notably Set-Cookie),
    /// because repeated header fields will be overwritten.
    /// </summary>
    public Dictionary<string, string> ToDictionaryLastWins()
    {
        var dict = new Dictionary<string, string>(NameComparer);
        for (int i = 0; i < _items.Count; i++)
        {
            var (k, v) = _items[i];
            dict[k] = v;
        }
        return dict;
    }

    /// <summary>
    /// Converts headers to a dictionary of lists, preserving duplicates and order.
    /// Recommended when you need correct handling for headers like Set-Cookie.
    /// </summary>
    public Dictionary<string, List<string>> ToDictionaryList()
    {
        var dict = new Dictionary<string, List<string>>(NameComparer);
        for (int i = 0; i < _items.Count; i++)
        {
            var (k, v) = _items[i];

            if (!dict.TryGetValue(k, out var list))
            {
                list = new List<string>();
                dict[k] = list;
            }

            list.Add(v);
        }
        return dict;
    }

    public void Add(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Header name cannot be null/empty.", nameof(name));
        _items.Add(new(name, value ?? string.Empty));
    }

    public void AddRange(IEnumerable<KeyValuePair<string, string>> headers)
    {
        if (headers is null) throw new ArgumentNullException(nameof(headers));
        foreach (var (k, v) in headers)
            Add(k, v);
    }

    public void Set(string name, string value)
    {
        Remove(name);
        Add(name, value);
    }

    public void SetIfAbsent(string name, string value)
    {
        if (!Contains(name))
            Add(name, value);
    }

    public bool TryGetFirst(string name, out string? value)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var kv = _items[i];
            if (NameComparer.Equals(name, kv.Key))
            {
                value = kv.Value;
                return true;
            }
        }
        value = null;
        return false;
    }

    public string? GetFirstOrDefault(string name)
        => TryGetFirst(name, out var v) ? v : null;

    public IEnumerable<string> GetAll(string name)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var kv = _items[i];
            if (NameComparer.Equals(name, kv.Key))
                yield return kv.Value;
        }
    }

    public IReadOnlyList<string> GetAllList(string name)
    {
        var list = new List<string>();
        foreach (var v in GetAll(name))
            list.Add(v);
        return list;
    }

    public IEnumerable<string> NamesDistinct()
        => _items.Select(kv => kv.Key).Distinct(NameComparer);

    public bool Contains(string name)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (NameComparer.Equals(name, _items[i].Key))
                return true;
        }
        return false;
    }

    public int RemoveAll(string name) => _items.RemoveAll(kv => NameComparer.Equals(name, kv.Key));
    public bool Remove(string name) => RemoveAll(name) > 0;
    public void Clear() => _items.Clear();
    public HttpHeaders Clone() => new(this);

    public void MergeFrom(HttpHeaders other, bool overwriteExisting)
    {
        if (other is null) throw new ArgumentNullException(nameof(other));

        if (overwriteExisting)
        {
            foreach (var name in other.NamesDistinct())
                RemoveAll(name);
        }

        AddRange(other._items);
    }

    public void MergeIfAbsentFrom(HttpHeaders other)
    {
        if (other is null) throw new ArgumentNullException(nameof(other));

        foreach (var (k, v) in other._items)
        {
            if (!Contains(k))
                Add(k, v);
        }
    }

    public ILookup<string, string> ToLookup() => _items.ToLookup(kv => kv.Key, kv => kv.Value, NameComparer);

    public override string ToString()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < _items.Count; i++)
        {
            var (k, v) = _items[i];
            sb.Append(k).Append(": ").Append(v).Append("\r\n");
        }
        return sb.ToString();
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public List<KeyValuePair<string, string>> ToList() => new(_items);
}
