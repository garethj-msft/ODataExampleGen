﻿// <copyright file="GenerationParameters.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System.Net.Http;

namespace ODataExampleGenerator
{
    using System;
    using System.Collections.Generic;
    using Microsoft.OData.Edm;

    public class GenerationParameters
    {
        /// <summary>
        /// The method to generate an example for.
        /// </summary>
        public HttpMethod HttpMethod { get; set; }

        /// <summary>
        /// The EDM model to work against.
        /// </summary>
        public IEdmModel Model { get; set; }

        /// <summary>
        /// The URI of the root of the service.
        /// </summary>
        public Uri ServiceRoot { get; set; }

        internal GenerationStyle GenerationStyle { get; set; }

        public IDictionary<string, IEdmStructuredType> ChosenTypes { get; } =
            new Dictionary<string, IEdmStructuredType>();

        public IDictionary<string, string> ChosenPrimitives { get; } =
            new Dictionary<string, string>();

        public IDictionary<string, string> ChosenIdProviders { get; } =
            new Dictionary<string, string>();

        /// <summary>
        /// Navigation properties that should have a deep insert gemerated for them for POST/PUT/PATCH requests.
        /// </summary>
        public IList<string> DeepInserts { get; set; }

        /// <summary>
        /// Properties that should be skipped in the generated requests.
        /// </summary>
        public IList<string> SkippedProperties { get; } = new List<string>();
    }

    public enum GenerationStyle
    {
        Request,
        Response,
    }
}