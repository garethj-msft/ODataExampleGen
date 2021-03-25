// <copyright file="PathSegmentToPathExpressionTranslator.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

namespace ODataExampleGenerator
{
    using System;
    using Microsoft.OData.Edm;
    using Microsoft.OData.UriParser;

    /// <summary>
    /// Translator to translate query url path segments to strings.
    /// </summary>
    internal sealed class PathSegmentToPathExpressionTranslator : PathSegmentTranslator<string>
    {
        /// <summary>
        /// Private constructor as static method GetPathExpression is the API to this class.
        /// </summary>
        /// <param name="parameters"></param>
        private PathSegmentToPathExpressionTranslator()
        {
        }

        /// <summary>Translate a TypeSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer</returns>
        public override string Translate(TypeSegment segment)
        {
            IEdmType type = segment.EdmType;
            if (type is IEdmCollectionType edmCollectionType)
            {
                type = edmCollectionType.ElementType.Definition;
            }

            return "/" + type.FullTypeName();
        }

        /// <summary>Translate a NavigationPropertySegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(NavigationPropertySegment segment)
        {
            return "/" + segment.NavigationProperty.Name;
        }

        /// <summary>Translate an EntitySetSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(EntitySetSegment segment)
        {
            return "/" + segment.EntitySet.Name;
        }

        /// <summary>Translate an SingletonSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(SingletonSegment segment)
        {
            return "/" + segment.Singleton.Name;
        }

        /// <summary>Translate a KeySegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(KeySegment segment)
        {
            return string.Empty;
        }

        /// <summary>Translate a PropertySegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(PropertySegment segment)
        {
            return "/" + segment.Property.Name;
        }

        /// <summary>Translate an AnnotationSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(AnnotationSegment segment)
        {
            return "/" + segment.Term.FullName();
        }

        /// <summary>Translate an OperationSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(OperationSegment segment)
        {
            throw new NotImplementedException();
        }

        /// <summary>Translate an OperationImportSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(OperationImportSegment segment)
        {
            throw new NotImplementedException();
        }

        /// <summary>Translate an OpenPropertySegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(DynamicPathSegment segment)
        {
            return "/" + segment.Identifier;
        }

        /// <summary>Translate a CountSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(CountSegment segment)
        {
            return "/" + segment.Identifier;
        }

        /// <summary>Translate a FilterSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(FilterSegment segment)
        {
            return "/" + segment.LiteralText;
        }

        /// <summary>Translate a ReferenceSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(ReferenceSegment segment)
        {
            return "/" + segment.Identifier;
        }

        /// <summary>Translate an EachSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(EachSegment segment)
        {
            return "/" + segment.Identifier;
        }

        /// <summary>Visit a NavigationPropertyLinkSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(NavigationPropertyLinkSegment segment)
        {
            return "/" + segment.NavigationProperty.Name + "/" + "$ref";
        }

        /// <summary>Translate a ValueSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(ValueSegment segment)
        {
            return "/" + segment.Identifier;
        }

        /// <summary>Translate a BatchSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(BatchSegment segment)
        {
            return "/" + segment.Identifier;
        }

        /// <summary>Translate a BatchReferenceSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(BatchReferenceSegment segment)
        {
            return "/" + segment.ContentId;
        }

        /// <summary>Translate a MetadataSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(MetadataSegment segment)
        {
            return "/" + segment.Identifier;
        }

        /// <summary>Translate a PathTemplateSegment</summary>
        /// <param name="segment">the segment to Translate</param>
        /// <returns>Defined by the implementer.</returns>
        public override string Translate(PathTemplateSegment segment)
        {
            return "/" + segment.Identifier;
        }

        /// <summary>
        /// Public interface to this class.
        /// </summary>
        /// <param name="pathToProperties"></param>
        /// 
        /// <returns></returns>
        public static string GetPathExpression(ODataPath pathToProperties)
        {
            var translator = new PathSegmentToPathExpressionTranslator();
            System.Collections.Generic.IEnumerable<string> pathExpressionArray = pathToProperties.WalkWith(translator);
            string pathExpression = string.Join(string.Empty, pathExpressionArray);
            return pathExpression;
        }
    }
}
