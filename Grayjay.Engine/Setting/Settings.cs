using Grayjay.Engine;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Setting
{
    public abstract class Settings<T> where T: Settings<T>
    {




        public SettingsObject<T> GetSettingsObject(string id = null)
        {
            return SettingsObject<T>.FromObject((T)this, id);
        }

        public static T FromText(string json)
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        public static string ToText(T obj)
        {
            return JsonSerializer.Serialize<T>(obj);
        }

    }

    public class SettingsObject<T>
    {
        public string ID { get; set; }
        public T Object { get; set; }
        public SettingsField[] Fields { get; set; }

        public static SettingsObject<T> FromObject(T obj, string id = null)
        {
            return new SettingsObject<T>()
            {
                ID = id,
                Object = obj,
                Fields = SettingsField.FromObject(obj)
            };
        }
    }

    public class SettingsFieldAttribute: Attribute
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }

        public SettingsFieldAttribute(string name, string type, string description, int order, string id = null)
        {
            Name = name;
            Type = type;
            Description = description;
            Order = order;
            ID = id;
        }
    }
    public class SettingsDropdownOptionsAttribute : Attribute
    {
        public string[] Options { get; set; }

        public SettingsDropdownOptionsAttribute(params string[] options)
        {
            Options = options;
        }
    }



    [JsonDerivedType(typeof(SettingsFieldToggle))]
    [JsonDerivedType(typeof(SettingsFieldGroup))]
    [JsonDerivedType(typeof(SettingsFieldDropDown))]
    [JsonDerivedType(typeof(SettingsFieldReadOnly))]
    [JsonDerivedType(typeof(SettingsFieldGroupFlat))]
    public abstract class SettingsField
    {
        public abstract string Type { get; }
        public string Title { get; set; }
        public string Description { get; set; }

        public string Property { get; set; }
        public string Dependency { get; set; }
        public string WarningDialog { get; set; }

        public SettingsField(string title, string description, string property = null)
        {
            Title = title;
            Description = description;
            Property = property.ToCamelCased();
        }

        public static SettingsField[] FromObject(object value) => FromObject(value.GetType(), value);
        public static SettingsField[] FromObject(Type type, object value)
        {
            PropertyInfo[] properties = type.GetProperties();

            return properties
                .Select(x => FromProperty(x, x.GetValue(value)))
                .Where(x => x != null)
                .ToArray();
        }

        public static SettingsField? FromProperty(PropertyInfo info, object value = null)
        {
            SettingsFieldAttribute attr = info.GetCustomAttribute<SettingsFieldAttribute>();
            if (attr == null)
                return null;
            
            switch(attr.Type)
            {
                case TOGGLE:
                    return new SettingsFieldToggle(attr.Name, attr.Description, info.Name, (bool)value);
                case DROPDOWN:
                    SettingsDropdownOptionsAttribute attrDropdown = info.GetCustomAttribute<SettingsDropdownOptionsAttribute>();
                    if (attrDropdown != null)
                        return new SettingsFieldDropDown(attr.Name, attr.Description, info.Name, (int)value, attrDropdown.Options);
                    else
                        return null;
                case GROUP:
                    return new SettingsFieldGroup(attr.Name, attr.Description, info.Name, FromObject(value?.GetType(), value));
                case READONLY:
                    return new SettingsFieldReadOnly(attr.Name, attr.Description, info.Name, (string)value);
                default:
                    return null;
            }
        }

        public const string DROPDOWN = "dropdown";
        public const string GROUP = "group";
        public const string GROUPFLAT = "group_flat";
        public const string READONLY = "readonly";
        public const string TOGGLE = "toggle";
        public const string BUTTON = "button";
    }

    public class SettingsFieldButton : SettingsField
    {
        public override string Type => BUTTON;
        public string Icon { get; set; }

        public SettingsFieldButton(string name, string description, string property, string icon) : base(name, description, property)
        {
            Icon = icon;
        }
    }
    public class SettingsFieldToggle : SettingsField
    {
        public override string Type => TOGGLE;
        public bool Value { get; set; }

        public SettingsFieldToggle(string name, string description, string property, bool value): base(name, description, property)
        {
            Value = value;
        }
    }
    public class SettingsFieldGroup : SettingsField
    {
        public override string Type => GROUP;

        public SettingsField[] Fields { get; set; }


        public SettingsFieldGroup(string name, string description, string property, params SettingsField[] subFields) : base(name, description, property)
        {
            Fields = subFields;
        }
    }
    public class SettingsFieldGroupFlat : SettingsFieldGroup
    {
        public override string Type => GROUPFLAT;

        public SettingsFieldGroupFlat(string name, string description, string property, params SettingsField[] subFields) : base(name, description, property, subFields)
        {
        }
    }
    public class SettingsFieldDropDown : SettingsField
    {
        public override string Type => DROPDOWN;
        public string[] Options { get; set; }
        public int Value { get; set; }

        public SettingsFieldDropDown(string name, string description, string property, int value, params string[] options) : base(name, description, property)
        {
            Options = options;
            Value = value;
        }
    }
    public class SettingsFieldReadOnly : SettingsField
    {
        public override string Type => READONLY;
        public string Text { get; set; }

        public SettingsFieldReadOnly(string name, string description, string property, string text) : base(name, description, property)
        {
            Text = text;
        }
    }
}
