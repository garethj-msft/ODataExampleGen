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
        public GenerationParameters()
        {
        }

        public IEdmModel Model { get; private set; }

        public IDictionary<string, IEdmStructuredType> ChosenTypes { get; } =
            new Dictionary<string, IEdmStructuredType>();

        public IDictionary<string, IEdmEnumMember> ChosenEnums { get; } =
            new Dictionary<string, IEdmEnumMember>();

        public IDictionary<string, string> ChosenPrimitives { get; } =
            new Dictionary<string, string>();

        public ODataPath Path { get; set; }

        public void PopulateModel(string csdlFileFullPath)
        {
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
}