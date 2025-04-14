using Grayjay.Engine.Models;
using Grayjay.Engine.Models.Feed;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Serializers
{
    public class PlatformContentConverter : JsonConverter<PlatformContent>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return base.CanConvert(typeToConvert);
        }

        public override PlatformContent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Utf8JsonReader readerClone = reader;

            if (readerClone.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            readerClone.Read();
            if (readerClone.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            string? propertyName = readerClone.GetString();
            if (!propertyName.Equals(nameof(PlatformContent.ContentType), StringComparison.OrdinalIgnoreCase))
                throw new JsonException("Expected property ContentType");

            readerClone.Read();
            ContentType typeDiscriminator = (readerClone.TokenType == JsonTokenType.Number) ?
                (ContentType)readerClone.GetInt32() :
                (ContentType)Enum.Parse(typeof(ContentType), readerClone.GetString());
            PlatformContent content = typeDiscriminator switch
            {
                ContentType.MEDIA => JsonSerializer.Deserialize<PlatformVideo>(ref reader, options)!,
                _ => JsonSerializer.Deserialize<PlatformContent>(ref reader, options)
            };
            return content;
        }

        public override void Write(Utf8JsonWriter writer, PlatformContent value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options.ExcludeConverter<PlatformContent>());
        }
    }
}
