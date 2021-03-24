// <copyright file="JsonPrettyPrinter.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

namespace ODataExampleGenerator
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Print nicely formatted Json, as OData serializer writer won't do that.
    /// <remarks>Also remove ID elements that can't be removed at serialization time.</remarks>
    /// </summary>
    internal class JsonPrettyPrinter
    {
        /// <summary>
        /// Get a tidied up string representation of the raw OData bytes.
        /// </summary>
        internal static string PrettyPrint(byte[] jsonBytes, GenerationParameters generationParameters)
        {
            string[] skipProperties = generationParameters.GenerationStyle == GenerationStyle.Request ? new[] {"@odata.context", "id"} : new string[0];

            using var doc = JsonDocument.Parse(
                jsonBytes,
                new JsonDocumentOptions
                    {AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 2000});
            using var outStream = new MemoryStream();
            using (var outWriter = new Utf8JsonWriter(outStream, new JsonWriterOptions {Indented = true}))
            {
                WriteJsonObject(outWriter, doc.RootElement, skipProperties);
            }

            outStream.Flush();
            string output = Encoding.UTF8.GetString(outStream.ToArray());
            return output;
        }

        private static void WriteJsonObject(Utf8JsonWriter outWriter, JsonElement element, string[] skipProperties)
        {
            var nodes = element.EnumerateObject()
                .Where(e => !skipProperties.Contains(e.Name, StringComparer.OrdinalIgnoreCase));

            outWriter.WriteStartObject();
            foreach (var node in nodes)
            {
                outWriter.WritePropertyName(node.Name);
                WriteJsonElement(outWriter, node.Value, skipProperties);
            }

            outWriter.WriteEndObject();
        }

        private static void WriteJsonElement(Utf8JsonWriter outWriter, JsonElement element, string[] skipProperties)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                WriteJsonObject(outWriter, element, skipProperties);
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                WriteJsonArray(outWriter, element, skipProperties);
            }
            else
            {
                element.WriteTo(outWriter);
            }
        }

        private static void WriteJsonArray(Utf8JsonWriter outWriter, JsonElement element, string[] skipProperties)
        {
            outWriter.WriteStartArray();
            foreach (var arrayNode in element.EnumerateArray())
            {
                WriteJsonElement(outWriter, arrayNode, skipProperties);
            }

            outWriter.WriteEndArray();
        }
    }
}