//---------------------------------------------------------------------
// <copyright file="InMemoryMessage.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.OData;

namespace ODataExampleGenerator
{
    internal class InMemoryMessage : IODataRequestMessage, IODataResponseMessage, IContainerProvider, IDisposable
    {
        private readonly Dictionary<string, string> headers = new Dictionary<string, string>();

        public IEnumerable<KeyValuePair<string, string>> Headers => this.headers;

        public int StatusCode { get; set; }

        public Uri Url { get; set; }

        public string Method { get; set; }

        public Stream Stream { get; set; }

        public IServiceProvider Container { get; set; }

        public string GetHeader(string headerName)
        {
            return this.headers.TryGetValue(headerName, out var headerValue) ? headerValue : null;
        }

        public void SetHeader(string headerName, string headerValue)
        {
            this.headers[headerName] = headerValue;
        }

        public Stream GetStream()
        {
            return this.Stream;
        }

        public Action DisposeAction { get; set; }

        void IDisposable.Dispose()
        {
            this.DisposeAction?.Invoke();
        }
    }
}