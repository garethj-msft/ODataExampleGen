using System;
using System.Linq;
using Microsoft.OData;
using Microsoft.OData.Edm;

namespace ODataExampleGenerator
{
    public class ValueGenerator
    {
        public int MonotonicId { get; set; } = 1;

        public Random Random { get; } = new Random();

        private GenerationParameters GenerationParameters { get; }

        public ValueGenerator(GenerationParameters generationParameters)
        {
            this.GenerationParameters = generationParameters;
        }
        public ODataProperty GetExamplePrimitiveProperty(IEdmStructuralProperty p)
        {
            if (p.Type.IsEnum())
            {
                string member;
                if (this.GenerationParameters.ChosenEnums.TryGetValue(p.Name, out IEdmEnumMember enumMember))
                {
                    member = enumMember.Name;
                }
                else
                {
                    var enumType = (IEdmEnumType) p.Type.Definition;
                    var usefulMembers = enumType.Members
                        .Where(m => !m.Name.Equals("unknownFutureValue", StringComparison.OrdinalIgnoreCase))
                        .Select(m => m.Name).ToList();
                    member = usefulMembers[this.Random.Next(usefulMembers.Count)];
                }

                return new ODataProperty
                    {Name = p.Name, Value = new ODataEnumValue(member)};
            }
            else
            {
                object primitive = this.GenerationParameters.ChosenPrimitives.TryGetValue(p.Name, out string primitiveString) ? this.GetSuppliedStructuralValue(p, primitiveString) : this.GetExampleStructuralValue(p);
                var returnProp = new ODataProperty
                    {Name = p.Name, PrimitiveTypeKind = p.Type.PrimitiveKind(), Value = primitive};
                return returnProp;
            }
        }

        private object GetExampleStructuralValue(IEdmStructuralProperty p)
        {
            if (p.Type.IsCollection())
            {
                return this.GetExamplePrimitiveValueArray(p);
            }
            else
            {
                return this.GetExampleScalarPrimitiveValue(p);
            }
        }

        private object GetSuppliedStructuralValue(IEdmStructuralProperty p, string suppliedValue)
        {
            if (p.Type.IsCollection())
            {
                throw new InvalidOperationException("Arrays not yet supported.");
                // return GetSuppliedPrimitiveValueArray(p, suppliedValue);
            }
            else
            {
                return this.GetSuppliedScalarPrimitiveValue(p, suppliedValue);
            }
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
                EdmPrimitiveTypeKind.String when p.Name.Equals("id", StringComparison.OrdinalIgnoreCase) => $"id{MonotonicId++}",
                EdmPrimitiveTypeKind.String => $"A sample {p.Name}",
                _ => throw new InvalidOperationException("Unknown primitive type."),

            };
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
                EdmPrimitiveTypeKind.String => new object[]{$"A sample of {p.Name}", $"Another sample of {p.Name}"},
                _ => throw new InvalidOperationException("Unknown primitive type."),
            }};
        }

        private double NextDouble(double maxValue)
        {
            return this.Random.NextDouble() * maxValue;
        }
    }
}