// <copyright file="ProgramOptionsExtractor.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Linq;
using Microsoft.OData.Edm;
using ODataExampleGenerator;

namespace ODataExampleGen
{
    /// <summary>
    /// Utility class to pull values from the command-line options and move them into the GenerationParameters.
    /// </summary>
    public static class ProgramOptionsExtractor
    {
        public static void PopulateChosenTypes(ProgramOptions options, GenerationParameters generationParameters)
        {
            foreach (string optionPair in options.PropertyTypePairs)
            {
                var pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    throw new InvalidOperationException($"Option '{optionPair}' is malformed, must be 'propertyName:typeName'.");
                }

                var declared = FindQualifiedTypeByName(pairTerms[1], generationParameters);

                if (declared == null)
                {
                    throw new InvalidOperationException($"Option '{optionPair}' is malformed, typename '{pairTerms[1]}' not found in model.");
                }

                generationParameters.ChosenTypes[pairTerms[0]] = (IEdmStructuredType) declared;
            }
        }

        public static void PopulateChosenEnums(ProgramOptions options, GenerationParameters generationParameters)
        {
            foreach (string optionPair in options.EnumValuePairs)
            {
                var pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    throw new InvalidOperationException($"Option '{optionPair}' is malformed, must be 'propertyName:enumValue'.");
                }

                IEdmEnumMember enumMember = FindEnumValueByName(pairTerms[0], pairTerms[1], generationParameters);

                if (enumMember == null)
                {
                    throw new InvalidOperationException($"Option '{optionPair}' is malformed, enum value '{pairTerms[1]}' not found in model.");
                }

                generationParameters.ChosenPrimitives[pairTerms[0]] = enumMember.Name;
            }
        }

        public static void PopulateChosenPrimitives(ProgramOptions options, GenerationParameters generationParameters)
        {
            foreach (string optionPair in options.PrimitiveValuePairs)
            {
                var pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    throw new InvalidOperationException($"Option '{optionPair}' is malformed, must be 'propertyName:primitiveValue'.");
                }

                generationParameters.ChosenPrimitives[pairTerms[0]] = pairTerms[1];
            }
        }

        private static IEdmEnumMember FindEnumValueByName(string propertyName, string enumValueString, GenerationParameters generationParameters)
        {
            var members = from s in generationParameters.Model.SchemaElements
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

            if (!members.Any())
            {
                throw new InvalidOperationException($"Unable to find an enum value '{enumValueString}' matching a property '{propertyName}'.");
            }
            return Enumerable.First(members);
        }

        private static IEdmType FindQualifiedTypeByName(string typeName, GenerationParameters generationParameters)
        {
            IEdmType declared = null;
            foreach (var aNamespace in generationParameters.Model.DeclaredNamespaces)
            {
                declared = generationParameters.Model.FindDeclaredType($"{aNamespace}.{typeName}");
                if (declared != null)
                {
                    break;
                }
            }

            return declared;
        }
    }
}