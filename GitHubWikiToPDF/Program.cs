﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PdfSharp;
using PdfSharp.Drawing;
//using ExpertPdf.HtmlToPdf.PdfDocument;
//using OpenHtmlToPdf;
//using PdfSharp.Pdf;
//using IronPdf;
//using TheArtOfDev.HtmlRenderer.PdfSharp;
//using iTextSharp;
//using iTextSharp.text;
//using iTextSharp.text;
using PdfSharp.Pdf;

namespace GitHubWikiToPDF
{
    class Program
    {
        const string userNameArg = "-user=";
        static string userName = null;
        const string projectNameArg = "-project=";
        static string projectName = null;
        const string authorNameArg = "-author=";
        static string authorName = null;
        const string inputFileArg = "-input-file=";
        static string inputFile= null;
        const string outputFileArg = "-output-file=";
        static string outputFile = null;

        static string projectDescription = null;
        static string tempFolder;
        static string markDownInputFolder;

        enum ExecutionMode { Undefined, GitHubWikiToPDF, LocalMarkdownFileToPDF};
        static ExecutionMode executionMode= ExecutionMode.Undefined;

        static bool ParseArguments(string [] args)
        {
            foreach(string arg in args)
            {
                if (arg.StartsWith(projectNameArg)) projectName = arg.Substring(projectNameArg.Length);
                else if (arg.StartsWith(userNameArg)) userName = arg.Substring(userNameArg.Length);
                else if (arg.StartsWith(inputFileArg)) inputFile = arg.Substring(inputFileArg.Length);
                else if (arg.StartsWith(outputFileArg)) outputFile = arg.Substring(outputFileArg.Length);
                else if (arg.StartsWith(authorNameArg)) authorName = arg.Substring(authorNameArg.Length);
            }

            if (projectName != null && userName != null && outputFile != null)
            {
                projectDescription = "https://github.com/" + userName + "/" + projectName; //
                tempFolder = projectName;
                markDownInputFolder = tempFolder;
                inputFile = "Home.md";
                executionMode = ExecutionMode.GitHubWikiToPDF;
                return true; //GitHub wiki -> PDF mode
            }
            if (inputFile != null && outputFile != null)
            {
                tempFolder = "tmp";
                string inputDocName = Path.GetFileNameWithoutExtension(inputFile);
                markDownInputFolder = Path.GetDirectoryName(inputFile);
                inputFile = Path.GetFileName(inputFile) ;
                executionMode = ExecutionMode.LocalMarkdownFileToPDF;
                return true; //Local Markdown file -> PDF mode
            }
            return false; //error parsing arguments
        }
        static void Main(string[] args)
        {
            if (!ParseArguments(args))
            {
                Console.WriteLine("ERROR. Incorrect arguments.");
                Console.WriteLine("Usage: GitHubWikiToPDF [-user=<github-user> -project=<github-project> | -input-file=<input-file (.md)]> -output-file=<output-file (.pdf)>");
                Console.WriteLine("Use examples:");
                Console.WriteLine("\ta) Download and convert a GitHub wiki: GitHubWikiToPDF -user=simionsoft -project=SimionZoo -output-file=SimionZoo.pdf");
                Console.WriteLine("\tb) Convert a local markdown file: GitHubWikiToPDF -input-file=../myLocalFile.md -output-file=myLocalFile.pdf");
                return;
            }

            if (!Directory.Exists(tempFolder))
                Directory.CreateDirectory(tempFolder);

            //Download the last version of the wiki
            Console.WriteLine("\n#### 1. Downloading the last version of the wiki");
            if (executionMode == ExecutionMode.GitHubWikiToPDF)
            {
                GitHubWikiDownloader downloader = new GitHubWikiDownloader();
                downloader.CloneWikiGitRepo(userName + "/" + projectName, tempFolder);
            }
            else
            {
                Console.WriteLine("Skipped");
            }

            //Convert it to PDF
            Console.WriteLine("\n#### 2. Converting markdown files to a single .pdf file");

            WikiToPDFConverter markDownWikiToPDFConverter = new WikiToPDFConverter();

            markDownWikiToPDFConverter.CreatePDFDocument(projectName, projectDescription, authorName, "Created with GitHubWikiToPDF");

            markDownWikiToPDFConverter.Convert(markDownInputFolder, inputFile, tempFolder);

            if (!outputFile.EndsWith(".pdf")) outputFile += ".pdf";
            markDownWikiToPDFConverter.SavePDFDocument(outputFile);

            if (File.Exists(outputFile))
                Process.Start(outputFile);
        }
    }
}
