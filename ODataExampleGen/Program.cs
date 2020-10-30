using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.UriParser;
using CommandLine;

namespace ODataExampleGen
{
    class Program
    {
        private static readonly Random Random = new Random();

        private static IEdmModel Model;

        private static ProgramOptions Options;

        private static Dictionary<string, IEdmStructuredType> ChosenTypes = new Dictionary<string, IEdmStructuredType>();

        private static ODataPath Path;

        private static int MonotonicId = 1;

        static int Main(string[] args)
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
            string csdlFileFullPath = options.CsdlFile;
            string baseUrl = options.BaseUrl;
            string uriToPost = options.UriToPost;
            if (!File.Exists(csdlFileFullPath))
            {
                Console.WriteLine($"Unable to locate csdl file: {csdlFileFullPath}");
                return 1;
            }

            var reader = XmlReader.Create(new StringReader(File.ReadAllText(csdlFileFullPath)));

            if (!CsdlReader.TryParse(reader, false, out Model, out var errors))
            {
                StringBuilder errorMessages = new StringBuilder();
                foreach (var error in errors)
                {
                    Console.WriteLine(error.ErrorMessage);
                }
                return 1;
            }

            foreach (string optionPair in options.PropertyTypePairs)
            {
                var pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    Console.WriteLine($"Option '{optionPair}' is malformed, must be 'propertyName:typeName'.");
                    return 1;
                }

                IEdmType declared = null;
                foreach (var aNamespace in Model.DeclaredNamespaces)
                {
                    string fqtn = $"{aNamespace}.{pairTerms[1]}";
                    declared = Model.FindDeclaredType(fqtn);
                    if (declared != null)
                    {
                        break;
                    }
                }

                if (declared == null)
                {
                    Console.WriteLine($"Option '{optionPair}' is malformed, typename '{pairTerms[1]}' not found in model.");
                    return 1;
                }

                ChosenTypes[pairTerms[0]] = (IEdmStructuredType)declared;
            }

