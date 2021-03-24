using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ODataExampleGenerator.ComponentInterfaces;

namespace ODataExampleGenerator.ComponentImplementations
{
    class ExchangeIdProvider : IIdProvider
    {
        public string Name => nameof(ExchangeIdProvider);

        public string GetNewId()
        {
            string source = Guid.NewGuid().ToString("D") + DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);
            var sourceBytes = System.Text.Encoding.UTF8.GetBytes(source);
            return System.Convert.ToBase64String(sourceBytes);
        }
    }
}
