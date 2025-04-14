using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Serializers
{
    public class NullableDefaultLongConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                if (reader.TokenType == JsonTokenType.Number)
                    return reader.GetInt64();
                else
                    return 0;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse long on type [{typeToConvert.Name}] with value type [{reader.TokenType}]");
            }
        }

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value);
        }
    }
}
