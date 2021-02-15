namespace ODataExampleGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.OData;
    using Microsoft.OData.Edm;
    using Microsoft.OData.Edm.Vocabularies;
    using Microsoft.OData.UriParser;

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

            using var writer = new ODataMessageWriter(
                (IODataRequestMessage) message,
                settings,
                this.generationParameters.Model);

            IEdmProperty property = finalNavPropSegment.NavigationProperty;
            IEdmStructuredType propertyType = property.Type.Definition.AsElementType() as IEdmStructuredType;
            if (!property.Type.IsCollection())
            {
                ODataWriter resWriter =
                    writer.CreateODataResourceWriter(finalNavPropSegment.NavigationSource, propertyType);
                this.WriteResource(resWriter, property, this.path);
            }
            else
            {
                IEdmEntitySetBase entitySet = new EdmEntitySet(this.generationParameters.Model.EntityContainer, this.path.FirstSegment.Identifier, (IEdmEntityType)propertyType);
                ODataWriter resWriter =
                    writer.CreateODataResourceSetWriter(entitySet, propertyType);
                this.WriteResourceSet(resWriter, property, this.path);
            }

            var output = JsonPrettyPrinter.PrettyPrint(stream.ToArray(), this.generationParameters);
            return output;
        }

        private void WriteResource(
            ODataWriter resWriter,
            IEdmProperty sourceProperty,
            ODataPath pathToResource)
        {
            IEdmStructuredType structuredType = sourceProperty.Type.Definition.AsElementType() as IEdmStructuredType;
            structuredType = this.ChooseDerivedStructuralTypeIfAny(structuredType, sourceProperty.Name);

            var rootOdr = new ODataResource
            {
                TypeName = structuredType.FullTypeName(),
                TypeAnnotation = new ODataTypeAnnotation(structuredType.FullTypeName())
            };

            this.AddExamplePrimitiveStructuralProperties(rootOdr,
                structuredType.StructuralProperties(),
                structuredType,
                pathToResource);
            resWriter.WriteStart(rootOdr);
            this.WriteContainedResources(
                resWriter,
                structuredType.NavigationProperties().Where(p => p.ContainsTarget),
                pathToResource);

            this.WriteContainedResources(
                resWriter,
                structuredType.StructuralProperties().Where(p =>
                    p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Complex),
                pathToResource);
            if (this.generationParameters.GenerationStyle == GenerationStyle.Request)
            {
                this.WriteReferenceBindings(
                    resWriter,
                    structuredType.NavigationProperties().Where(p => !p.ContainsTarget),
                    pathToResource);
            }

            resWriter.WriteEnd(); // ODataResource
        }

        private void WriteResourceSet(
            ODataWriter resWriter,
            IEdmProperty sourceProperty,
            ODataPath pathToResources)
        {
            IEdmStructuredType structuredType = sourceProperty.Type.Definition.AsElementType() as IEdmStructuredType;
            var set = new ODataResourceSet();
            resWriter.WriteStart(set);

            var propTypes = this.ChooseDerivedStructuralTypeList(structuredType, sourceProperty.Name).ToList();
            long setSize = propTypes.LongCount();
            if (setSize == 1)
            {
                setSize++;  // Show an array even if there's only one type.
            }

            // SetSize might be constrained by an annotation on the navigation property or source on the nav property.
            setSize = Math.Min(sourceProperty.GetAnnotationValue<IEdmIntegerConstantExpression>(this.generationParameters.Model, "Org.OData.Validation.V1.MaxItems")?.Value ?? setSize, setSize);

            for (long i = 0; i < setSize; i++)
            {
                int typeIndex = (int)(i < propTypes.LongCount() ? i : propTypes.LongCount() - 1);
                structuredType = propTypes[typeIndex];
                var rootOdr = new ODataResource
                {
                    TypeName = structuredType.FullTypeName()
                };

                this.AddExamplePrimitiveStructuralProperties(
                    rootOdr,
                    structuredType.StructuralProperties(),
                    structuredType,
                    pathToResources);

                resWriter.WriteStart(rootOdr);
                this.WriteContainedResources(
                    resWriter,
                    structuredType.NavigationProperties().Where(p => p.ContainsTarget),
                    pathToResources);

                this.WriteContainedResources(
                    resWriter,
                    structuredType.StructuralProperties().Where(p =>
                        p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Complex),
                    pathToResources);
                if (this.generationParameters.GenerationStyle == GenerationStyle.Request)
                {
                    this.WriteReferenceBindings(
                        resWriter,
                        structuredType.NavigationProperties().Where(p => !p.ContainsTarget),
                        pathToResources);
                }

                resWriter.WriteEnd(); // ODataResource
            }

            resWriter.WriteEnd(); // ODataResourceSet
        }

        private void WriteReferenceBindings(
            ODataWriter resWriter,
            IEnumerable<IEdmNavigationProperty> properties,
            ODataPath pathToResources)
        {
            if (this.generationParameters.GenerationStyle == GenerationStyle.Request)
            {
                properties = properties.FilterReadOnly<IEdmNavigationProperty>(this.path, this.generationParameters);
            }

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
            IEnumerable<IEdmProperty> properties,
            ODataPath pathToResources)
        {
            if (this.generationParameters.GenerationStyle == GenerationStyle.Request)
            {
                properties = properties.FilterReadOnly<IEdmProperty>(this.path, this.generationParameters);
            }

            foreach (IEdmProperty property in properties)
            {
                if (this.generationParameters.GenerationStyle == GenerationStyle.Response &&
                    property is IEdmNavigationProperty)
                {
                    var shouldAutoExpand = property.GetAnnotationValue<IEdmBooleanConstantExpression>(this.generationParameters.Model, "Org.OData.Core.V1.AutoExpand");
                    if (shouldAutoExpand == null || !shouldAutoExpand.Value)
                    {
                        continue;
                    }
                }

                bool isCollection = property.Type.IsCollection();
                resWriter.WriteStart(new ODataNestedResourceInfo {Name = property.Name, IsCollection = isCollection});
                ODataPath nestedPath = pathToResources.ConcatenateSegment(property);

                if (!isCollection)
                {
                    this.WriteResource(resWriter, property, nestedPath);
                }
                else
                {
                    this.WriteResourceSet(resWriter, property, nestedPath);
                }

                resWriter.WriteEnd(); // ODataNestedResourceInfo
            }
        }

        private IEdmStructuredType ChooseDerivedStructuralTypeIfAny(
            IEdmStructuredType propertyType,
            string propertyName)
        {
            var potentialTypes = this.ChooseDerivedStructuralTypeList(propertyType, propertyName).ToList();
            return potentialTypes[this.valueGenerator.Random.Next(potentialTypes.Count)];
        }

        private IList<IEdmStructuredType> ChooseDerivedStructuralTypeList(
            IEdmStructuredType propertyType,
            string propertyName)
        {
            if (this.generationParameters.ChosenTypes.TryGetValue(propertyName, out var chosenType))
            {
                propertyType = chosenType;
            }
            else
            {
                var potentialTypes = this.generationParameters.Model.FindAllDerivedTypes(propertyType)
                    .Where(t => !t.IsAbstract).ToList();
                if (potentialTypes.Count > 0)
                {
                    if (!propertyType.IsAbstract)
                    {
                        potentialTypes.Add(propertyType);
                    }

                    return potentialTypes;
                }
            }

            return Enumerable.Repeat(propertyType, 1).ToList();
        }

        private void AddExamplePrimitiveStructuralProperties(
            ODataResource structuralResource,
            IEnumerable<IEdmStructuralProperty> properties,
            IEdmStructuredType hostType,
            ODataPath pathToProperties)
        {
            properties = properties.Where(p => p.Type.Definition.AsElementType().TypeKind != EdmTypeKind.Complex);
            if (this.generationParameters.GenerationStyle == GenerationStyle.Request)
            {
                properties = properties.FilterReadOnly<IEdmStructuralProperty>(pathToProperties, this.generationParameters);
            }
            properties = properties.FilterSpecialProperties();

            var odataProps = new List<ODataProperty>(
                properties.Select(p => this.valueGenerator.GetExamplePrimitiveProperty(hostType, p)));

            structuralResource.Properties = odataProps;
        }
    }
}