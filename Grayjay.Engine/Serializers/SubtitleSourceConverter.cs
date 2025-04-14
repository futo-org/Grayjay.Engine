﻿using Grayjay.Engine.Models;
using Grayjay.Engine.Models.Feed;
using Grayjay.Engine.Models.Ratings;
using Grayjay.Engine.Models.Subtitles;
using Grayjay.Engine.Models.Video.Sources;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Serializers
{
    public enum SubtitleConvertBehavior
    {
        Null = 0,
        Fetch = 1,
        Serialize = 2
    }

    public class SubtitleSourceConverter : JsonConverter<SubtitleSource>
    {
        private SubtitleConvertBehavior _subBehavior = SubtitleConvertBehavior.Null;
        public SubtitleSourceConverter(SubtitleConvertBehavior subBehavior = SubtitleConvertBehavior.Null)
        {
            _subBehavior = subBehavior;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return base.CanConvert(typeToConvert);
        }

        public override SubtitleSource? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Utf8JsonReader readerClone = reader;

            bool hasRaw = false;

            if (readerClone.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            readerClone.Read();
            if (readerClone.TokenType != JsonTokenType.PropertyName)
                throw new JsonException();

            string? propertyName = readerClone.GetString();
            if (propertyName != "_subtitles" && propertyName != nameof(SubtitleRawSource._Subtitles))
                return GJsonSerializer.Deserialize<SubtitleSource.Serializable>(ref reader);
            else
                return GJsonSerializer.Deserialize<SubtitleRawSource>(ref reader);
        }

        public override void Write(Utf8JsonWriter writer, SubtitleSource value, JsonSerializerOptions options)
        {
            if (value is SubtitleRawSource raw)
            {
                JsonSerializer.Serialize(writer, raw, options);
            }
            else if (value.HasFetch)
            {
                switch (_subBehavior)
                {
                    case SubtitleConvertBehavior.Null:
                        writer.WriteNullValue();
                        break;
                    case SubtitleConvertBehavior.Serialize:
                        JsonSerializer.Serialize(writer, new SubtitleSource.Serializable()
                        {
                            Url = value.Url,
                            Name = value.Name,
                            Format = value.Format,
                            HasFetch = true
                        }, options);
                        break;
                    case SubtitleConvertBehavior.Fetch:
                        var rawSubs = value.ToRaw();
                        JsonSerializer.Serialize(writer, rawSubs, options);
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            else
                JsonSerializer.Serialize(writer, new SubtitleSource.Serializable(value), options.ExcludeConverter<SubtitleSource>());
        }
    }
}