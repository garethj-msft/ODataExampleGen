// <copyright file="ExchangeIdProvider.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ODataExampleGenerator.ComponentInterfaces;

namespace ODataExampleGenerator.ComponentImplementations
{
    internal class ExchangeIdProvider : IIdProvider
    {
        public string Name => "Exchange";

        public string GetNewId()
        {
            string source = Guid.NewGuid().ToString("D") + DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);
            byte[] sourceBytes = System.Text.Encoding.UTF8.GetBytes(source);
            return System.Convert.ToBase64String(sourceBytes);
        }
    }
}
