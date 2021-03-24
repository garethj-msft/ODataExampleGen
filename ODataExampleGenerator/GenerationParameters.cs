// <copyright file="GenerationParameters.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System.Net.Http;
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
        public HttpMethod HttpMethod { get; set; }

        public IEdmModel Model { get; set; }

        public Uri ServiceRoot { get; set; }

        internal GenerationStyle GenerationStyle { get; set; }

        public IDictionary<string, IEdmStructuredType> ChosenTypes { get; } =
            new Dictionary<string, IEdmStructuredType>();

        public IDictionary<string, string> ChosenPrimitives { get; } =
            new Dictionary<string, string>();

        
    }

    public enum GenerationStyle
    {
        Request,
        Response,
    }
}