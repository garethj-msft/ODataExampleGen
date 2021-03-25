// <copyright file="ValueGenerator.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData;
using Microsoft.OData.Edm;
using ODataExampleGenerator.ComponentInterfaces;

namespace ODataExampleGenerator.ComponentImplementations
{
    internal class ValueGenerator : IValueGenerator
    {
        private int MonotonicId { get; set; } = 1;

        private Random Random { get; } = new Random();

        private GenerationParameters GenerationParameters { get; }
        
        private IEnumerable<IIdProvider> IdProviders { get; }
        
        private Dictionary<string, int> MonotonicPropertyValueTags { get; } = new Dictionary<string, int>();

        private Dictionary<int, string> IdValues { get; } = new Dictionary<int, string>();

        public ValueGenerator(
            GenerationParameters generationParameters,
            IEnumerable<IIdProvider> idProviders)
        {
            this.GenerationParameters = generationParameters;
            this.IdProviders = idProviders;
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
                    string usefulMember = this.GetExampleEnumValue(p);
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
                EdmPrimitiveTypeKind.Decimal => (decimal)this.NextDouble(10.0),
                EdmPrimitiveTypeKind.Single => (float)this.NextDouble(10.0),
                EdmPrimitiveTypeKind.Double => this.NextDouble(10.0),
                EdmPrimitiveTypeKind.Int16 => (short)this.Random.Next(10),
                EdmPrimitiveTypeKind.Int32 => this.Random.Next(10),
                EdmPrimitiveTypeKind.Int64 => (long)this.Random.Next(10),
                EdmPrimitiveTypeKind.Duration => TimeSpan.FromHours(this.NextDouble(10.0)),
                EdmPrimitiveTypeKind.String when this.PropertyIsId(p) => this.GetIdValue(p, this.MonotonicId++),
                EdmPrimitiveTypeKind.String => $"{p.Name}-{this.GetPropertyTag(p)}",
                _ => throw new InvalidOperationException($"Unknown primitive type '{((IEdmPrimitiveType)p.Type.Definition.AsElementType()).PrimitiveKind}'."),

            };
        }

        private object GetExamplePrimitiveValueArray(IEdmStructuralProperty p)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new ODataCollectionValue {Items = ((IEdmPrimitiveType)p.Type.Definition.AsElementType()).PrimitiveKind switch
            {
                EdmPrimitiveTypeKind.Boolean => new object[]{ true, false}.AsEnumerable(),
                EdmPrimitiveTypeKind.Byte =>  new object[]{this.Random.Next(10), this.Random.Next(10)},
                EdmPrimitiveTypeKind.Date => new object[]{ new Date(now.Year, now.Month, now.Day), new Date(now.Year, now.Month, now.Day)},
                EdmPrimitiveTypeKind.DateTimeOffset =>new object[]{now, now},
                EdmPrimitiveTypeKind.Decimal => new object[]{ (decimal)this.NextDouble(10.0), (decimal)this.NextDouble(10.0)},
                EdmPrimitiveTypeKind.Single => new object[]{ (float)this.NextDouble(10.0), (float)this.NextDouble(10.0)},
                EdmPrimitiveTypeKind.Double => new object[]{ this.NextDouble(10.0), this.NextDouble(10.0)},
                EdmPrimitiveTypeKind.Int16 => new object[]{(short)this.Random.Next(10), (short)this.Random.Next(10)},
                EdmPrimitiveTypeKind.Int32 => new object[]{this.Random.Next(10), this.Random.Next(10)},
                EdmPrimitiveTypeKind.Int64 => new object[]{(long)this.Random.Next(10), (long)this.Random.Next(10)},
                EdmPrimitiveTypeKind.Duration => new object[]{TimeSpan.FromHours(this.NextDouble(10.0)), TimeSpan.FromHours(this.NextDouble(10.0))},
                EdmPrimitiveTypeKind.String => new object[]{$"{p.Name}-{this.GetPropertyTag(p)}", $"{p.Name}-{this.GetPropertyTag(p)}"},
                _ => throw new InvalidOperationException("Unknown primitive type '{((IEdmPrimitiveType)p.Type.Definition.AsElementType()).PrimitiveKind}'."),
            }};
        }

        private bool PropertyIsId(IEdmStructuralProperty p)
        {
            return p.Name.Equals("id", StringComparison.Ordinal) || p.Name.EndsWith("Id", StringComparison.Ordinal);
        }

        private string GetIdValue(IEdmStructuralProperty p, int monotonicId)
        {
            // TODO: align the id that came in for an individual GET call with the *first* id value.
            if (!this.IdValues.TryGetValue(monotonicId, out string idValue))
            {
                if (!this.GenerationParameters.ChosenIdProviders.TryGetValue(p.Name, out string providerName))
                {
                    _ = this.GenerationParameters.ChosenIdProviders.TryGetValue("@default", out providerName);
                }

                IIdProvider idProvider = (!string.IsNullOrWhiteSpace(providerName) ? this.IdProviders.SingleOrDefault(p => p.Name.EqualsOic(providerName)) : null) ?? this.IdProviders.First();
                {
                    idValue = idProvider.GetNewId();
                }
                this.IdValues[monotonicId] = idValue;
            }
            return idValue;
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

        public int GetNextRandom(int scope)
        {
            return this.Random.Next(scope);
        }

        public int GetNextMonotonicId()
        {
            return this.MonotonicId++;
        }

        public string GetNextId(string provider)
        {
            throw new NotImplementedException();
        }
    }
}