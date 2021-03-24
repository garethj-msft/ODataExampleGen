// <copyright file="GuidIdProvider.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using ODataExampleGenerator.ComponentInterfaces;

namespace ODataExampleGenerator.ComponentImplementations
{
    class GuidIdProvider : IIdProvider
    {
        public string Name => nameof(GuidIdProvider);

        public string GetNewId()
        {
            return Guid.NewGuid().ToString("D");
        }
    }
}
