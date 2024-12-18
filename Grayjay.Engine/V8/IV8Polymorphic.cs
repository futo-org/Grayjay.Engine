using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Grayjay.Engine.V8
{
    public interface IV8Polymorphic
    {
        private static Dictionary<Type, MethodInfo> _polyTypeMethods = new Dictionary<Type, MethodInfo>();

        public static Type GetPolymorphicType<T>(IJavaScriptObject obj)
        {
            Type t = typeof(T);
            return GetPolymorphicType(t, obj);
        }
        public static Type GetPolymorphicType(Type t, IJavaScriptObject obj)
        {
            lock (_polyTypeMethods)
            {
                if (!_polyTypeMethods.ContainsKey(t))
                {
                    MethodInfo method = t.GetMethod("GetPolymorphicType", BindingFlags.Public | BindingFlags.Static);
                    if (method == null)
                        throw new InvalidOperationException($"Type [{t.Name}] is missing GetPolymorphicType");
                    _polyTypeMethods[t] = method;
                }

                return (Type)_polyTypeMethods[t].Invoke(null, new object[] { obj });
            }
        }
    }
}
