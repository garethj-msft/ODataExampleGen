using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;

namespace ODataExampleGenerator
{
    internal static class ODataExtensions
    {
        public static IEnumerable<T> FilterReadOnly<T>(this IEnumerable<IEdmProperty> properties, IEdmModel model)
            where T : IEdmProperty
        {
            return properties.Where(p =>
            {
                // Have to leave keys in otherwise serialization fails - strip keys out later.
                if (p.IsKey())
                {
                    return true;
                }
            
                IEdmVocabularyAnnotatable host = p.DeclaringType as IEdmVocabularyAnnotatable;
                var readOnly =
                    // Property has a Computed annotation of its own
                    p.VocabularyAnnotations(model).Any(a =>
                        a.Term.FullName().Equals("Org.OData.Core.V1.Computed", StringComparison.OrdinalIgnoreCase) &&
                        IsBooleanExpressionWithValue(a.Value, true)
                    )

                    || 

                    // Property's owner has a NavigationRestriction mentioning the property.
                    host.VocabularyAnnotations(model).Any(a =>
                        a.Term.FullName().Equals("Org.OData.Capabilities.V1.NavigationRestrictions",
                            StringComparison.OrdinalIgnoreCase) &&
                        a.Value is IEdmRecordExpression restrictedPropertiesRecord &&
                        restrictedPropertiesRecord.Properties.Any(pc =>
                            pc.Name.Equals("NavigationProperty", StringComparison.OrdinalIgnoreCase)
                            && pc.Value is IEdmPathExpression propertyPath
                            && propertyPath.Path.Equals(p.Name, StringComparison.OrdinalIgnoreCase) )
                        && restrictedPropertiesRecord.Properties.Any(pc =>
                            pc.Name.Equals("InsertRestrictions", StringComparison.OrdinalIgnoreCase)
                            && pc.Value is IEdmRecordExpression insertRestrictionsRecord
                            && insertRestrictionsRecord.Properties.Any(pcir =>
                                pcir.Name.Equals("Insertable", StringComparison.OrdinalIgnoreCase)
                                && IsBooleanExpressionWithValue(pc.Value, false)))
                    );
                return !readOnly;
            }).Cast<T>();
        }

        public static IEnumerable<IEdmStructuralProperty> FilterSpecialProperties(this IEnumerable<IEdmStructuralProperty> properties)
        {
            return properties.Where(p =>
            {
                // Drop Edm.Stream properties.
                return !(p.Type.Definition.AsElementType() is IEdmPrimitiveType primitive
                         && primitive.PrimitiveKind == EdmPrimitiveTypeKind.Stream);
            });
        }

        public static T GetAnnotationValue<T>(
            this IEdmProperty property,
            IEdmModel model,
            string term)
            where T : class, IEdmExpression
        {
            return property.VocabularyAnnotations(model).FirstOrDefault(a =>
                a.Term.FullName().Equals(term, StringComparison.OrdinalIgnoreCase))?.Value as T;
        }

        private static bool IsBooleanExpressionWithValue(IEdmExpression expression, bool value)
        {
            return expression is IEdmBooleanConstantExpression boolExpression && boolExpression.Value == value;
        }
    }
}
      