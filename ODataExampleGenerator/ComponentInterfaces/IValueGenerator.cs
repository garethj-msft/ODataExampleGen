// <copyright file="IValueGenerator.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using Microsoft.OData;
using Microsoft.OData.Edm;

namespace ODataExampleGenerator
{
    public interface IValueGenerator
    {
        int GetNextRandom(int scope);

        int GetNextMonotonicId();

        string GetNextId(string provider);

        ODataProperty GetExamplePrimitiveProperty(
           IEdmStructuredType hostType,
           IEdmStructuralProperty p);
    }
}