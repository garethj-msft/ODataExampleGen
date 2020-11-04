using System.Runtime.InteropServices.ComTypes;

namespace ODataExampleGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Xml;
    using Microsoft.OData.Edm;
    using Microsoft.OData.Edm.Csdl;
    using Microsoft.OData.Edm.Validation;
    using Microsoft.OData.UriParser;

    public class GenerationParameters
    {
        public IEdmModel Model { get; private set; }

        public ODataPath Path { get; private set; }

        public Uri ServiceRoot { get; set; }

        public string UriToPost { get; set; }
        
        public IDictionary<string, IEdmStructuredType> ChosenTypes { get; } =
            new Dictionary<string, IEdmStructuredType>();

        public IDictionary<string, IEdmEnumMember> ChosenEnums { get; } =
            new Dictionary<string, IEdmEnumMember>();

        public IDictionary<string, string> ChosenPrimitives { get; } =
            new Dictionary<string, string>();

        public void PopulateModel(string csdlFileFullPath)
        {
            if (!File.Exists(csdlFileFullPath))
            {
                throw new InvalidOperationException($"Unable to locate csdl file: {csdlFileFullPath}");
            }

            var reader = XmlReader.Create(new StringReader(File.ReadAllText(csdlFileFullPath)));

            if (CsdlReader.TryParse(reader, false, out IEdmModel model, out IEnumerable<EdmError> errors))
            {
                this.Model = model;
            }
            else
            {
                var errorMessages = new StringBuilder();
                foreach (var error in errors)
                {
                    errorMessages.AppendLine(error.ErrorMessage);
                }

                throw new InvalidOperationException(
                    $@"Failed to read model {csdlFileFullPath}.\r\nErrors:\r\n{errorMessages}");
            }
        }

        public void PopulatePath()
        {
            _ = this.Model ?? throw new InvalidOperationException($"Model must be populated before calling {nameof(PopulatePath)}.");
            _ = this.ServiceRoot ?? throw new InvalidOperationException($"ServiceRoot must be populated before calling {nameof(PopulatePath)}.");
            _ = this.UriToPost ?? throw new InvalidOperationException($"UriToPost must be populated before calling {nameof(PopulatePath)}.");

            var parser = new ODataUriParser(
                this.Model,
                this.ServiceRoot,
                new Uri(this.UriToPost, UriKind.Relative));
            this.Path = parser.ParsePath();
        }
    }
}