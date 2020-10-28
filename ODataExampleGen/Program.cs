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

        public class Options
        {
            [Option('c', "csdl", Required = true, HelpText = "CSDL file to use as the model from within which to generate examples.")]
            public string CsdlFile{ get; set; }

            [Option('u', "uri", Required = true, HelpText = "URI to generate an example to POST to.")]
            public string UriToPost{ get; set; }

            [Option('b', "baseUri", Required = false, HelpText = "Base URI for the API.", Default = "https://graph.microsoft.com/beta/")]
            public string BaseUrl { get; set; }

        }

        static int Main(string[] args)
        {
            int result = Parser.Default.ParseArguments<Options>(args).MapResult(RunCommand, _ => 1);
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }

            return result;
        }

        private static int RunCommand(Options options)
        {
            string csdlFileFullPath = options.CsdlFile;
            string baseUrl = options.BaseUrl;
            string uriToPost = options.UriToPost;
            IEnumerable<EdmError> errors;
            IEdmModel model;
            XmlReader reader = XmlReader.Create(new StringReader(File.ReadAllText(csdlFileFullPath)));

            if (!CsdlReader.TryParse(reader, false, out model, out errors))
            {
                StringBuilder errorMessages = new StringBuilder();
                foreach (var error in errors)
                {
                    Console.WriteLine(error.ErrorMessage);
                }
                return 1;
            }

            MemoryStream stream = new MemoryStream();
            ContainerBuilder cb = new ContainerBuilder();
            cb.AddDefaultODataServices();
            ODataSimplifiedOptions option = new ODataSimplifiedOptions
            {
                EnableWritingKeyAsSegment    = true,
            };

            IServiceProvider sp = cb.BuildContainer();
            InMemoryMessage message = new InMemoryMessage { Stream = stream, Container = sp };
            var settings = new ODataMessageWriterSettings
            {
                Validations = ValidationKinds.All
            };

            ODataFormat format = ODataFormat.Json;
            settings.SetContentType(format);

            var serviceRoot = new Uri(baseUrl, UriKind.Absolute);
            var relativeUrlToParse = uriToPost;
            ODataUriParser parser = new ODataUriParser(model, serviceRoot, new Uri(relativeUrlToParse, UriKind.Relative));
            ODataPath path = parser.ParsePath();
            settings.ODataUri = new ODataUri
            {
                ServiceRoot = serviceRoot,
                Path = path,
            };

            // Get to start point of writer, using path.
            if (!(path.LastSegment is NavigationPropertySegment finalNavPropSegment))
            {
                Console.WriteLine("Path must end in navigation property.");
                return 1;
            }
            else
            {
                ODataMessageWriter writer = new ODataMessageWriter((IODataRequestMessage)message, settings, model);
                ODataWriter resWriter = writer.CreateODataResourceWriter(finalNavPropSegment.NavigationSource);

                WriteResource(resWriter, finalNavPropSegment.NavigationProperty);

                var output = PrettyPrint(stream);
                Console.WriteLine(output);
                return 0;
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

        private static void WriteResource(ODataWriter resWriter, IEdmProperty property)
        {
            var rootODR = new ODataResource();
            IEdmStructuredType finalEntityType =
                property.Type.Definition.AsElementType() as IEdmStructuredType;
            AddExamplePrimitiveStructuralProperties(rootODR, finalEntityType.StructuralProperties());
            resWriter.WriteStart(rootODR);
            WriteContainedEntities(resWriter,
                finalEntityType.DeclaredNavigationProperties().Where(p => p.ContainsTarget));
            WriteContainedEntities(resWriter,
                finalEntityType.DeclaredStructuralProperties().Where(p =>
                    p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Complex));
            WriteReferenceBindings(resWriter,
                finalEntityType.DeclaredNavigationProperties().Where(p => !p.ContainsTarget));
            resWriter.WriteEnd();
        }

        private static void WriteResourceSet(ODataWriter resWriter, IEdmProperty property)
        {
            var set = new ODataResourceSet();
            var rootODR = new ODataResource();
            IEdmStructuredType finalEntityType =
                property.Type.Definition.AsElementType() as IEdmStructuredType;
            AddExamplePrimitiveStructuralProperties(rootODR, finalEntityType.StructuralProperties());
            resWriter.WriteStart(set);
            for (int i = 0; i < 2; i++)
            {
                resWriter.WriteStart(rootODR);
                WriteContainedEntities(resWriter,
                    finalEntityType.DeclaredNavigationProperties().Where(p => p.ContainsTarget));
                WriteContainedEntities(resWriter,
                    finalEntityType.DeclaredStructuralProperties().Where(p => p.Type.Definition.AsElementType().TypeKind == EdmTypeKind.Complex));
                WriteReferenceBindings(resWriter,
                    finalEntityType.DeclaredNavigationProperties().Where(p => !p.ContainsTarget));
                resWriter.WriteEnd();
            }

            resWriter.WriteEnd();
        }

        private static void WriteReferenceBindings(
            ODataWriter resWriter,
            IEnumerable<IEdmNavigationProperty> properties)
        {
            // TODO
        }

        private static void WriteContainedEntities(
            ODataWriter resWriter,
            IEnumerable<IEdmProperty> properties)
        {
            foreach (IEdmProperty navProp in properties)
            {
                bool isCollection = navProp.Type.IsCollection();
                resWriter.WriteStart(new ODataNestedResourceInfo { Name = navProp.Name, IsCollection = isCollection });
                if (!isCollection)
                {
                    WriteResource(resWriter, navProp);
                }
                else
                {
                    WriteResourceSet(resWriter, navProp);
                }
                resWriter.WriteEnd();
            }
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
                EdmPrimitiveTypeKind.String => $"A {p.Name}",
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
                EdmPrimitiveTypeKind.String => new object[]{$"A {p.Name}", $"Another {p.Name}"},
                _ => throw new InvalidOperationException("Unknown primitive type."),
            }};
        }
    }
}
