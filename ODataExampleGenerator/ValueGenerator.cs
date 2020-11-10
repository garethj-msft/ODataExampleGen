using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData;
using Microsoft.OData.Edm;

namespace ODataExampleGenerator
{
    internal class ValueGenerator
    {
        public int MonotonicId { get; set; } = 1;

        public Random Random { get; } = new Random();

        private GenerationParameters GenerationParameters { get; }
        private Dictionary<string, int> MonotonicPropertyValueTags { get; } = new Dictionary<string, int>();

        public ValueGenerator(GenerationParameters generationParameters)
        {
            this.GenerationParameters = generationParameters;
        }
        public ODataProperty GetExamplePrimitiveProperty(
            IEdmStructuredType hostType,
            IEdmStructuralProperty p)
        {
            bool isChosenValue = this.GenerationParameters.ChosenPrimitives.TryGetValue(p.Name, out string primitiveString);
            object primitive = !isChosenValue ? this.GetExampleStructuralValue(p) : this.GetSuppliedStructuralValue(p, primitiveString);
            var returnProp = new ODataProperty {Name = p.Name, Value = primitive};

            if (!p.Type.IsEnum())
            {
                returnProp.PrimitiveTypeKind = p.Type.PrimitiveKind();
            }
            return returnProp;
        }

        private object GetExampleStructuralValue(IEdmStructuralProperty p)
        {
            if (p.Type.IsCollection())
            {
                if (p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Enum)
                {
                    string firstEnumValue = this.GetExampleEnumValue(p);
                    return new ODataCollectionValue
                        {Items = new[] {new ODataEnumValue(firstEnumValue), new ODataEnumValue(this.GetExampleEnumValue(p, firstEnumValue))}};
                }
                else
                {
                    return this.GetExamplePrimitiveValueArray(p);
                }
            }
            else
            {
                if (p.Type.IsEnum())
                {
                    var usefulMember = this.GetExampleEnumValue(p);
                    return new ODataEnumValue(usefulMember);
                }
                else
                {
                    return this.GetExampleScalarPrimitiveValue(p);
                }
            }
        }

