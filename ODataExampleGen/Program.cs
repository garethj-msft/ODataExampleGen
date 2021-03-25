// <copyright file="Program.cs" company="Microsoft">
// © Microsoft. All rights reserved.
// </copyright>

using System.ComponentModel;
using System.Net.Http;

namespace ODataExampleGen
{
    using System;
    using System.Diagnostics;
    using CommandLine;
    using ODataExampleGenerator;

    public class Program
    {
        public static int Main(string[] args)
        {
            int result = Parser.Default.ParseArguments<ProgramOptions>(args).MapResult(RunCommand, _ => 1);
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
            }

            return result;
        }

        private static int RunCommand(ProgramOptions options)
        {
            try
            {
                var generationParameters = new GenerationParameters
                {
                    ServiceRoot = new Uri(options.BaseUrl, UriKind.Absolute),
                };

                generationParameters.Model = CsdlLoader.LoadModel(options.CsdlFile);

                try
                {
                    generationParameters.HttpMethod = new HttpMethod(options.Method);
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException($"Unknown method argument {options.Method}.");
                }

                // Process the more complicated options into actionable structures.
                ProgramOptionsExtractor.PopulateChosenTypes(options, generationParameters);
                ProgramOptionsExtractor.PopulateChosenEnums(options, generationParameters);
                ProgramOptionsExtractor.PopulateChosenPrimitives(options, generationParameters);
                ProgramOptionsExtractor.PopulateChosenIdProviders(options, generationParameters);

                var exampleGenerator = new ExampleGenerator(generationParameters);
                string output = exampleGenerator.CreateExample(options.UriForMethod);
                Console.WriteLine(output);
                return 0;
            }
            catch (InvalidOperationException ioe)
            {
                Console.WriteLine(ioe.Message);
                return 1;
            }
            catch (Exception)
            {
                return 1;
            }
        }
    }
}
