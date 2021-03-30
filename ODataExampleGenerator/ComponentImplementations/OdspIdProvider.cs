// <copyright file="ExchangeIdProvider.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Primitives;
using Microsoft.OData.Edm;
using ODataExampleGenerator.ComponentInterfaces;

namespace ODataExampleGenerator.ComponentImplementations
{
    internal class OdspIdProvider : IIdProvider
    {
        private readonly Random rand = new ((int)DateTimeOffset.UtcNow.Ticks);

        public string Name => "Odsp";

        public string GetNewId(IEdmStructuredType host)
        {
            if (((IEdmNamedElement)host).Name.EqualsOic("Drive"))
            {
                // b!Hpsovc0KS0CPIsn-WFNR8GhV9dMxb2tCsYvkwOKABNKFLTswmPZLRoagnK9DpYwL
                StringBuilder idBuilder = new();
                idBuilder.Append("b!");
                byte[] asciiId = this.GetRandomAsciiBytes(15, true, true, true);
                idBuilder.Append(Encoding.ASCII.GetString(asciiId));
                idBuilder.Append("-");
                asciiId = this.GetRandomAsciiBytes(48, true, true, true);
                idBuilder.Append(Encoding.ASCII.GetString(asciiId));
                return idBuilder.ToString();
            }
            else if (((IEdmNamedElement)host).Name.EqualsOic("DriveItem"))
            {
                // 01GIXM3Y6SFRTTGAJ3PFGYABIDORF6HIVV
                byte[] asciiId = this.GetRandomAsciiBytes(34, false, true, true);
                return Encoding.ASCII.GetString(asciiId);
            }
            else if (((IEdmNamedElement)host).Name.EqualsOic("Site"))
            {
                // microsoft.sharepoint-df.com,e444a94f-ef3e-471f-9d17-2ab595ba641e,c3f0bcb7-4e44-447a-9024-29665db878c6
                StringBuilder idBuilder = new();
                byte[] asciiId = this.GetRandomAsciiBytes(9, true, false, false);
                idBuilder.Append(Encoding.ASCII.GetString(asciiId));
                idBuilder.Append(",");
                idBuilder.Append(Guid.NewGuid().ToString("D"));
                idBuilder.Append(",");
                idBuilder.Append(Guid.NewGuid().ToString("D"));
                return idBuilder.ToString();
            }
            else
            {
                return Guid.NewGuid().ToString("D");
            }
        }

        private byte[] GetRandomAsciiBytes(int length, bool includeLowerAlpha, bool includeUpperAlpha, bool includeNumeric)
        {
            if ( !(includeLowerAlpha || includeUpperAlpha || includeUpperAlpha))
            {
                throw new ArgumentException("Must include one of the range types.");
            }

            byte lowerAVal = Encoding.ASCII.GetBytes("a")[0];
            byte upperAVal = Encoding.ASCII.GetBytes("A")[0];
            byte zeroVal = Encoding.ASCII.GetBytes("0")[0];
            int randRange = (includeLowerAlpha ? 26 : 0) + (includeUpperAlpha ? 26 : 0) + (includeNumeric ? 10 : 0);


            byte[] asciiId = new byte[length];

            for (int i = 0; i < length; i++)
            {
                byte charRand = (byte)this.rand.Next(randRange);
                asciiId[i] = charRand switch
                {
                    byte when includeLowerAlpha && includeUpperAlpha && includeNumeric && charRand < 26 => (byte)(lowerAVal + charRand),
                    byte when includeLowerAlpha && includeUpperAlpha && includeNumeric && charRand < 52 => (byte)(upperAVal + charRand - 26),
                    byte when includeLowerAlpha && includeUpperAlpha && includeNumeric => (byte)(zeroVal + charRand - 52),
                    byte when includeLowerAlpha && includeUpperAlpha && !includeNumeric && charRand < 26 => (byte)(lowerAVal + charRand),
                    byte when includeLowerAlpha && includeUpperAlpha && !includeNumeric => (byte)(upperAVal + charRand - 26),
                    byte when includeLowerAlpha && !includeUpperAlpha && includeNumeric && charRand < 26 => (byte)(lowerAVal + charRand),
                    byte when includeLowerAlpha && !includeUpperAlpha && includeNumeric => (byte)(zeroVal + charRand - 26),
                    byte when !includeLowerAlpha && includeUpperAlpha && includeNumeric && charRand < 26 => (byte)(upperAVal + charRand),
                    byte when !includeLowerAlpha && includeUpperAlpha && includeNumeric => (byte)(zeroVal + charRand - 26),
                    byte when includeLowerAlpha && !includeUpperAlpha && !includeNumeric => (byte)(lowerAVal + charRand),
                    byte when !includeLowerAlpha && includeUpperAlpha && !includeNumeric => (byte)(upperAVal + charRand),
                    byte when !includeLowerAlpha && !includeUpperAlpha && includeNumeric => (byte)(zeroVal + charRand),
                    _ => throw new InvalidOperationException("Invalid combination/bug."),
                };
            }

            return asciiId;
        }
    }
}
