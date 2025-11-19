using Grayjay.Engine.Pagers;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Grayjay.Engine.V8
{
    public static class V8Converter
    {
        private static Dictionary<Type, IV8Converter> _converters = new Dictionary<Type, IV8Converter>();


        private static IV8Converter GetConverter(Type type)
        {
            lock(_converters)
            {
                if (!_converters.ContainsKey(type))
                    _converters.Add(type, (IV8Converter)Activator.CreateInstance(typeof(V8Converter<>).MakeGenericType(type)));
                return _converters[type];
            }
        }

        public static T ConvertValue<T>(GrayjayPlugin plugin, object obj)
        {
            Type t = typeof(T);
            return (T)ConvertValue(plugin, t, obj);
        }
        public static object ConvertValue(GrayjayPlugin plugin, Type t, object obj)
        {
            if (obj == null)
                return null;

            if (t == typeof(object))
                return obj;

            object primitiveResult;
            if (TryConvertPrimitive(t, obj, out primitiveResult))
                return primitiveResult;

            if(t.IsAssignableTo(typeof(IDictionary)) && obj is PropertyBag bag)
            {
                var dict = (IDictionary)Activator.CreateInstance(t);
                foreach (var prop in bag)
                    dict.Add(prop.Key, prop.Value);
                return dict;
            }

            if (!(obj is IJavaScriptObject))
            {
                throw new InvalidOperationException($"No supported V8 mapping found for {t.Name}");
            }
            if(t.IsArray)
            {
                IJavaScriptObject jobj = (IJavaScriptObject)obj;

                Type arrayType = t.GetElementType();
                Array array = Array.CreateInstance(arrayType, jobj.PropertyIndices.Count());
                if (IsBasicType(arrayType))
                {

                    foreach (var index in jobj.PropertyIndices)
                    {
                        object elVal;
                        if (TryConvertPrimitive(arrayType, jobj.GetProperty(index), out elVal))
                            array.SetValue(elVal, index);
                    }    
                }
                else
                {
                    foreach (var index in jobj.PropertyIndices)
                    {
                        var elVal = ConvertValue(plugin, arrayType, jobj.GetProperty(index));
                        array.SetValue(elVal, index);
                    }
                        
                }
                return array;
            }
            else if(typeof(IList).IsAssignableFrom(t) && t.GetGenericArguments().Length > 0)
            {
                IJavaScriptObject jobj = (IJavaScriptObject)obj;
                Type genericType = t.GetGenericArguments()[0];

                IList list = (IList)Activator.CreateInstance(t);//  Array.CreateInstance(arrayType, jobj.PropertyIndices.Count());
                if (IsBasicType(genericType))
                {

                    foreach (var index in jobj.PropertyIndices)
                    {
                        object elVal;
                        if (TryConvertPrimitive(genericType, jobj.GetProperty(index), out elVal))
                            list.Add(elVal);
                    }
                }
                else
                {
                    foreach (var index in jobj.PropertyIndices)
                    {
                        var elVal = ConvertValue(plugin, genericType, jobj.GetProperty(index));
                        list.Add(elVal);
                    }

                }
                return list;
            }
            else if(typeof(IDictionary).IsAssignableFrom(t) && t.GetGenericArguments().Length > 1)
            {
                IJavaScriptObject jobj = (IJavaScriptObject)obj;
                Type keyType = t.GetGenericArguments()[0];
                if (keyType != typeof(string))
                    throw new NotImplementedException("Dictionaries only support string keys");
                Type valueType = t.GetGenericArguments()[1];

                IDictionary dict = (IDictionary)Activator.CreateInstance(t);
                if (IsBasicType(valueType))
                {

                    foreach (var index in jobj.PropertyNames)
                    {
                        object elVal;
                        if (TryConvertPrimitive(valueType, jobj.GetProperty(index), out elVal))
                            dict.Add(index, elVal);
                    }
                }
                else
                {
                    foreach (var index in jobj.PropertyNames)
                    {
                        var elVal = ConvertValue(plugin, valueType, jobj.GetProperty(index));
                        dict.Add(index, elVal);
                    }

                }
                return dict;
            }
            else if (t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPager<>)) && t.GetGenericArguments().Length > 0)
            {
                try
                {
                    if (!(obj is IJavaScriptObject jo))
                        throw new InvalidCastException($"Found {obj?.GetType()?.Name}, expected IJavaScriptObject");
                    var ctor = t.GetConstructor(new Type[] { typeof(IJavaScriptObject) });
                    if(ctor != null)
                        return ctor.Invoke(new object[] { jo });
                    ctor = t.GetConstructor(new Type[] { typeof(GrayjayPlugin), typeof(IJavaScriptObject) });
                    return ctor.Invoke(new object[] { plugin, jo });
                }
                catch(Exception ex)
                {
                    throw;
                }
            }

            if (typeof(IV8Polymorphic).IsAssignableFrom(t))
                t = IV8Polymorphic.GetPolymorphicType(t, (IJavaScriptObject)obj);

            if (t == typeof(IJavaScriptObject))
                return (IJavaScriptObject)obj;
            IV8Converter converter = GetConverter(t);
            return converter.Convert(plugin, (IJavaScriptObject)obj);
        }

        public static bool TryConvertPrimitive(Type t, object obj, out object value)
        {
            if (t.IsPrimitive || t == typeof(string))
            {
                if (obj != null && obj is not Undefined)
                {
                    try
                    {
                        value = obj.GetType() != t ? Convert.ChangeType(obj, t) : obj;
                    }
                    catch(Exception ex)
                    {
                        Logger.Error(nameof(V8Converter), $"FAILED TO PARSE VALUE ON {t.Name} (Value: {obj})");
                        value = null;
                    }
                }
                else
                    value = null;
                return true;
            }

            if (t == typeof(DateTime))
            {
                try
                {
                    value = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
                        .AddSeconds((int)(obj.GetType() != typeof(int) ? Convert.ChangeType(obj, typeof(int)) : obj))
                        .ToLocalTime();
                }
                catch(Exception ex)
                {
                    Logger.Error(nameof(V8Converter), $"FAILED TO PARSE VALUE ON {t.Name} (Value: {obj}): ", ex);
                    value = null;
                }


                return true;
            }
            value = null;
            return false;
        }

        public static bool IsBasicType(Type t)
        {
            return t.IsPrimitive || t == typeof(string) || t == typeof(DateTime);
        }
    }
    public interface IV8Converter
    {
        object Convert(GrayjayPlugin plugin, IJavaScriptObject obj);
    }
    public class V8Converter<T>: IV8Converter
    {

        public Type Type { get; private set; }
        private Dictionary<string, V8TypeProperty> _properties;

        private ConstructorInfo _constructorPlugin;
        private ConstructorInfo _constructorObject;
        private ConstructorInfo _constructorEmpty;

        public V8Converter()
        {
            Type = typeof(T);
            _properties = Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.GetCustomAttribute<V8PropertyAttribute>() != null)
                .Select(x => new V8TypeProperty(x, x.GetCustomAttribute<V8PropertyAttribute>()))
                .ToDictionary(x => x.Attribute.Name, y => y);

            _constructorPlugin = Type.GetConstructor(new[] { typeof(GrayjayPlugin), typeof(IJavaScriptObject) });
            _constructorObject = Type.GetConstructor(new[] { typeof(IJavaScriptObject) });
            _constructorEmpty = Type.GetConstructor(new Type[] { });

            if (_constructorObject == null && _constructorEmpty == null && _constructorPlugin == null)
                throw new InvalidOperationException($"Type [{Type.Name}] does not have a compatible V8 constructor");
        }

        public object Convert(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            T instance = (_constructorPlugin != null) ?
                (T)_constructorPlugin.Invoke(new object[] { plugin, obj }) : 
                    ((_constructorObject != null) ?
                        (T)_constructorObject.Invoke(new object[] { obj }) :
                        (T)_constructorEmpty.Invoke(new object[] { }));

            foreach (V8TypeProperty prop in _properties.Values)
            {
                try
                {
                    object val = obj.GetProperty(prop.Attribute.Name);
                    if (val is Undefined)
                    {
                        if (prop.Attribute.Optional)
                            continue;
                        else
                            throw new InvalidOperationException($"Undefined property [{prop.Info.DeclaringType.Name}.{prop.Attribute.Name}]");
                    }
                    prop.Set(instance, V8Converter.ConvertValue(plugin, prop.Info.PropertyType, val));
                }
                catch (InvalidCastException ex)
                {
                    throw;
                    //TODO: rewrite exception with context
                }
            }

            return instance;
        }

        public T[] ConvertArray(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            //if (obj.Kind != JavaScriptObjectKind.Array)
            //    throw new InvalidOperationException($"Expected array object, got {obj.Kind}");

            List<T> items = new List<T>();

            foreach (var index in obj.PropertyIndices)
                items.Add((T)Convert(plugin, (IJavaScriptObject)obj.GetProperty(index)));

            return items.ToArray();
        }


        private class V8TypeProperty
        {

            public PropertyInfo Info { get; private set; }
            public V8PropertyAttribute Attribute { get; private set; }

            public V8TypeProperty(PropertyInfo info, V8PropertyAttribute attr)
            {
                Info = info;
                Attribute = attr;
            }


            public void Set(T instance, object value)
            {
                try
                {
                    if (value is Double && Double.IsNaN((double)value))
                        value = null;
                    Info.SetValue(instance, value);
                }
                catch(Exception ex)
                {
                    throw new Exception($"Failed to set [{Info.DeclaringType.Name}.{Info.Name}]:{Info.PropertyType.Name} to {value?.GetType().Name}", ex);
                }
            }
        }
    }
}
