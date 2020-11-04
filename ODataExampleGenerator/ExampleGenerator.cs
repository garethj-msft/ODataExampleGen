using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace ODataExampleGenerator
{
    public class ExampleGenerator
    {
        private readonly GenerationParameters generationParameters;
        private readonly ValueGenerator valueGenerator;

        public ExampleGenerator(GenerationParameters generationParameters)
        {
            this.generationParameters = generationParameters;
            this.valueGenerator = new ValueGenerator(generationParameters);
        }
        public string CreateExample()
        {
            MemoryStream stream = new MemoryStream();

            var message = new InMemoryMessage {Stream = stream};

            var settings = new ODataMessageWriterSettings
            {
                Validations = ValidationKinds.All,
                ODataUri = new ODataUri
                {
                    ServiceRoot = this.generationParameters.ServiceRoot,
                    Path = this.generationParameters.Path
                }
            };

            // Get to start point of writer, using path.
            if (!(this.generationParameters.Path.LastSegment is NavigationPropertySegment finalNavPropSegment))
            {
                throw new InvalidOperationException("Path must end in navigation property.");
            }
            else
            {
                var writer = new ODataMessageWriter((IODataRequestMessage) message, settings, this.generationParameters.Model);

                IEdmProperty property = finalNavPropSegment.NavigationProperty;
                IEdmStructuredType propertyType = property.Type.Definition.AsElementType() as IEdmStructuredType;
                propertyType = this.ChooseDerivedStructuralTypeIfAny(propertyType, property.Name);
                ODataWriter resWriter =
                    writer.CreateODataResourceWriter(finalNavPropSegment.NavigationSource, propertyType);
                this.WriteResource(resWriter, propertyType);

                var output = PrettyPrint(stream);
                return output;
            }
        }

        /// <summary>
        /// Get a tidied up string representation of the raw OData bytes.
        /// </summary>
        private static string PrettyPrint(MemoryStream stream)
        {
            var skipProperties = new[] {"@odata.context", "id"};

            using JsonDocument doc = JsonDocument.Parse(stream.ToArray(),
                new JsonDocumentOptions
                    {AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 2000});
            var nodes = doc.RootElement.EnumerateObject()
                .Where(e => !skipProperties.Contains(e.Name, StringComparer.OrdinalIgnoreCase));

            // TODO: Recursively traverse the subnodes removing those too, then traverse doing the write.

            using var outStream = new MemoryStream();
            using (var outWriter = new Utf8JsonWriter(outStream, new JsonWriterOptions {Indented = true}))
            {
                outWriter.WriteStartObject();
                foreach (var node in nodes)
                {
                    node.WriteTo(outWriter);
                }
                outWriter.WriteEndObject();
            }

            outStream.Flush();
            string output = Encoding.UTF8.GetString(outStream.ToArray());
            return output;
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
            this.AddExamplePrimitiveStructuralProperties(rootOdr, structuredType.StructuralProperties());
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
            this.AddExamplePrimitiveStructuralProperties(rootOdr, structuredType.StructuralProperties());
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
            IEdmNavigationSource bindingsHost = this.generationParameters.Model.FindDeclaredNavigationSource(this.generationParameters.Path.FirstSegment.Identifier);

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
            IEdmNavigationSource rootTargetElement = this.generationParameters.Model.FindDeclaredNavigationSource(binding.Target.Path.PathSegments.First());

            IEdmType AdvanceCursor(IEdmType cursor, int currentSegment)
            {
                // Don't try and index past the end of the segments.
                if (currentSegment >= segmentsList.Length - 1)
                {
                    return cursor;
                }

                var structure = cursor.AsElementType() as IEdmStructuredType;
                IEdmNavigationProperty nextSegmentProp = structure.NavigationProperties()
                    .FirstOrDefault(p => p.Name.Equals(segmentsList[currentSegment + 1], StringComparison.OrdinalIgnoreCase));
                if (nextSegmentProp == null)
                {
                    throw new InvalidOperationException($"Error: bindingTarget '{binding.Target.Path.Path}' for {binding.NavigationProperty.Name} is erroneous");
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
                resWriter.WriteStart(new ODataNestedResourceInfo { Name = navProp.Name, IsCollection = isCollection });
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

        private IEdmStructuredType ChooseDerivedStructuralTypeIfAny(IEdmStructuredType propertyType, string propertyName)
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
            IEnumerable<IEdmStructuralProperty> properties)
        {
            properties = properties.FilterComputed<IEdmStructuralProperty>(this.generationParameters.Model);
            List<ODataProperty> odataProps = new List<ODataProperty>(
                properties.Where(p=> p.Type.Definition.AsElementType().TypeKind != EdmTypeKind.Complex).Select(p => this.valueGenerator.GetExamplePrimitiveProperty(p)));

            structuralResource.Properties = odataProps;
        }
    }
}