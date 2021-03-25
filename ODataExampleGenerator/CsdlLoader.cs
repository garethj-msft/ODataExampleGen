// <copyright file="CsdlLoader.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Edm.Validation;
using Microsoft.OData.Edm;
using System.Xml;

namespace ODataExampleGenerator
{
    /// <summary>
    /// Helper class to load models.
    /// </summary>
    public static class CsdlLoader
    {
        /// <summary>
        /// Load a model.
        /// </summary>
        public static IEdmModel LoadModel(string csdlFileFullPath)
        {
            if (!File.Exists(csdlFileFullPath))
            {
                throw new InvalidOperationException($"Unable to locate csdl file: {csdlFileFullPath}");
            }

            var reader = XmlReader.Create(new StringReader(File.ReadAllText(csdlFileFullPath)));

            if (CsdlReader.TryParse(reader, false, out IEdmModel model, out IEnumerable<EdmError> errors))
            {
                return model;
            }
            else
            {
                var errorMessages = new StringBuilder();
                foreach (EdmError error in errors)
                {
                    errorMessages.AppendLine(error.ErrorMessage);
                }

                throw new InvalidOperationException($@"Failed to read model {csdlFileFullPath}.\r\nErrors:\r\n{errorMessages}");
            }
        }
    }
}
