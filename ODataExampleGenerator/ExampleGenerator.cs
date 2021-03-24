// <copyright file="ExampleGenerator.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

namespace ODataExampleGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.OData;
    using Microsoft.OData.Edm;
    using Microsoft.OData.Edm.Vocabularies;
    using Microsoft.OData.UriParser;
    using ODataExampleGenerator.ComponentImplementations;
    using ODataExampleGenerator.ComponentInterfaces;

    internal enum UrlMode
    {
        Neutral,
        Single,
        Collection,
    }

    public class ExampleGenerator
    {
        private readonly GenerationParameters generationParameters;
        private ODataPath path;
        private SelectExpandClause selectExpand;
        private IHost host;

        public ExampleGenerator(GenerationParameters generationParameters)
        {
            this.generationParameters = generationParameters;
            IHostBuilder builder = Host.CreateDefaultBuilder();
            builder.ConfigureServices(this.ConfigureServices);
            this.host = builder.Build();
        }

        public string CreateExample(string incomingUri)
        {
            _ = this.generationParameters.Model ?? throw new InvalidOperationException(
                $"{nameof(GenerationParameters.Model)} must be populated before calling {nameof(CreateExample)}.");
            _ = this.generationParameters.ServiceRoot ?? throw new InvalidOperationException(
                $"{nameof(GenerationParameters.ServiceRoot)} must be populated before calling {nameof(CreateExample)}.");
            _ = incomingUri ??
                throw new InvalidOperationException(
                    $"{nameof(incomingUri)} must be populated before calling {nameof(CreateExample)}.");

            var parser = new ODataUriParser(
                this.generationParameters.Model,
                this.generationParameters.ServiceRoot,
                new Uri(incomingUri, UriKind.Relative));
            this.path = parser.ParsePath();
            this.selectExpand = parser.ParseSelectAndExpand();

            // Get to start point of writer, using path.
            if (!(this.path.LastSegment is NavigationPropertySegment ||
                  this.path.LastSegment is KeySegment ||
                  this.path.LastSegment is EntitySetSegment ||
                  this.path.LastSegment is SingletonSegment))
            {
                throw new InvalidOperationException("Path must end in an EntitySet, a Singleton, a navigation property or a key into a navigation property.");
            }

            IEdmNavigationSource source = this.path.LastSegment.GetNavigationSource();
            IEdmStructuredType propertyType = source.Type.AsElementType() as IEdmStructuredType;

            var runRules = this.generationParameters.HttpMethod switch
            {
                { } m when m == HttpMethod.Get => (urlMode: UrlMode.Neutral, responseStatus: "200 OK", requiresResponse: true, requiresRequest: false, forceResponseSingle: false),
                { } m when m == HttpMethod.Post => (urlMode: UrlMode.Collection, responseStatus: "201 CREATED", requiresResponse: true, requiresRequest: true, forceResponseSingle: true),
                { } m when m == HttpMethod.Delete => (urlMode: UrlMode.Single, responseStatus: "204 NO CONTENT", requiresResponse: false, requiresRequest: false, forceResponseSingle: false),
                { } m when m == HttpMethod.Patch => (urlMode: UrlMode.Single, responseStatus: "204 NO CONTENT", requiresResponse: false, requiresRequest: true, forceResponseSingle: false),
                { } m when m == HttpMethod.Put => (urlMode: UrlMode.Single, responseStatus: "204 NO CONTENT", requiresResponse: false, requiresRequest: true, forceResponseSingle: false),
                { } m => throw new InvalidOperationException($"Unsupported HTTP method {m}."),
            };

            if (runRules.urlMode == UrlMode.Collection &&
                this.path.LastSegment.EdmType.TypeKind != EdmTypeKind.Collection)
            {
                throw new InvalidOperationException($"HTTP method {this.generationParameters.HttpMethod} requires a collection-valued URL.");
            }

            if (runRules.urlMode == UrlMode.Single &&
                this.path.LastSegment.EdmType.TypeKind == EdmTypeKind.Collection)
            {
                throw new InvalidOperationException($"HTTP method {this.generationParameters.HttpMethod} requires a single-valued URL.");
            }

            StringBuilder output = new StringBuilder();
            output.AppendLine($"{this.generationParameters.HttpMethod} {incomingUri}");
            if (runRules.requiresRequest)
            {
                this.generationParameters.GenerationStyle = GenerationStyle.Request;
                var request = this.WritePayload(source, propertyType, true);
                output.AppendLine(request); 
            }
            output.AppendLine(runRules.responseStatus);
            if (runRules.requiresResponse)
            {
                this.generationParameters.GenerationStyle = GenerationStyle.Response;
                var response = this.WritePayload(source, propertyType, runRules.forceResponseSingle);
                output.AppendLine(response);
            }

            return output.ToString();
        }

        private string WritePayload(
            IEdmNavigationSource source,
            IEdmStructuredType propertyType, 
            bool forceSingle)
        {
            var settings = new ODataMessageWriterSettings
            {
                Validations = ValidationKinds.All,
                ODataUri = new ODataUri
                {
                    ServiceRoot = this.generationParameters.ServiceRoot,
                    Path = this.path
                }
            };

            using var stream = new MemoryStream();
            using var message = new InMemoryMessage {Stream = stream};

            using var writer = new ODataMessageWriter(
                (IODataRequestMessage) message,
                settings,
                this.generationParameters.Model);

            if (forceSingle || this.path.LastSegment.EdmType.TypeKind != EdmTypeKind.Collection)
            {
                ODataWriter resWriter =
                    writer.CreateODataResourceWriter(source, propertyType);
                this.WriteResource(resWriter, source, this.path);
            }
            else
            {
                IEdmEntitySetBase entitySet = source as IEdmEntitySetBase;
                ODataWriter resWriter =
                    writer.CreateODataResourceSetWriter(entitySet, propertyType);
                this.WriteResourceSet(resWriter, source, this.path);
            }

            var output = JsonPrettyPrinter.PrettyPrint(stream.ToArray(), this.generationParameters);
            return output;
        }

        private void WriteResource(
            ODataWriter resWriter,
            IEdmNavigationSource navSource,
            ODataPath pathToResource)
        {
            IEdmStructuredType structuredType = navSource.Type.AsElementType() as IEdmStructuredType;
            this.WriteResourceImpl(resWriter, pathToResource, structuredType, navSource.Name);
        }
        private void WriteResource(
            ODataWriter resWriter,
            IEdmStructuralProperty property,
            ODataPath pathToResource)
        {
            IEdmStructuredType structuredType = property.Type.Definition.AsElementType() as IEdmStructuredType;
            this.WriteResourceImpl(resWriter, pathToResource, structuredType, property.Name);
        }

        private void WriteResourceSet(
            ODataWriter resWriter,
            IEdmNavigationSource navSource,
            ODataPath pathToResources)
        {
            IEdmStructuredType structuredType = navSource.Type.AsElementType() as IEdmStructuredType;

            IEdmVocabularyAnnotatable annotatable = null;
            if (navSource is IEdmContainedEntitySet contained)
            {
                // SetSize might be constrained by an annotation on the navigation property or source on the nav property.
                annotatable = contained.NavigationProperty;
            }
            else if (navSource is IEdmEntitySet || navSource is IEdmSingleton)
            {
                annotatable = (IEdmVocabularyAnnotatable)navSource;
            }

            this.WriteResourceSetImpl(resWriter, pathToResources, structuredType, annotatable, navSource.Name);
        }

        private void WriteResourceSet(
            ODataWriter resWriter,
            IEdmStructuralProperty property,
            ODataPath pathToResources)
        {
            IEdmStructuredType structuredType = property.Type.Definition.AsElementType() as IEdmStructuredType;
            this.WriteResourceSetImpl(resWriter, pathToResources, structuredType, property, property.Name);
        }

        private void WriteResourceImpl(
            ODataWriter resWriter,
            ODataPath pathToResource,
            IEdmStructuredType structuredType,
            string sourceName)
        {
            structuredType = this.ChooseDerivedStructuralTypeIfAny(structuredType, sourceName);
            this.WriteResourceDetail(resWriter, pathToResource, structuredType);
        }

        private void WriteResourceSetImpl(
            ODataWriter resWriter,
            ODataPath pathToResources,
            IEdmStructuredType structuredType,
            IEdmVocabularyAnnotatable annotatable,
            string sourceName)
        {
            var propTypes = this.ChooseDerivedStructuralTypeList(structuredType, sourceName).ToList();

            long setSize = propTypes.LongCount();
            setSize = Math.Min(annotatable.GetAnnotationValue<IEdmIntegerConstantExpression>(this.generationParameters.Model, "Org.OData.Validation.V1.MaxItems")?.Value ?? setSize, setSize);

            var set = new ODataResourceSet();
            resWriter.WriteStart(set);

            for (long i = 0; i < setSize; i++)
            {
                int typeIndex = (int) (i < propTypes.LongCount() ? i : propTypes.LongCount() - 1);
                structuredType = propTypes[typeIndex];
                this.WriteResourceDetail(resWriter, pathToResources, structuredType);
            }

            resWriter.WriteEnd(); // ODataResourceSet
        }

        private void WriteResourceDetail(ODataWriter resWriter, ODataPath pathToResource, IEdmStructuredType structuredType)
        {
            var resource = new ODataResource
            {
                TypeName = structuredType.FullTypeName()
            };

            IEnumerable<IEdmStructuralProperty> structuralProperties = structuredType.StructuralProperties();
            IEnumerable<IEdmNavigationProperty> navigationProperties = structuredType.NavigationProperties();

            if (this.generationParameters.GenerationStyle == GenerationStyle.Request)
            {
                structuralProperties = structuralProperties.FilterReadOnly(pathToResource, this.generationParameters);
                navigationProperties = navigationProperties.FilterReadOnly(pathToResource, this.generationParameters);
            }

            // Materialize the lists before processing them.
            var fixedStructuralProperties = structuralProperties.ToList();
            var fixedNavigationProperties = navigationProperties.ToList();

            this.AddExamplePrimitiveStructuralProperties(
                resource,
                fixedStructuralProperties.Where(p =>
                    p.Type.Definition.AsElementType().TypeKind != EdmTypeKind.Complex),
                structuredType);

            resWriter.WriteStart(resource);

            this.WriteContainedResources(
                resWriter,
                fixedNavigationProperties.Where(p => p.ContainsTarget),
                pathToResource);

            this.WriteContainedResources(
                resWriter,
                fixedStructuralProperties.Where(p =>
                    p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Complex),
                pathToResource);

            if (this.generationParameters.GenerationStyle == GenerationStyle.Request)
            {
                this.WriteReferenceBindings(
                    resWriter,
                    fixedNavigationProperties.Where(p => !p.ContainsTarget));
            }

            resWriter.WriteEnd(); // ODataResource
        }

        private void WriteReferenceBindings(ODataWriter resWriter, IEnumerable<IEdmNavigationProperty> properties)
        {
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
                    // If there's no binding we can't determine what the link should be.
                    continue;
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
                    var valueGenerator = this.host.Services.GetRequiredService<IValueGenerator>();
                    uriBuilder.Append($"/id{valueGenerator.GetNextMonotonicId()}");
                }
            }

            var link = new ODataEntityReferenceLink
            {
                Url = new Uri(uriBuilder.ToString(), UriKind.Absolute)
            };
            return link;
        }

        private void WriteContainedResources<T>(
            ODataWriter resWriter,
            IEnumerable<T> properties,
            ODataPath pathToResources)
            where T : IEdmProperty
        {
            foreach (T property in properties)
            {
                ODataPath nestedPath = pathToResources.ConcatenateSegment(property);

                // Responses to POSTS must return everything that was sent in.
                if ((this.generationParameters.GenerationStyle == GenerationStyle.Response ||
                     this.generationParameters.HttpMethod != HttpMethod.Post) &&
                    property is IEdmNavigationProperty)
                {
                    bool shouldAutoExpand =
                        property.GetAnnotationValue<IEdmBooleanConstantExpression>(this.generationParameters.Model,
                            "Org.OData.Core.V1.AutoExpand")?.Value ?? false;
                    bool hasExplicitExpand = pathToResources == this.path &&  // Only work at initial level.
                        (this.selectExpand?.SelectedItems?.OfType<ExpandedNavigationSelectItem>()
                            .Any(i => i.PathToNavigationProperty.LastSegment.Identifier == nestedPath.LastSegment.Identifier) ?? false);
                    if (!(shouldAutoExpand || hasExplicitExpand))
                    {
                        continue;
                    }
                }

                bool isCollection = property.Type.IsCollection();
                resWriter.WriteStart(new ODataNestedResourceInfo {Name = property.Name, IsCollection = isCollection});

                if (!isCollection)
                {
                    if (property is IEdmStructuralProperty structural)
                    {
                        this.WriteResource(resWriter, structural, nestedPath);
                    }
                    else
                    {
                        this.WriteResource(resWriter, nestedPath.LastSegment.GetNavigationSource(), nestedPath);
                    }
                }
                else
                {
                    if (property is IEdmStructuralProperty structural)
                    {
                        this.WriteResourceSet(resWriter, structural, nestedPath);
                    }
                    else
                    {
                        this.WriteResourceSet(resWriter, nestedPath.LastSegment.GetNavigationSource(), nestedPath);
                    }
                }

                resWriter.WriteEnd(); // ODataNestedResourceInfo
            }
        }

        private IEdmStructuredType ChooseDerivedStructuralTypeIfAny(
            IEdmStructuredType propertyType,
            string propertyName)
        {
            var potentialTypes = this.ChooseDerivedStructuralTypeList(propertyType, propertyName).ToList();
            var valueGenerator = this.host.Services.GetRequiredService<IValueGenerator>();
            return potentialTypes[valueGenerator.GetNextRandom(potentialTypes.Count)];
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
            IEdmStructuredType hostType)
        {
            properties = properties.FilterSpecialProperties();
            if (this.generationParameters.HttpMethod == HttpMethod.Patch &&
                this.generationParameters.GenerationStyle == GenerationStyle.Request)
            {
                // Just pick one simple property (plus the id, which is required for serialization) to demonstrate patch.
                var single = properties.FirstOrDefault(p => !p.IsKey() && p.Type.IsPrimitive());
                var key = properties.FirstOrDefault(p => p.IsKey());
                properties = single != null ? Enumerable.Repeat(single, 1) : Enumerable.Empty<IEdmStructuralProperty>();
                if (key != null)
                {
                    properties = properties.Concat(Enumerable.Repeat(key, 1));
                }
            }

            var valueGenerator = this.host.Services.GetRequiredService<IValueGenerator>();
            var odataProps = new List<ODataProperty>(
                properties.Select(p => valueGenerator.GetExamplePrimitiveProperty(hostType, p)));

            structuralResource.Properties = odataProps;
        }

        /// <summary>
        /// Configure services that are used by the generation process.
        /// </summary>
        /// <param name="serviceCollection">The collection to add services to.</param>
        public void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<IIdProvider, GuidIdProvider>();
            serviceCollection.AddSingleton<IIdProvider, ExchangeIdProvider>();
            serviceCollection.AddSingleton<GenerationParameters>(_ => this.generationParameters);
            serviceCollection.AddSingleton<IValueGenerator, ValueGenerator>();
        }
    }
}