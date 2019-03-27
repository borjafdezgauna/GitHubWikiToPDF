using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SimionSrcParser
{
    class MainApp
    {
        const string argSourcePrefix = "lang=";
        const string argSourceDirPrefix = "source-dir=";
        const string argOutDirPrefix = "out-dir=";

        static string sourcesLanguage = "cpp"; //default value
        static List<string> sourceDirectories = new List<string>();

        static string outputDocsFolder = null;

        static bool ParseArguments(string [] args)
        {
            //lang=[cpp|csharp] source-dir=<dir> [out-docs=<path-to-md>]
            foreach (string arg in args)
            {
                if (arg.StartsWith(argSourcePrefix)) sourcesLanguage = arg.Substring(argSourcePrefix.Length);
                else if (arg.StartsWith(argSourceDirPrefix)) sourceDirectories.Add(arg.Substring(argSourceDirPrefix.Length));
                else if (arg.StartsWith(argOutDirPrefix)) outputDocsFolder = arg.Substring(argOutDirPrefix.Length);
            }
            if (sourceDirectories.Count == 0 || outputDocsFolder == null)
                return false;   //all required arguments were not provided

            return true;        //all required arguments were provided
        }
        static int Main(string[] args)
        {
            //not to read 23.232 as 23232
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            if (!ParseArguments(args))
            {
                Console.WriteLine("ERROR. Usage: Documenter lang=[cpp|csharp] source-dir=<dir> [out-dir=<path-to-md>]");
                return 0;
            }

            //Create the appropriate parser
            SimionSrcParser parser = null;
            if (sourcesLanguage == "csharp")
                parser = new CSharpSourceParser();
            else parser = new CppSourceParser();

            foreach (string sourceDir in sourceDirectories)
            {
                if (Directory.Exists(sourceDir))
                    parser.ParseSourceFilesInDir(sourceDir);
                else Console.WriteLine("ERROR. Directory doesn't exist: " + sourceDir);
            }

            int numErrors = parser.PostProcess();

            if (numErrors==0)
            {
                string inputSourceDirectories = "{ ";
                foreach (string sourceDir in sourceDirectories) inputSourceDirectories += sourceDir + "; ";
                inputSourceDirectories += "}";

                if (outputDocsFolder != null && parser.GetObjectClasses() != null)
                {
                    //Save documentation as markdown
                    MarkdownExporter markdownExporter = new MarkdownExporter();
                    Console.WriteLine("Saving source code documentation as Markdown: " + inputSourceDirectories + " ->"
                        + outputDocsFolder + "Home" + markdownExporter.FormatExtension());
                    DocumentationExporter.ExportDocumentation(outputDocsFolder, markdownExporter, parser.GetObjectClasses());
                }

                Console.WriteLine("Finished: {0} Kbs of code read.", parser.GetNumBytesProcessed() / 1000);
            }
            return 0;
        }
    }
}