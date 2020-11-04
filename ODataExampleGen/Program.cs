using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CommandLine;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;

namespace ODataExampleGen
{
    class Program
    {
        private static ProgramOptions Options;

        private static readonly GenerationParameters GenerationParameters;

        private static readonly ValueGenerator ValueGenerator;

        static Program()
        {
            GenerationParameters = new GenerationParameters();
            ValueGenerator = new ValueGenerator(GenerationParameters);
        }

        public static int Main(string[] args)
        {
            int result = Parser.Default.ParseArguments<ProgramOptions>(args).MapResult(RunCommand, _ => 1);
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }

            return result;
        }

        private static int RunCommand(ProgramOptions options)
        {
            Options = options;
            if (!File.Exists(options.CsdlFile))
            {
                Console.WriteLine($"Unable to locate csdl file: {options.CsdlFile}");
                return 1;
            }

            try
            {
                GenerationParameters.PopulateModel(options.CsdlFile);

                // Process the more complicated options into actionable structures.
                GenerationParameters.PopulateChosenTypes(options);
                GenerationParameters.PopulateChosenEnums(options);
                GenerationParameters.PopulateChosenPrimitives(options);

                MemoryStream stream = new MemoryStream();
                ContainerBuilder cb = new ContainerBuilder();
                cb.AddDefaultODataServices();

                IServiceProvider sp = cb.BuildContainer();
                var message = new InMemoryMessage {Stream = stream, Container = sp};
                var settings = new ODataMessageWriterSettings
                {
                    Validations = ValidationKinds.All
                };

                var format = ODataFormat.Json;
                settings.SetContentType(format);

                var serviceRoot = new Uri(options.BaseUrl, UriKind.Absolute);
                var relativeUrlToParse = options.UriToPost;
                ODataUriParser parser =
                    new ODataUriParser(GenerationParameters.Model, serviceRoot, new Uri(relativeUrlToParse, UriKind.Relative));
                GenerationParameters.Path = parser.ParsePath();

                settings.ODataUri = new ODataUri
                {
                    ServiceRoot = serviceRoot,
                    Path = GenerationParameters.Path,
                };

                // Get to start point of writer, using path.
                if (!(GenerationParameters.Path.LastSegment is NavigationPropertySegment finalNavPropSegment))
                {
                    Console.WriteLine("Path must end in navigation property.");
                    return 1;
                }
                else
                {
                    var writer = new ODataMessageWriter((IODataRequestMessage) message, settings, GenerationParameters.Model);

                    IEdmProperty property = finalNavPropSegment.NavigationProperty;
                    IEdmStructuredType propertyType = property.Type.Definition.AsElementType() as IEdmStructuredType;
                    propertyType = ChooseDerivedStructuralTypeIfAny(propertyType, property.Name);
                    ODataWriter resWriter =
                        writer.CreateODataResourceWriter(finalNavPropSegment.NavigationSource, propertyType);
                    WriteResource(resWriter, propertyType);

                    var output = PrettyPrint(stream);
                    Console.WriteLine(output);
                    return 0;
                }
            }
            catch (Exception)
            {
                return 1;
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

        private static void WriteResource(ODataWriter resWriter, IEdmStructuredType structuredType)
        {
            var rootOdr = new ODataResource
            {
                TypeName = structuredType.FullTypeName(),
                TypeAnnotation = new ODataTypeAnnotation(structuredType.FullTypeName())
            };
            AddExamplePrimitiveStructuralProperties(rootOdr, structuredType.StructuralProperties());
            resWriter.WriteStart(rootOdr);
            WriteContainedResources(resWriter,
                structuredType.NavigationProperties().Where(p => p.ContainsTarget));
            WriteContainedResources(resWriter,
                structuredType.StructuralProperties().Where(p =>
                    p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Complex));
            WriteReferenceBindings(resWriter,
                structuredType.NavigationProperties().Where(p => !p.ContainsTarget));
            resWriter.WriteEnd(); // ODataResource
        }

        private static void WriteResourceSet(ODataWriter resWriter, IEdmStructuredType structuredType)
        {
            var set = new ODataResourceSet();
            var rootOdr = new ODataResource
            {
                TypeName = structuredType.FullTypeName()
            };
            AddExamplePrimitiveStructuralProperties(rootOdr, structuredType.StructuralProperties());
            resWriter.WriteStart(set);
            for (int i = 0; i < 2; i++)
            {
                resWriter.WriteStart(rootOdr);
                WriteContainedResources(resWriter,
                    structuredType.NavigationProperties().Where(p => p.ContainsTarget));
                WriteContainedResources(resWriter,
                    structuredType.StructuralProperties().Where(p =>
                        p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Complex));
                WriteReferenceBindings(resWriter,
                    structuredType.NavigationProperties().Where(p => !p.ContainsTarget));
                resWriter.WriteEnd(); // ODataResource
            }

            resWriter.WriteEnd(); // ODataResourceSet
        }

        private static void WriteReferenceBindings(
            ODataWriter resWriter,
            IEnumerable<IEdmNavigationProperty> properties)
        {
            properties = properties.FilterComputed<IEdmNavigationProperty>(GenerationParameters.Model);

            // For each property, build URL to the nav prop based on the nav prop binding in the entitySet.
            // to find the necessary nav prop bindings, we need to look under the root container (es or singleton) that the call is being made to.
            IEdmNavigationSource bindingsHost = GenerationParameters.Model.FindDeclaredNavigationSource(GenerationParameters.Path.FirstSegment.Identifier);

            foreach (IEdmNavigationProperty navProp in properties)
            {
                bool isCollection = navProp.Type.IsCollection();
                var binding = bindingsHost.FindNavigationPropertyBindings(navProp).FirstOrDefault();
                if (binding == null)
                {
                    Console.WriteLine($"Error: No bindingPath found for {navProp.Name}.");
                    throw new InvalidOperationException();
                }

                resWriter.WriteStart(new ODataNestedResourceInfo {Name = navProp.Name, IsCollection = isCollection});


                for (int i = 0; i < (navProp.Type.IsCollection() ? 2 : 1); i++)
                {
                    var link = ConstructEntityReferenceLink(binding);
                    resWriter.WriteEntityReferenceLink(link);
                }

                resWriter.WriteEnd(); // ODataNestedResourceInfo
            }
        }

        /// <summary>
        /// Create a reference link using the target of the binding to create the Url.
        /// </summary>
        private static ODataEntityReferenceLink ConstructEntityReferenceLink(
            IEdmNavigationPropertyBinding binding)
        {
            string[] segmentsList = binding.Target.Path.PathSegments.ToArray();

            // Walk along the path in the target of the binding.
            IEdmNavigationSource rootTargetElement = GenerationParameters.Model.FindDeclaredNavigationSource(binding.Target.Path.PathSegments.First());

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
                    Console.WriteLine($"Error: bindingTarget '{binding.Target.Path.Path}' for {binding.NavigationProperty.Name} is erroneous");
                    throw new InvalidOperationException();
                }

                return nextSegmentProp.Type.Definition;
            }


            var uriBuilder = new StringBuilder(Options.BaseUrl.TrimEnd('/'));

            // Cursor through the types that make up the binding's target path.
            IEdmType targetCursor = rootTargetElement.Type;
            for (int segment = 0; segment < segmentsList.Length; targetCursor = AdvanceCursor(targetCursor, segment++))
            {
                uriBuilder.Append($"/{segmentsList[segment]}");
                if (targetCursor is IEdmCollectionType)
                {
                    uriBuilder.Append($"/id{ValueGenerator.MonotonicId++}");
                }
            }

            var link = new ODataEntityReferenceLink
            {
                Url = new Uri(uriBuilder.ToString(), UriKind.Absolute)
            };
            return link;
        }

