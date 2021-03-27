// <copyright file="GuidIdProvider.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.OData.Edm;
using ODataExampleGenerator.ComponentInterfaces;

namespace ODataExampleGenerator.ComponentImplementations
{
    internal class GuidIdProvider : IIdProvider
    {
        public string Name => "Guid";

        public string GetNewId(IEdmStructuredType _)
        {
            return Guid.NewGuid().ToString("D");
        }
    }
}
