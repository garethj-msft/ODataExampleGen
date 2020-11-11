namespace ODataExampleGen
{
    using System.Collections.Generic;
    using CommandLine;

    public class ProgramOptions
    {
        [Option('c', "csdl", Required = true,
            HelpText = "CSDL file to use as the model from within which to generate examples.")]
        public string CsdlFile { get; set; }

        [Option('q', "request", Required = true, HelpText = "Generate a request body. Mutually exclusive with --response.", SetName = "request")]
        public bool GenerateRequest { get; set; }

        [Option('s', "response", Required = true, HelpText = "Generate a response body. Mutually exclusive with --request.", SetName = "response")]
        public bool GenerateResponse{ get; set; }

        [Option('u', "uri", Required = true, HelpText = "URI to generate an example to POST to.")]
        public string UriToPost { get; set; }

        [Option('b', "baseUri", Required = false, HelpText = "Base URI for the API.",
            Default = "https://graph.microsoft.com/beta/")]
        public string BaseUrl { get; set; }

        [Option('p', "propertyType", Required = false,
            HelpText = "Property:Type pairs for resolving choices in the inheritance hierarchy.")]
        public IEnumerable<string> PropertyTypePairs { get; set; }

        [Option('e', "enumValue", Required = false,
            HelpText = "Property:EnumValue pairs for resolving choices in enum property values.")]
        public IEnumerable<string> EnumValuePairs { get; set; }

        [Option('r', "primitiveValue", Required = false,
            HelpText = "Property:PrimitiveValue pairs for resolving choices in primitive property values.")]
        public IEnumerable<string> PrimitiveValuePairs { get; set; }
    }
}