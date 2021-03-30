// <copyright file="ProgramOptions.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

namespace ODataExampleGen
{
    using System.Collections.Generic;
    using CommandLine;

    public class ProgramOptions
    {
        [Option('c', "csdl", Required = true,
            HelpText = "CSDL file to use as the model from within which to generate examples.")]
        public string CsdlFile { get; set; }

        [Option('m', "method", Required = true, HelpText = "Generate output for the given HTTP method.")]
        public string Method { get; set; }

        [Option('u', "uri", Required = true, HelpText = "URI to generate an example to POST to/GET from etc.")]
        public string UriForMethod { get; set; }

        [Option('b', "baseUri", Required = false, HelpText = "Base URI for the API.",
            Default = "https://graph.microsoft.com/beta/")]
        public string BaseUrl { get; set; }

        [Option('p', "propertyTypes", Required = false,
            HelpText = "Property:Type pairs for resolving choices in the inheritance hierarchy. Use propertyName:@skip to remove a property.")]
        public IEnumerable<string> PropertyTypePairs { get; set; }

        [Option('e', "enumValues", Required = false,
            HelpText = "Property:EnumValue pairs for resolving choices in enum property values.")]
        public IEnumerable<string> EnumValuePairs { get; set; }

        [Option('r', "primitiveValues", Required = false,
            HelpText = "Property:PrimitiveValue pairs for resolving choices in primitive property values.")]
        public IEnumerable<string> PrimitiveValuePairs { get; set; }

        [Option('i', "idProviders", Required = false,
            HelpText = "Property:IdProvider pairs for resolving which provider used to generate ids in string property values ending with id/Id.  Use @default as the property name for a fallback choice.")]
        public IEnumerable<string> IdProviderPairs { get; set; }

        [Option('d', "deepInserts", Required = false,
            HelpText = "Navigation property names that should be deep inserted on a PUT/POST/PATCH. Expand on GET should be specified in the uri parameter.")]
        public IEnumerable<string> DeepInserts { get; set; }
    }
}