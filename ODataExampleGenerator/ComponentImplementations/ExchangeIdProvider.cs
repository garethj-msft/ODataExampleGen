// <copyright file="ExchangeIdProvider.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.OData.Edm;
using ODataExampleGenerator.ComponentInterfaces;

namespace ODataExampleGenerator.ComponentImplementations
{
    internal class ExchangeIdProvider : IIdProvider
    {
        public string Name => "Exchange";

        public string GetNewId(IEdmStructuredType _)
        {
            // Use 256 bits of entropy to get a B64 encoding that's about the right length
            // It should really be the Base 64W encoding but there isn't a handy nuget implementation of that so regular base64 is close enough.
            string source = Guid.NewGuid().ToString("D") + Guid.NewGuid().ToString("D");
            byte[] sourceBytes = System.Text.Encoding.UTF8.GetBytes(source);
            return System.Convert.ToBase64String(sourceBytes);
        }
    }
}
