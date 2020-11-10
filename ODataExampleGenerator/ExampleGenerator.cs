﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace ODataExampleGenerator
{
    public class ExampleGenerator
    {
        private readonly GenerationParameters generationParameters;
        private readonly ValueGenerator valueGenerator;
        private ODataPath path;

        public ExampleGenerator(GenerationParameters generationParameters)
        {
            this.generationParameters = generationParameters;
            this.valueGenerator = new ValueGenerator(generationParameters);
        }

        public string CreateExample(string uriToPost)
        {
            _ = this.generationParameters.Model ?? throw new InvalidOperationException(
                $"{nameof(GenerationParameters.Model)} must be populated before calling {nameof(CreateExample)}.");
            _ = this.generationParameters.ServiceRoot ?? throw new InvalidOperationException(
                $"{nameof(GenerationParameters.ServiceRoot)} must be populated before calling {nameof(CreateExample)}.");
            _ = uriToPost ??
                throw new InvalidOperationException(
                    $"{nameof(uriToPost)} must be populated before calling {nameof(CreateExample)}.");

            var parser = new ODataUriParser(
                this.generationParameters.Model,
                this.generationParameters.ServiceRoot,
                new Uri(uriToPost, UriKind.Relative));
            this.path = parser.ParsePath();

            using var stream = new MemoryStream();
            using var message = new InMemoryMessage {Stream = stream};

            var settings = new ODataMessageWriterSettings
            {
                Validations = ValidationKinds.All,
                ODataUri = new ODataUri
                {
                    ServiceRoot = this.generationParameters.ServiceRoot,
                    Path = this.path
                }
            };

            // Get to start point of writer, using path.
            if (!(this.path.LastSegment is NavigationPropertySegment finalNavPropSegment))
            {
                throw new InvalidOperationException("Path must end in navigation property.");
            }
            else
            {
                using var writer = new ODataMessageWriter(
                    (IODataRequestMessage) message,
                    settings,
                    this.generationParameters.Model);

                IEdmProperty property = finalNavPropSegment.NavigationProperty;
                IEdmStructuredType propertyType = property.Type.Definition.AsElementType() as IEdmStructuredType;
                propertyType = this.ChooseDerivedStructuralTypeIfAny(propertyType, property.Name);
                ODataWriter resWriter =
                    writer.CreateODataResourceWriter(finalNavPropSegment.NavigationSource, propertyType);
                this.WriteResource(resWriter, propertyType);

                var output = JsonPrettyPrinter.PrettyPrint(stream.ToArray());
                return output;
            }
        }

        private void WriteResource(
            ODataWriter resWriter,
            IEdmStructuredType structuredType)
        {
            var rootOdr = new ODataResource
            {
                TypeName = structuredType.FullTypeName(),
                TypeAnnotation = new ODataTypeAnnotation(structuredType.FullTypeName())
            };

            this.AddExamplePrimitiveStructuralProperties(rootOdr, structuredType.StructuralProperties(),
                structuredType);
            resWriter.WriteStart(rootOdr);
            this.WriteContainedResources(resWriter,
                structuredType.NavigationProperties().Where(p => p.ContainsTarget));
            this.WriteContainedResources(resWriter,
                structuredType.StructuralProperties().Where(p =>
                    p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Complex));
            this.WriteReferenceBindings(resWriter,
                structuredType.NavigationProperties().Where(p => !p.ContainsTarget));
            resWriter.WriteEnd(); // ODataResource
        }

        private void WriteResourceSet(
            ODataWriter resWriter,
            IEdmStructuredType structuredType)
        {
            var set = new ODataResourceSet();
            var rootOdr = new ODataResource
            {
                TypeName = structuredType.FullTypeName()
            };
            this.AddExamplePrimitiveStructuralProperties(rootOdr, structuredType.StructuralProperties(),
                structuredType);
            resWriter.WriteStart(set);
            for (int i = 0; i < 2; i++)
            {
                resWriter.WriteStart(rootOdr);
                this.WriteContainedResources(resWriter,
                    structuredType.NavigationProperties().Where(p => p.ContainsTarget));
                this.WriteContainedResources(resWriter,
                    structuredType.StructuralProperties().Where(p =>
                        p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Complex));
                this.WriteReferenceBindings(resWriter,
                    structuredType.NavigationProperties().Where(p => !p.ContainsTarget));
                resWriter.WriteEnd(); // ODataResource
            }

            resWriter.WriteEnd(); // ODataResourceSet
        }

        private void WriteReferenceBindings(
            ODataWriter resWriter,
            IEnumerable<IEdmNavigationProperty> properties)
        {
            properties = properties.FilterComputed<IEdmNavigationProperty>(this.generationParameters.Model);

            // For each property, build URL to the nav prop based on the nav prop binding in the entitySet.
            // to find the necessary nav prop bindings, we need to look under the root container (es or singleton) that the call is being made to.
            IEdmNavigationSource bindingsHost =
                this.generationParameters.Model.FindDeclaredNavigationSource(this.path.FirstSegment.Identifier);

            foreach (IEdmNavigationProperty navProp in properties)
            {
                bool isCollection = navProp.Type.IsCollection();
                var binding = bindingsHost.FindNavigationPropertyBindings(navProp).FirstOrDefault();
                if (binding == null)
                {
                    throw new InvalidOperationException($"Error: No bindingPath found for {navProp.Name}.");
                }

                resWriter.WriteStart(new ODataNestedResourceInfo {Name = navProp.Name, IsCollection = isCollection});


                for (int i = 0; i < (navProp.Type.IsCollection() ? 2 : 1); i++)
                {
                    var link = this.ConstructEntityReferenceLink(binding);
                    resWriter.WriteEntityReferenceLink(link);
                }

                resWriter.WriteEnd(); // ODataNestedResourceInfo
            }
        }

        /// <summary>
        /// Create a reference link using the target of the binding to create the Url.
        /// </summary>
        private ODataEntityReferenceLink ConstructEntityReferenceLink(
            IEdmNavigationPropertyBinding binding)
        {
            string[] segmentsList = binding.Target.Path.PathSegments.ToArray();

            // Walk along the path in the target of the binding.
            IEdmNavigationSource rootTargetElement =
                this.generationParameters.Model.FindDeclaredNavigationSource(binding.Target.Path.PathSegments.First());

            IEdmType AdvanceCursor(IEdmType cursor, int currentSegment)
            {
                // Don't try and index past the end of the segments.
                if (currentSegment >= segmentsList.Length - 1)
                {
                    return cursor;
                }

                var structure = cursor.AsElementType() as IEdmStructuredType;
                IEdmNavigationProperty nextSegmentProp = structure.NavigationProperties()
                    .FirstOrDefault(p =>
                        p.Name.Equals(segmentsList[currentSegment + 1], StringComparison.OrdinalIgnoreCase));
                if (nextSegmentProp == null)
                {
                    throw new InvalidOperationException(
                        $"Error: bindingTarget '{binding.Target.Path.Path}' for {binding.NavigationProperty.Name} is erroneous");
                }

                return nextSegmentProp.Type.Definition;
            }

            var uriBuilder = new StringBuilder(this.generationParameters.ServiceRoot.AbsoluteUri.TrimEnd('/'));

            // Cursor through the types that make up the binding's target path.
            IEdmType targetCursor = rootTargetElement.Type;
            for (int segment = 0; segment < segmentsList.Length; targetCursor = AdvanceCursor(targetCursor, segment++))
            {
                uriBuilder.Append($"/{segmentsList[segment]}");
                if (targetCursor is IEdmCollectionType)
                {
                    uriBuilder.Append($"/id{this.valueGenerator.MonotonicId++}");
                }
            }

            var link = new ODataEntityReferenceLink
            {
                Url = new Uri(uriBuilder.ToString(), UriKind.Absolute)
            };
            return link;
        }

        private void WriteContainedResources(
            ODataWriter resWriter,
            IEnumerable<IEdmProperty> properties)
        {
            properties = properties.FilterComputed<IEdmProperty>(this.generationParameters.Model);

            foreach (IEdmProperty navProp in properties)
            {
                bool isCollection = navProp.Type.IsCollection();
                resWriter.WriteStart(new ODataNestedResourceInfo {Name = navProp.Name, IsCollection = isCollection});
                IEdmStructuredType propertyType = navProp.Type.Definition.AsElementType() as IEdmStructuredType;
                propertyType = this.ChooseDerivedStructuralTypeIfAny(propertyType, navProp.Name);
                if (!isCollection)
                {
                    this.WriteResource(resWriter, propertyType);
                }
                else
                {
                    this.WriteResourceSet(resWriter, propertyType);
                }

                resWriter.WriteEnd(); // ODataNestedResourceInfo
            }
        }

        private IEdmStructuredType ChooseDerivedStructuralTypeIfAny(
            IEdmStructuredType propertyType,
            string propertyName)
        {
            var potentialTypes = this.generationParameters.Model.FindAllDerivedTypes(propertyType).ToList();
            if (potentialTypes.Count > 0)
            {
                // Must pick a type.
                potentialTypes.Add(propertyType);

                if (!this.generationParameters.ChosenTypes.TryGetValue(propertyName, out propertyType))
                {
                    var concreteTypes = potentialTypes.Where(t => !t.IsAbstract).ToList();
                    propertyType = concreteTypes[this.valueGenerator.Random.Next(concreteTypes.Count)];
                }
            }

            return propertyType;
        }

        private void AddExamplePrimitiveStructuralProperties(
            ODataResource structuralResource,
            IEnumerable<IEdmStructuralProperty> properties,
            IEdmStructuredType hostType)
        {
            properties = properties.Where(p => p.Type.Definition.AsElementType().TypeKind != EdmTypeKind.Complex)
                .FilterComputed<IEdmStructuralProperty>(this.generationParameters.Model);

            List<ODataProperty> odataProps = new List<ODataProperty>(
                properties.Select(p => this.valueGenerator.GetExamplePrimitiveProperty(hostType, p)));

            structuralResource.Properties = odataProps;
        }
    }
}