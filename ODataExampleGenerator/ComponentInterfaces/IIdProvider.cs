﻿// <copyright file="IIdProvider.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace ODataExampleGenerator.ComponentInterfaces
{
    /// <summary>
    /// Interface for components that produce ID values.
    /// </summary>
    internal interface IIdProvider
    {
        /// <summary>
        /// Name of the provider.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Get a brand new Id.
        /// </summary>
        /// <returns></returns>
        string GetNewId();
    }
}