        private static void WriteContainedResources(
            ODataWriter resWriter,
            IEnumerable<IEdmProperty> properties)
        {
            properties = properties.FilterComputed<IEdmProperty>(GenerationParameters.Model);

            foreach (IEdmProperty navProp in properties)
            {
                bool isCollection = navProp.Type.IsCollection();
                resWriter.WriteStart(new ODataNestedResourceInfo { Name = navProp.Name, IsCollection = isCollection });
                IEdmStructuredType propertyType = navProp.Type.Definition.AsElementType() as IEdmStructuredType;
                propertyType = ChooseDerivedStructuralTypeIfAny(propertyType, navProp.Name);
                if (!isCollection)
                {
                    WriteResource(resWriter, propertyType);
                }
                else
                {
                    WriteResourceSet(resWriter, propertyType);
                }
                resWriter.WriteEnd(); // ODataNestedResourceInfo
            }
        }

        private static IEdmStructuredType ChooseDerivedStructuralTypeIfAny(IEdmStructuredType propertyType, string propertyName)
        {
            var potentialTypes = GenerationParameters.Model.FindAllDerivedTypes(propertyType).ToList();
            if (potentialTypes.Count > 0)
            {
                // Must pick a type.
                potentialTypes.Add(propertyType);

                if (!GenerationParameters.ChosenTypes.TryGetValue(propertyName, out propertyType))
                {
                    var concreteTypes = potentialTypes.Where(t => !t.IsAbstract).ToList();
                    propertyType = concreteTypes[ValueGenerator.Random.Next(concreteTypes.Count)];
                }
            }

            return propertyType;
        }

        private static void AddExamplePrimitiveStructuralProperties(
            ODataResource structuralResource,
            IEnumerable<IEdmStructuralProperty> properties)
        {
            properties = properties.FilterComputed<IEdmStructuralProperty>(GenerationParameters.Model);
            List<ODataProperty> odataProps = new List<ODataProperty>(
            properties.Where(p=> p.Type.Definition.AsElementType().TypeKind != EdmTypeKind.Complex).Select(p => ValueGenerator.GetExamplePrimitiveProperty(p)));

            structuralResource.Properties = odataProps;
        }
    }
}
