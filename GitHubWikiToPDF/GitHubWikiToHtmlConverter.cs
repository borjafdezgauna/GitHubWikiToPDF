﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading.Tasks;

namespace GitHubWikiToPDF
{
    class GitHubWikiToHtmlConverter
    {
        List<string> ConvertedPages = new List<string>();
        List<string> LinkedPages = new List<string>();
        List<string> DownloadedImages = new List<string>();

        WebClient webClient= new WebClient();

        int m_numOpenLists = 0;
        int m_openListLevel = 0;
        string OpenList(int level)
        {
            if (level < m_openListLevel)
            {
                //close one list
                m_numOpenLists--;
                m_openListLevel = level;
                return "</ul>";
            }
            if (m_numOpenLists > 0 && level == m_openListLevel) return "";
            m_openListLevel = level;
            m_numOpenLists++;
            return "<ul>";

        }
        string CloseAllOpenLists()
        {
            string closingTags;
            if (m_numOpenLists == 0) return "";
            closingTags = "";
            for (int i = 0; i < m_numOpenLists; i++)
                closingTags += "</ul>";
            m_numOpenLists = 0;
            m_openListLevel = 0;
            return closingTags;
        }

        string AsItemList(string line, int level) { return OpenList(level) + "<li>" + line.Substring(2) + "</li>"; }
        string AsParagraph(string line) { return "<p>" + line + "</p>"; }

        void DownloadImage(string url, string localFile)
        {
            string outputFolder = Path.GetDirectoryName(localFile);
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);
            webClient.DownloadFile(url, localFile);
        }
        string FromUrlToLocalFile(string url, string localFolder)
        {
            string localFile = localFolder;
            if (!localFile.EndsWith("\\") && !localFile.EndsWith("/"))
                localFile += "/";
            return localFile + "img/" + Path.GetFileName(url);
        }
        string FromUrlToLocalFileRelativeToHtml(string url)
        {
            return "img/" + Path.GetFileName(url);
        }

        string ParseImages(string line, string regExpr, string folder)
        {
            Match match = Regex.Match(line, regExpr);
            while (match.Success)
            {
                string text = match.Groups[1].Value;
                string url = match.Groups[2].Value;
                string localFile = FromUrlToLocalFile(url, folder);
                if (!DownloadedImages.Contains(url))
                {
                    DownloadImage(url, localFile);
                    DownloadedImages.Add(url);
                }
                string htmlLink = "<img src=\"" + FromUrlToLocalFileRelativeToHtml(url) + "\" alt=\"" + text + "\">";
                line = line.Substring(0, match.Index) + htmlLink + line.Substring(match.Index + match.Length);

                match = Regex.Match(line, regExpr);
            }
            return line;
        }

        string WikifyLink(string url)
        {
            return url.ToLower().Replace(' ', '-');
        }
        string LinkToAnchorName(string url)
        {
            int lastSlash = Math.Max(url.LastIndexOf('/'), url.LastIndexOf('\\'));
            if (lastSlash >= 0)
                return url.Substring(lastSlash + 1);
            return url;
        }

        string ParseLinks(string line, string regExpr)
        {
            Match match = Regex.Match(line, regExpr);
            while (match.Success)
            {
                string text = match.Groups[1].Value;
                string wikiLink;

                if (match.Groups.Count > 2)
                    wikiLink = match.Groups[2].Value;
                else wikiLink = text;

                if (!wikiLink.StartsWith("http"))
                {
                    wikiLink = WikifyLink(wikiLink);
                    //Add to the list of linked pages
                    LinkedPages.Add(wikiLink + ".md");
                }

                //We are merging all source documents to a single one, so links within the wiki need to be converted to anchors
                string htmlLink= "<a href=\"#" + LinkToAnchorName(wikiLink) + "\">" + text + "</a>";
                line = line.Substring(0, match.Index) + htmlLink + line.Substring(match.Index + match.Length);

                match = Regex.Match(line, regExpr);
            }
            return line;
        }
        string AsTitle(int level, string text)
        {
            if (level <= 2)
                return "<h" + level + "><a name=\"" + LinkToAnchorName(WikifyLink(text)) + "\">" + text + "</a></h" + level + ">";
            else
                return "<h" + level + ">" + text + "</h" + level + ">";
        }
        string Dehtmlfy(string line)
        {
            string output = "";
            foreach (char c in line.ToCharArray())
            {
                if (c == '<') output += "&lt;";
                else if (c == '>') output += "&gt;";
                else output += c;
            }
            return output;
        }

