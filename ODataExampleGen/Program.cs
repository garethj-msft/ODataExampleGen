using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CommandLine;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using ODataExampleGenerator;

namespace ODataExampleGen
{
    class Program
    {
        static Program()
        {
        }

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
                    UriToPost = options.UriToPost,
                };

                var exampleGenerator = new ExampleGenerator(generationParameters);

                generationParameters.PopulateModel(options.CsdlFile);
                generationParameters.PopulatePath();

                // Process the more complicated options into actionable structures.
                ProgramOptionsExtractor.PopulateChosenTypes(options, generationParameters);
                ProgramOptionsExtractor.PopulateChosenEnums(options, generationParameters);
                ProgramOptionsExtractor.PopulateChosenPrimitives(options, generationParameters);


                string output = exampleGenerator.CreateExample();
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
