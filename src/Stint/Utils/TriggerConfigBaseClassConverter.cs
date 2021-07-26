namespace Stint
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class TriggerConfigBaseClassConverter : JsonConverter<BaseTriggerConfig>
    {
        public string TypeDescriminatorPropertyName { get; }

        public Dictionary<string, Type> DerivedTypeMapping { get; }

        public Lazy<Dictionary<Type, string>> ReverseLookupDerivedTypeMapping { get; }


        public TriggerConfigBaseClassConverter(Dictionary<string, Type> derivedTypeMapping, string typeDescriminatorPropertyName = "TypeDiscriminator")
        {
            TypeDescriminatorPropertyName = typeDescriminatorPropertyName;
            DerivedTypeMapping = derivedTypeMapping;
            // lazy so we avoid doing work in construcotr and defer until first use.
            ReverseLookupDerivedTypeMapping = new Lazy<Dictionary<Type, string>>(() => DerivedTypeMapping.BuildReverseLookupDictionary());
        }
        //private enum TypeDiscriminator
        //{
        //    BaseClass = 0,
        //    DerivedA = 1,
        //    DerivedB = 2
        //}

        public override bool CanConvert(Type type) => typeof(BaseTriggerConfig).IsAssignableFrom(type);

        public override BaseTriggerConfig Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            // copy the reader at this position so we can use it to deserialize the entire object after validating the type descriminator property.
            var derivedObjectReader = reader;

            if (!reader.Read()
                    || reader.TokenType != JsonTokenType.PropertyName
                    || reader.GetString() != TypeDescriminatorPropertyName)
            {
                throw new JsonException();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            BaseTriggerConfig baseClass;
            var derivedTypeName = reader.GetString();

            // TypeDiscriminator typeDiscriminator = (TypeDiscriminator)reader.GetInt32();
            if (!DerivedTypeMapping.TryGetValue(derivedTypeName, out var derivedType))
            {
                throw new NotSupportedException();
            }

            // use copy of reader at previous postition to read entire object.
            baseClass = (BaseTriggerConfig)JsonSerializer.Deserialize(ref derivedObjectReader, derivedType);

            if (!derivedObjectReader.Read() || derivedObjectReader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException();
            }

            //switch (typeDiscriminator)
            //{
            //    case TypeDiscriminator.DerivedA:
            //        if (!reader.Read() || reader.GetString() != "TypeValue")
            //        {
            //            throw new JsonException();
            //        }
            //        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            //        {
            //            throw new JsonException();
            //        }
            //        baseClass = (DerivedA)JsonSerializer.Deserialize(ref reader, typeof(DerivedA));
            //        break;
            //    case TypeDiscriminator.DerivedB:
            //        if (!reader.Read() || reader.GetString() != "TypeValue")
            //        {
            //            throw new JsonException();
            //        }
            //        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            //        {
            //            throw new JsonException();
            //        }
            //        baseClass = (DerivedB)JsonSerializer.Deserialize(ref reader, typeof(DerivedB));
            //        break;
            //    default:
            //        throw new NotSupportedException();
            //}

            //if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            //{
            //    throw new JsonException();
            //}

            return baseClass;
        }

        public override void Write(
            Utf8JsonWriter writer,
            BaseTriggerConfig value,
            JsonSerializerOptions options)
        {
           // writer.WriteStartObject();

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var reverseLookup = ReverseLookupDerivedTypeMapping.Value;
            if (!reverseLookup.TryGetValue(value.GetType(), out var name))
            {
                throw new NotSupportedException();
            }

            writer.WriteStartObject();
            writer.WriteString(TypeDescriminatorPropertyName, name);

            var json = JsonSerializer.Serialize(value, value.GetType());
            var document = JsonDocument.Parse(json);

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new NotSupportedException();
            }            

            foreach (var property in root.EnumerateObject())
            {
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.Flush();


            //json = json.TrimStart('{').TrimEnd('}');

            //json = $"{json},\"TypeDescriminatorPropertyName\":\"{name}\"}
            
            //doc.RootElement.add

            // writer.WriteString(TypeDescriminatorPropertyName, name);


            //if (value is DerivedA derivedA)
            //{
            //    writer.WriteNumber(TypeDescriminatorPropertyName, (int)TypeDiscriminator.DerivedA);
            //    writer.WritePropertyName("TypeValue");
            //    JsonSerializer.Serialize(writer, derivedA);
            //}
            //else if (value is DerivedB derivedB)
            //{
            //    writer.WriteNumber(TypeDescriminatorPropertyName, (int)TypeDiscriminator.DerivedB);
            //    writer.WritePropertyName("TypeValue");
            //    JsonSerializer.Serialize(writer, derivedB);
            //}
            //else
            //{
            //    throw new NotSupportedException();
            //}

            //writer.WriteEndObject();
        }
    }
}
