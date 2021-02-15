using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Vocabularies;
using Microsoft.OData.UriParser;

namespace ODataExampleGenerator
{
    internal static class ODataExtensions
    {
        private const string OrgODataCoreV1Computed = "Org.OData.Core.V1.Computed";
        private const string OrgODataCapabilitiesV1NavigationRestrictions = "Org.OData.Capabilities.V1.NavigationRestrictions";

        public static IEnumerable<T> FilterReadOnly<T>(this IEnumerable<IEdmProperty> properties, ODataPath pathToProperties, GenerationParameters parameters)
            where T : IEdmProperty
        {
            // Get a path <from> the host/first term, by dropping the first segment.
            string GetPropertyPathExpression(IEdmNavigationProperty p)
            {
                var segments = new List<ODataPathSegment>(pathToProperties.Skip(1));
                // Add a typecast if the property does not live on the base type of the container.
                if (segments.Last().EdmType.AsElementType() != p.DeclaringType.AsElementType() &&
                    ((IEdmStructuredType)p.DeclaringType.AsElementType()).InheritsFrom((IEdmStructuredType)segments.Last().EdmType.AsElementType()))
                {
                    segments.Add(new TypeSegment(p.DeclaringType, p.DeclaringType, null));
                }
                segments.Add(new NavigationPropertySegment(p, null));
                var propPath = new ODataPath(segments);
                return PathSegmentToPathExpressionTranslator.GetPathExpression(
                    propPath,
                    parameters).TrimStart('/');
            }

            IEdmVocabularyAnnotatable root =
                (pathToProperties.FirstSegment as EntitySetSegment)?.EntitySet ??
                (pathToProperties.FirstSegment as SingletonSegment)?.Singleton as IEdmVocabularyAnnotatable;
            if (root == null)
            {
                throw new InvalidOperationException("Path root is neither an entitySet nor a singleton.");
            }

            return properties.Where(p =>
            {
                // Have to leave keys in otherwise serialization fails - strip keys out later.
                if (p.IsKey())
                {
                    return true;
                }

                var readOnly =
                    // Structural Property has a Computed annotation of its own
                    p is IEdmStructuralProperty &&
                    p.VocabularyAnnotations(parameters.Model).Any(a =>
                        a.Term.FullName().Equals(OrgODataCoreV1Computed, StringComparison.OrdinalIgnoreCase) &&
                        IsBooleanExpressionWithValue(a.Value, true)
                    ) || 
                    p is IEdmNavigationProperty navProp &&
                    HasReadOnlyNavigationRestriction(root.VocabularyAnnotations(parameters.Model), GetPropertyPathExpression(navProp)); // Root has an annotation targeting the property.

                return !readOnly;
            }).Cast<T>();
        }

        /// <summary>
        /// Check for a navigation restriction annotation on a root nav source.
        /// </summary>
        /// <param name="rootAnnotations"></param>
        /// <param name="restrictedPath">The path that is specified for the navigation restriction.</param>
        /// <returns>Whether the restriction is present.</returns>
        private static bool HasReadOnlyNavigationRestriction(
            IEnumerable<IEdmVocabularyAnnotation> rootAnnotations,
            string restrictedPath) =>
            rootAnnotations.Any(annotation =>
                annotation.Term.FullName().EqualsOic(OrgODataCapabilitiesV1NavigationRestrictions) &&
                annotation.Value.IsRecordWithProperty<IEdmCollectionExpression>("RestrictedProperties", restrictedPropertiesCollection=>
                    restrictedPropertiesCollection.Elements.Any(restrictedPropertiesCollectionElement =>
                        restrictedPropertiesCollectionElement.IsRecordWithProperty<IEdmPathExpression>("NavigationProperty", navigationProperty => 
                            navigationProperty.Path.EqualsOic(restrictedPath)) &&
                        restrictedPropertiesCollectionElement.IsRecordWithProperty<IEdmRecordExpression>("InsertRestrictions", insertRestrictions => 
                            insertRestrictions.IsRecordWithProperty<IEdmBooleanConstantExpression>("Insertable", insertable => !insertable.Value)
                        )
                    )
                )
            );

        private static bool IsRecordWithProperty<TProperty>(
            this IEdmExpression value,
            string name,
            Func<TProperty, bool> propertyCondition)
        {
            return value is IEdmRecordExpression record &&
                   record.Properties.Any(p =>
                       p.Name.EqualsOic(name) &&
                       p.Value is TProperty property &&
                       propertyCondition(property));
        }

        public static bool EqualsOic(this string theString, string value) => theString.Equals(value, StringComparison.OrdinalIgnoreCase);

        public static ODataPath ConcatenateSegment(this ODataPath pathToProperties, IEdmNavigationProperty p) =>
            new ODataPath(pathToProperties.Concat(Enumerable.Repeat(
                new NavigationPropertySegment(p, null), 1)));

        public static ODataPath ConcatenateSegment(this ODataPath pathToProperties, IEdmStructuralProperty p) =>
            new ODataPath(pathToProperties.Concat(Enumerable.Repeat(
                new PropertySegment(p), 1)));

        public static ODataPath ConcatenateSegment(this ODataPath pathToProperties, IEdmProperty p) =>
            p is IEdmNavigationProperty navProp ?
                pathToProperties.ConcatenateSegment(navProp) : 
                pathToProperties.ConcatenateSegment((IEdmStructuralProperty)p);

        public static IEnumerable<IEdmStructuralProperty> FilterSpecialProperties(this IEnumerable<IEdmStructuralProperty> properties) =>
            properties.Where(p =>
                !(p.Type.Definition.AsElementType() is IEdmPrimitiveType primitive
                  && primitive.PrimitiveKind == EdmPrimitiveTypeKind.Stream));

        public static T GetAnnotationValue<T>(
            this IEdmProperty property,
            IEdmModel model,
            string term)
            where T : class, IEdmExpression =>
            property.VocabularyAnnotations(model).FirstOrDefault(a =>
                a.Term.FullName().Equals(term, StringComparison.OrdinalIgnoreCase))?.Value as T;

        private static bool IsBooleanExpressionWithValue(IEdmExpression expression, bool value) =>
            expression is IEdmBooleanConstantExpression boolExpression && boolExpression.Value == value;
    }
}
      