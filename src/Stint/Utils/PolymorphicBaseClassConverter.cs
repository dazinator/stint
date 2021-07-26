namespace Stint
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class PolymorphicBaseClassConverter<TBaseClass> : JsonConverter<TBaseClass>
    {
        public string TypeDescriminatorPropertyName { get; }

        public Dictionary<string, Type> DerivedTypeMapping { get; }

        public Lazy<Dictionary<Type, string>> ReverseLookupDerivedTypeMapping { get; }


        public PolymorphicBaseClassConverter(Dictionary<string, Type> derivedTypeMapping, string typeDescriminatorPropertyName = "TypeDiscriminator")
        {
            TypeDescriminatorPropertyName = typeDescriminatorPropertyName;
            DerivedTypeMapping = derivedTypeMapping;
            // lazy so we avoid doing work in construcotr and defer until first use.
            ReverseLookupDerivedTypeMapping = new Lazy<Dictionary<Type, string>>(() => DerivedTypeMapping.BuildReverseLookupDictionary());
        }

        public override bool CanConvert(Type type) => typeof(TBaseClass).IsAssignableFrom(type);

        public override TBaseClass Read(
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

            TBaseClass baseClass;
            var derivedTypeName = reader.GetString();

            // TypeDiscriminator typeDiscriminator = (TypeDiscriminator)reader.GetInt32();
            if (!DerivedTypeMapping.TryGetValue(derivedTypeName, out var derivedType))
            {
                throw new NotSupportedException();
            }

            // use copy of reader at previous postition to read entire object.
            baseClass = (TBaseClass)JsonSerializer.Deserialize(ref derivedObjectReader, derivedType);

            if (!derivedObjectReader.Read() || derivedObjectReader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException();
            }

            return baseClass;
        }

        public override void Write(
            Utf8JsonWriter writer,
            TBaseClass value,
            JsonSerializerOptions options)
        {
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
        }
    }
}