        private string GetExampleEnumValue(IEdmStructuralProperty p, params string[] avoidValues)
        {
            var enumType = (IEdmEnumType) p.Type.Definition.AsElementType();
            var usefulMembers = enumType.Members
                .Where(m => !m.Name.Equals("unknownFutureValue", StringComparison.OrdinalIgnoreCase))
                .Where(m => !avoidValues.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
                .Select(m => m.Name).ToList();
            string usefulMember = usefulMembers[this.Random.Next(usefulMembers.Count)];
            return usefulMember;
        }

        private object GetExampleScalarPrimitiveValue(IEdmStructuralProperty p)
        {
            return ((IEdmPrimitiveType)p.Type.Definition.AsElementType()).PrimitiveKind switch
            {
                EdmPrimitiveTypeKind.Boolean => true,
                EdmPrimitiveTypeKind.Byte =>  this.Random.Next(10),
                EdmPrimitiveTypeKind.Date => new Date(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, DateTimeOffset.UtcNow.Day),
                EdmPrimitiveTypeKind.DateTimeOffset => DateTimeOffset.UtcNow,
                EdmPrimitiveTypeKind.Decimal => this.NextDouble(10.0),
                EdmPrimitiveTypeKind.Single => this.NextDouble(10.0),
                EdmPrimitiveTypeKind.Double => this.NextDouble(10.0),
                EdmPrimitiveTypeKind.Int16 => this.Random.Next(10),
                EdmPrimitiveTypeKind.Int32 => this.Random.Next(10),
                EdmPrimitiveTypeKind.Int64 => this.Random.Next(10),
                EdmPrimitiveTypeKind.Duration => TimeSpan.FromHours(this.NextDouble(10.0)),
                EdmPrimitiveTypeKind.String when p.Name.Equals("id", StringComparison.OrdinalIgnoreCase) => $"id{this.MonotonicId++}",
                EdmPrimitiveTypeKind.String => $"{p.Name}-{this.GetPropertyTag(p)}",
                _ => throw new InvalidOperationException("Unknown primitive type."),

            };
        }

        private object GetExamplePrimitiveValueArray(IEdmStructuralProperty p)
        {
            var now = DateTimeOffset.UtcNow;
            return new ODataCollectionValue {Items = ((IEdmPrimitiveType)p.Type.Definition.AsElementType()).PrimitiveKind switch
            {
                EdmPrimitiveTypeKind.Boolean => new object[]{ true, false}.AsEnumerable(),
                EdmPrimitiveTypeKind.Byte =>  new object[]{this.Random.Next(10), this.Random.Next(10)},
                EdmPrimitiveTypeKind.Date => new object[]{ new Date(now.Year, now.Month, now.Day), new Date(now.Year, now.Month, now.Day)},
                EdmPrimitiveTypeKind.DateTimeOffset =>new object[]{now, now},
                EdmPrimitiveTypeKind.Decimal => new object[]{ this.NextDouble(10.0), this.NextDouble(10.0)},
                EdmPrimitiveTypeKind.Single => new object[]{ this.NextDouble(10.0), this.NextDouble(10.0)},
                EdmPrimitiveTypeKind.Double => new object[]{ this.NextDouble(10.0), this.NextDouble(10.0)},
                EdmPrimitiveTypeKind.Int16 => new object[]{this.Random.Next(10), this.Random.Next(10)},
                EdmPrimitiveTypeKind.Int32 => new object[]{this.Random.Next(10), this.Random.Next(10)},
                EdmPrimitiveTypeKind.Int64 => new object[]{this.Random.Next(10), this.Random.Next(10)},
                EdmPrimitiveTypeKind.Duration => new object[]{TimeSpan.FromHours(this.NextDouble(10.0)), TimeSpan.FromHours(this.NextDouble(10.0))},
                EdmPrimitiveTypeKind.String => new object[]{$"{p.Name}-{this.GetPropertyTag(p)}", $"{p.Name}-{this.GetPropertyTag(p)}"},
                _ => throw new InvalidOperationException("Unknown primitive type."),
            }};
        }

        private object GetSuppliedStructuralValue(IEdmStructuralProperty p, string suppliedValue)
        {

            if (p.Type.IsCollection())
            {
                throw new InvalidOperationException("Arrays not yet supported.");
            }
            else
            {
                if (p.Type.IsEnum())
                {

                    return new ODataEnumValue(suppliedValue);
                }
                else
                {
                    return this.GetSuppliedScalarPrimitiveValue(p, suppliedValue);
                }
            }
        }

        private object GetSuppliedScalarPrimitiveValue(IEdmStructuralProperty p, string suppliedValue)
        {
            try
            {
                bool isDateTimeOffset = DateTimeOffset.TryParse(suppliedValue, out DateTimeOffset o);
                return ((IEdmPrimitiveType) p.Type.Definition.AsElementType()).PrimitiveKind switch
                {
                    EdmPrimitiveTypeKind.Boolean => bool.Parse(suppliedValue),
                    EdmPrimitiveTypeKind.Byte => byte.Parse(suppliedValue),
                    EdmPrimitiveTypeKind.Date when isDateTimeOffset => new Date(o.Year, o.Month, o.Day),
                    EdmPrimitiveTypeKind.DateTimeOffset when isDateTimeOffset => o,
                    EdmPrimitiveTypeKind.Decimal => decimal.Parse(suppliedValue),
                    EdmPrimitiveTypeKind.Single => float.Parse(suppliedValue),
                    EdmPrimitiveTypeKind.Double => double.Parse(suppliedValue),
                    EdmPrimitiveTypeKind.Int16 => short.Parse(suppliedValue),
                    EdmPrimitiveTypeKind.Int32 => int.Parse(suppliedValue),
                    EdmPrimitiveTypeKind.Int64 => long.Parse(suppliedValue),
                    EdmPrimitiveTypeKind.Duration => TimeSpan.Parse(suppliedValue),
                    EdmPrimitiveTypeKind.String => suppliedValue,
                    _ => throw new InvalidOperationException("Unknown primitive type."),

                };
            }
            catch (Exception)
            {
                throw new InvalidOperationException($"Value {suppliedValue} supplied for property {p.Name} can't be converted to the property type {p.Type.ShortQualifiedName()}.");
            }
        }

        private double NextDouble(double maxValue)
        {
            return this.Random.NextDouble() * maxValue;
        }

        private string GetPropertyTag(IEdmStructuralProperty p)
        {
            if (!this.MonotonicPropertyValueTags.TryGetValue(p.Name, out int tag))
            {
                tag = 1;
            }

            this.MonotonicPropertyValueTags[p.Name] = tag + 1;

            return tag < 2 ? "value" : $"value{tag}";
        }
    }
}