            try
            {
                MemoryStream stream = new MemoryStream();
                ContainerBuilder cb = new ContainerBuilder();
                cb.AddDefaultODataServices();
                ODataSimplifiedOptions option = new ODataSimplifiedOptions
                {
                    EnableWritingKeyAsSegment = true,
                };

                IServiceProvider sp = cb.BuildContainer();
                InMemoryMessage message = new InMemoryMessage {Stream = stream, Container = sp};
                var settings = new ODataMessageWriterSettings
                {
                    Validations = ValidationKinds.All
                };

                ODataFormat format = ODataFormat.Json;
                settings.SetContentType(format);

                var serviceRoot = new Uri(baseUrl, UriKind.Absolute);
                var relativeUrlToParse = uriToPost;
                ODataUriParser parser =
                    new ODataUriParser(Model, serviceRoot, new Uri(relativeUrlToParse, UriKind.Relative));
                Path = parser.ParsePath();

                settings.ODataUri = new ODataUri
                {
                    ServiceRoot = serviceRoot,
                    Path = Path,
                };

                // Get to start point of writer, using path.
                if (!(Path.LastSegment is NavigationPropertySegment finalNavPropSegment))
                {
                    Console.WriteLine("Path must end in navigation property.");
                    return 1;
                }
                else
                {
                    ODataMessageWriter writer = new ODataMessageWriter((IODataRequestMessage) message, settings, Model);

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

        private static string PrettyPrint(MemoryStream stream)
        {
            JsonDocument doc = JsonDocument.Parse(stream.ToArray(),
                new JsonDocumentOptions
                    {AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 2000});
            using var outStream = new MemoryStream();
            using (var outWriter = new Utf8JsonWriter(outStream, new JsonWriterOptions {Indented = true}))
            {
                doc.WriteTo(outWriter);
            }

            outStream.Flush();
            string output = Encoding.UTF8.GetString(outStream.ToArray());
            return output;
        }

        private static void WriteResource(ODataWriter resWriter, IEdmStructuredType structuredType)
        {
            var rootODR = new ODataResource
            {
                TypeName = structuredType.FullTypeName()
            };
            AddExamplePrimitiveStructuralProperties(rootODR, structuredType.StructuralProperties());
            resWriter.WriteStart(rootODR);
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
            var rootODR = new ODataResource
            {
                TypeName = structuredType.FullTypeName()
            };
            AddExamplePrimitiveStructuralProperties(rootODR, structuredType.StructuralProperties());
            resWriter.WriteStart(set);
            for (int i = 0; i < 2; i++)
            {
                resWriter.WriteStart(rootODR);
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
            // For each property, build URL to the nav prop based on the nav prop binding in the entitySet.
            // to find the necessary nav prop bindings, we need to look under the root container (es or singleton) that the call is being made to.
            IEdmNavigationSource bindingsHost = Model.FindDeclaredNavigationSource(Path.FirstSegment.Identifier);

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
                    var link = ConstructEntityReferenceLink(binding, ref MonotonicId);
                    resWriter.WriteEntityReferenceLink(link);
                }

                resWriter.WriteEnd(); // ODataNestedResourceInfo
            }
        }

        /// <summary>
        /// Create a reference link using the target of the binding to create the Url.
        /// </summary>
        private static ODataEntityReferenceLink ConstructEntityReferenceLink(
            IEdmNavigationPropertyBinding binding,
            ref int idCount)
        {
            string[] segmentsList = binding.Target.Path.PathSegments.ToArray();

            // Walk along the path in the target of the binding.
            IEdmNavigationSource rootTargetElement = Model.FindDeclaredNavigationSource(binding.Target.Path.PathSegments.First());

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
                    uriBuilder.Append($"/id{idCount++}");
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
            var potentialTypes = Model.FindAllDerivedTypes(propertyType).ToList();
            if (potentialTypes.Count > 0)
            {
                // Must pick a type.
                potentialTypes.Add(propertyType);

                if (!ChosenTypes.TryGetValue(propertyName, out propertyType))
                {
                    var concreteTypes = potentialTypes.Where(t => !t.IsAbstract).ToList();
                    propertyType = concreteTypes[Random.Next(concreteTypes.Count)];
                }
            }

            return propertyType;
        }

        private static void AddExamplePrimitiveStructuralProperties(
            ODataResource structuralResource,
            IEnumerable<IEdmStructuralProperty> properties)
        {
            List<ODataProperty> odataProps = new List<ODataProperty>(
            properties.Where(p=> p.Type.Definition.AsElementType().TypeKind != EdmTypeKind.Complex).Select(p => GetExamplePrimitiveProperty(p)));

            structuralResource.Properties = odataProps;
        }

        private static ODataProperty GetExamplePrimitiveProperty(IEdmStructuralProperty p)
        {
            if (p.Type.IsEnum())
            {
                var enumType = (IEdmEnumType) p.Type.Definition;
                var usefulMembers = enumType.Members
                    .Where(m => !m.Name.Equals("unknownFutureValue", StringComparison.OrdinalIgnoreCase))
                    .Select(m => m.Name).ToList();
                var member = usefulMembers[Random.Next(usefulMembers.Count)];

                return new ODataProperty
                    {Name = p.Name, Value = new ODataEnumValue(member)};
            }
            else
            {
                return new ODataProperty
                    {Name = p.Name, PrimitiveTypeKind = p.Type.PrimitiveKind(), Value = GetExampleStructuralValue(p)};
            }
        }

        private static object GetExampleStructuralValue(IEdmStructuralProperty p)
        {
            if (p.Type.IsCollection())
            {
                return GetExamplePrimitiveValueArray(p);
            }
            else
            {
                return GetExampleScalarPrimitiveValue(p);
            }
        }

        private static object GetExampleScalarPrimitiveValue(IEdmStructuralProperty p)
        {
            return ((IEdmPrimitiveType)p.Type.Definition.AsElementType()).PrimitiveKind switch
            {
                EdmPrimitiveTypeKind.Boolean => true,
                EdmPrimitiveTypeKind.Byte =>  2,
                EdmPrimitiveTypeKind.Date => new Date(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, DateTimeOffset.UtcNow.Day),
                EdmPrimitiveTypeKind.DateTimeOffset =>DateTimeOffset.UtcNow,
                EdmPrimitiveTypeKind.Decimal => 5.0,
                EdmPrimitiveTypeKind.Single => 5.0,
                EdmPrimitiveTypeKind.Double => 5.0,
                EdmPrimitiveTypeKind.Int16 => 3,
                EdmPrimitiveTypeKind.Int32 => 3,
                EdmPrimitiveTypeKind.Int64 => 3,
                EdmPrimitiveTypeKind.Duration => TimeSpan.FromHours(6.0),
                EdmPrimitiveTypeKind.String => $"A sample {p.Name}",
                _ => throw new InvalidOperationException("Unknown primitive type."),

            };
        }
        private static object GetExamplePrimitiveValueArray(IEdmStructuralProperty p)
        {
            var now = DateTimeOffset.UtcNow;
            return new ODataCollectionValue {Items = ((IEdmPrimitiveType)p.Type.Definition.AsElementType()).PrimitiveKind switch
            {
                EdmPrimitiveTypeKind.Boolean => new object[]{ true, false}.AsEnumerable(),
                EdmPrimitiveTypeKind.Byte =>  new object[]{2, 3},
                EdmPrimitiveTypeKind.Date => new object[]{ new Date(now.Year, now.Month, now.Day), new Date(now.Year, now.Month, now.Day)},
                EdmPrimitiveTypeKind.DateTimeOffset =>new object[]{now, now},
                EdmPrimitiveTypeKind.Decimal => new object[]{5.0, 6.0},
                EdmPrimitiveTypeKind.Single => new object[]{5.0, 6.0},
                EdmPrimitiveTypeKind.Double => new object[]{5.0, 6.0},
                EdmPrimitiveTypeKind.Int16 => new object[]{3, 4},
                EdmPrimitiveTypeKind.Int32 => new object[]{3, 4},
                EdmPrimitiveTypeKind.Int64 => new object[]{3, 4},
                EdmPrimitiveTypeKind.Duration => new object[]{TimeSpan.FromHours(6.0), TimeSpan.FromHours(4.0)},
                EdmPrimitiveTypeKind.String => new object[]{$"A sample of {p.Name}", $"Another sample of {p.Name}"},
                _ => throw new InvalidOperationException("Unknown primitive type."),
            }};
        }
    }
}
