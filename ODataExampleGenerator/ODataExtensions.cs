using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;

namespace ODataExampleGenerator
{
    public static class ODataExtensions
    {
        public static IEnumerable<T> FilterComputed<T>(this IEnumerable<IEdmProperty> properties, IEdmModel model)
            where T : IEdmProperty
        {
            return properties.Where(p =>
            {
                // Have to leave keys in otherwise serialization fails - strip those later.
                var computed = !ExtensionMethods.IsKey(p) && ExtensionMethods.VocabularyAnnotations(p, model).Any(a =>
                    ExtensionMethods.FullName((IEdmSchemaElement) a.Term).Equals("Org.OData.Core.V1.Computed", StringComparison.OrdinalIgnoreCase) &&
                    a.Value is IEdmBooleanConstantExpression boolExpression &&
                    boolExpression.Value);
                return !computed;
            }).Cast<T>();
        }
    }
}