        bool IsCodeBlockOpen = false;
        string ConvertLinePrefixes(string line, int numIndents)
        {
            if (line.StartsWith("```"))
            {
                if (!IsCodeBlockOpen)
                {
                    IsCodeBlockOpen = true;
                    return "<pre><code>";
                }
                IsCodeBlockOpen = false;
                return "</code></pre>";
            }
            else if (IsCodeBlockOpen)
                return Dehtmlfy(line);
            //<h1> is reserved for the title of the document
            if (line.StartsWith("# ")) return CloseAllOpenLists() + AsTitle(2, line.Substring(2));
            if (line.StartsWith("## ")) return CloseAllOpenLists() + AsTitle(3, line.Substring(3));
            if (line.StartsWith("### ")) return CloseAllOpenLists() + AsTitle(4, line.Substring(4));
            if (line.StartsWith("#### ")) return CloseAllOpenLists() + AsTitle(5, line.Substring(5));
            if (line.StartsWith("> ")) return "<i>" + line.Substring(2) + "</i>";
            if (line.StartsWith("* ")) return AsItemList(line, numIndents);
            if (line.StartsWith("- ")) return AsItemList(line, numIndents);
            return AsParagraph(line);
            
        }

 
        string SubstitutePattern(string line, string regExpr, string outPrefix, string outPostfix)
        {
            Match match;
            match = Regex.Match(line, regExpr);
            while (match.Success)
            {
                string text = match.Groups[1].Value;

                if (match.Groups.Count > 2) // if the pattern includes a character after the main one, we add it to the output. See the pattern used for italics
                    line = line.Substring(0, match.Index) + outPrefix + text + outPostfix + match.Groups[2].Value + line.Substring(match.Index + match.Length);
                else line = line.Substring(0, match.Index) + outPrefix + text + outPostfix + line.Substring(match.Index + match.Length);

                match = Regex.Match(line, regExpr);
            }
            return line;
        }

        string DocNameFromFilename(string htmlDocFilename)
        {
            string docName = Path.GetFileNameWithoutExtension(htmlDocFilename);
            docName = docName.Replace('-', ' ');
            var result = Regex.Replace(docName, @"\b(\w)", m => m.Value.ToUpper());
            docName = Regex.Replace(result, @"(\s(of|in|by|and|the)|\'[st])\b", m => m.Value.ToLower(), RegexOptions.IgnoreCase);

            return docName;
        }

        int CountSpacesAtBeginning(string line)
        {
            int i = 0;
            while (i<line.Length && line[i] == ' ')
                i++;
            return i;
        }

        public void Convert(StreamWriter htmlWriter, string inputMarkdownFolder, string markdownDocFilename, string outputHtmlFolder, string cssFile = null, bool isRootDocument= true)
        {
            //we ignore external references
            List<string> ignoredPrefixes = new List<string>(){ "http://", "https://" };
            foreach (string ignoredPrefix in ignoredPrefixes)
            {
                if (markdownDocFilename.StartsWith(ignoredPrefix))
                    return;
            }
            //we ignore references to anchors
            if (markdownDocFilename.Contains("#"))
                return;

            string localFilename = inputMarkdownFolder + "\\" + markdownDocFilename;

            if (!File.Exists(localFilename))
            {
                Console.WriteLine("Warning: Invalid reference to " + markdownDocFilename + " found");
                return;
            }

            string[] lines = File.ReadAllLines(localFilename);

            if (lines == null)
            {
                Console.WriteLine("ERROR. Couldn't find referenced page: " + markdownDocFilename);
                return;
            }

            List<string> parsedLines = new List<string>();

            foreach (string line in lines)
            {
                int numIndents = 0;
                numIndents = CountSpacesAtBeginning(line);
                string parsedLine = line.Trim(' ');

                parsedLine = ConvertLinePrefixes(parsedLine, numIndents);

                //parse images, ALWAYS BEFORE REGULAR LINKS
                parsedLine = ParseImages(parsedLine, @"!\[([^\]]+)\]\(([^\)]+)\)", inputMarkdownFolder);
                //parse links
                parsedLine = ParseLinks(parsedLine, @"\[\[([^\]]+)\|([^\]]+)\]\]"); //[[text|url]]
                parsedLine = ParseLinks(parsedLine, @"\[([^\]]+)\]\(([^\)]+)\)"); //[text](url)
                parsedLine = ParseLinks(parsedLine, @"\[\[([^\]]+)\]\]"); //[[url]]
                //parse bolds
                parsedLine = SubstitutePattern(parsedLine, @"\*\*([^\*]+)\*\*", "<b>", "</b>");
                parsedLine = SubstitutePattern(parsedLine, @"\*([^\*]+)\*", "<b>", "</b>");
                //parse italics
                parsedLine = SubstitutePattern(parsedLine, @"_([^_]+)_(\s|,|\.|\:|\))", "<em>", "</em>");
                //parse code
                parsedLine = SubstitutePattern(parsedLine, @"`([^`]+)`", "<code>", "</code>");

                parsedLines.Add(parsedLine);
            }

            parsedLines.Add(CloseAllOpenLists()); //In case there is some un-closed list

            string title = DocNameFromFilename(localFilename);
            if (isRootDocument)
            {
                htmlWriter.Write("<html><header><title>" + title + "</title>");
                if (cssFile != null)
                {
                    File.Copy(cssFile, outputHtmlFolder + "\\" + Path.GetFileName(cssFile), true);
                    htmlWriter.Write("<link rel=\"stylesheet\" type=\"text/css\" href=\"" + Path.GetFileName(cssFile) + "\">");
                }
                htmlWriter.WriteLine("</header><body>");
            }
            
            htmlWriter.WriteLine(AsTitle(1, title));
            foreach (string line in parsedLines)
                htmlWriter.WriteLine(line);
           

            ConvertedPages.Add(markdownDocFilename);
            while (LinkedPages.Count > 0)
            {
                string linkedPage = LinkedPages[0];
                LinkedPages.RemoveAt(0);

                if (!ConvertedPages.Contains(linkedPage))
                    Convert(htmlWriter, inputMarkdownFolder, linkedPage, outputHtmlFolder, null, false);
            }

            if (isRootDocument)
                htmlWriter.WriteLine("</body></html>");
        }
    }
}
