using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.UriParser;

namespace ODataExampleGen
{
    public class GenerationParameters
    {
        public IEdmModel Model { get; private set; }

        public IDictionary<string, IEdmStructuredType> ChosenTypes { get; } =
            new Dictionary<string, IEdmStructuredType>();

        public IDictionary<string, IEdmEnumMember> ChosenEnums { get; } =
            new Dictionary<string, IEdmEnumMember>();

        public IDictionary<string, string> ChosenPrimitives { get; } =
            new Dictionary<string, string>();

        public ODataPath Path { get; set; }

        public void PopulateChosenTypes(ProgramOptions options)
        {
            foreach (string optionPair in options.PropertyTypePairs)
            {
                var pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    Console.WriteLine($"Option '{optionPair}' is malformed, must be 'propertyName:typeName'.");
                    throw new InvalidOperationException();
                }

                var declared = FindQualifiedTypeByName(pairTerms[1]);

                if (declared == null)
                {
                    Console.WriteLine($"Option '{optionPair}' is malformed, typename '{pairTerms[1]}' not found in model.");
                    throw new InvalidOperationException();
                }

                ChosenTypes[pairTerms[0]] = (IEdmStructuredType) declared;
            }
        }

        public void PopulateChosenEnums(ProgramOptions options)
        {
            foreach (string optionPair in options.EnumValuePairs)
            {
                var pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    Console.WriteLine($"Option '{optionPair}' is malformed, must be 'propertyName:enumValue'.");
                    throw new InvalidOperationException();
                }

                IEdmEnumMember enumMember = FindEnumValueByName(pairTerms[0], pairTerms[1]);

                if (enumMember == null)
                {
                    Console.WriteLine($"Option '{optionPair}' is malformed, enum value '{pairTerms[1]}' not found in model.");
                    throw new InvalidOperationException();
                }

                ChosenEnums[pairTerms[0]] = enumMember;
            }
        }

        public void PopulateChosenPrimitives(ProgramOptions options)
        {
            foreach (string optionPair in options.PrimitiveValuePairs)
            {
                var pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    Console.WriteLine($"Option '{optionPair}' is malformed, must be 'propertyName:primitiveValue'.");
                    throw new InvalidOperationException();
                }

                ChosenPrimitives[pairTerms[0]] = pairTerms[1];
            }
        }

        private IEdmEnumMember FindEnumValueByName(string propertyName, string enumValueString)
        {
            var members = from s in this.Model.SchemaElements
                where s.SchemaElementKind == EdmSchemaElementKind.TypeDefinition && s is IEdmStructuredType
                let t = (IEdmStructuredType) s
                let prop1 = t.DeclaredProperties.FirstOrDefault(p =>
                    p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                    p.Type.IsEnum() &&
                    p.Type.Definition is IEdmEnumType)
                where prop1 != null
                let mem1 = ((IEdmEnumType) prop1.Type.Definition).Members.FirstOrDefault(m =>
                    m.Name.Equals(enumValueString, StringComparison.OrdinalIgnoreCase))
                where mem1 != null
                select mem1;

            return members.First();
        }

        private IEdmType FindQualifiedTypeByName(string typeName)
        {
            IEdmType declared = null;
            foreach (var aNamespace in this.Model.DeclaredNamespaces)
            {
                declared = this.Model.FindDeclaredType($"{aNamespace}.{typeName}");
                if (declared != null)
                {
                    break;
                }
            }

            return declared;
        }

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