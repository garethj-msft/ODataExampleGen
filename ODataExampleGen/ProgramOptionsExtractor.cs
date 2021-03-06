﻿// <copyright file="ProgramOptionsExtractor.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Process the more complicated options into actionable structures.
        /// </summary>
        public static void PopulateComplexOptions(ProgramOptions options, GenerationParameters generationParameters)
        {
            PopulateChosenTypes(options, generationParameters);
            PopulateChosenEnums(options, generationParameters);
            PopulateChosenPrimitives(options, generationParameters);
            PopulateChosenIdProviders(options, generationParameters);
        }

        private static void PopulateChosenTypes(ProgramOptions options, GenerationParameters generationParameters)
        {
            foreach (string optionPair in options.PropertyTypePairs)
            {
                string[] pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    throw new InvalidOperationException($"Option '{optionPair}' is malformed, must be 'propertyName:typeName'.");
                }

                IEdmType declared = FindQualifiedTypeByName(pairTerms[1], generationParameters);

                if (declared == null)
                {
                    throw new InvalidOperationException($"Option '{optionPair}' is malformed, typename '{pairTerms[1]}' not found in model.");
                }

                generationParameters.ChosenTypes[pairTerms[0]] = (IEdmStructuredType) declared;
            }
        }

        private static void PopulateChosenEnums(ProgramOptions options, GenerationParameters generationParameters)
        {
            foreach (string optionPair in options.EnumValuePairs)
            {
                string[] pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
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

        private static void PopulateChosenPrimitives(ProgramOptions options, GenerationParameters generationParameters)
        {
            foreach (string optionPair in options.PrimitiveValuePairs)
            {
                string[] pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    throw new InvalidOperationException($"Option '{optionPair}' is malformed, must be 'propertyName:primitiveValue'.");
                }

                if (string.Equals(pairTerms[1], "@skip", StringComparison.OrdinalIgnoreCase))
                {
                    generationParameters.SkippedProperties.Add(pairTerms[0]);
                }
                else
                {
                    generationParameters.ChosenPrimitives[pairTerms[0]] = pairTerms[1];
                }
            }
        }

        private static void PopulateChosenIdProviders(ProgramOptions options, GenerationParameters generationParameters)
        {
            foreach (string optionPair in options.IdProviderPairs)
            {
                string[] pairTerms = optionPair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (pairTerms.Length != 2)
                {
                    throw new InvalidOperationException($"Option '{optionPair}' is malformed, must be 'idPropertyName:idProviderName'.");
                }

                generationParameters.ChosenIdProviders[pairTerms[0]] = pairTerms[1];
            }
        }

        private static IEdmEnumMember FindEnumValueByName(string propertyName, string enumValueString, GenerationParameters generationParameters)
        {
           IEnumerable<IEdmEnumMember> members = from s in generationParameters.Model.SchemaElements
                where s.SchemaElementKind == EdmSchemaElementKind.TypeDefinition && s is IEdmStructuredType
                let t = (IEdmStructuredType)s
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
            foreach (string aNamespace in generationParameters.Model.DeclaredNamespaces)
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