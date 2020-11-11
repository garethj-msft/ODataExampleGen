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
        public IEdmModel Model { get; set; }

        public Uri ServiceRoot { get; set; }

        public GenerationStyle GenerationStyle { get; set; }

        public IDictionary<string, IEdmStructuredType> ChosenTypes { get; } =
            new Dictionary<string, IEdmStructuredType>();

        public IDictionary<string, string> ChosenPrimitives { get; } =
            new Dictionary<string, string>();

        public void LoadModel(string csdlFileFullPath)
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
    }

    public enum GenerationStyle
    {
        Request,
        Response,
    }
}