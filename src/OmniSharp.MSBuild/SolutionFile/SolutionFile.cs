// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.CodeAnalysis.MSBuild
{
    public class SolutionFile
    {
        internal SolutionFile(
            IEnumerable<string> headerLines,
            string visualStudioVersionLineOpt,
            string minimumVisualStudioVersionLineOpt,
            IEnumerable<ProjectBlock> projectBlocks,
            IEnumerable<SectionBlock> globalSectionBlocks)
        {
            if (headerLines == null)
            {
                throw new ArgumentNullException(nameof(headerLines));
            }

            if (projectBlocks == null)
            {
                throw new ArgumentNullException(nameof(projectBlocks));
            }

            if (globalSectionBlocks == null)
            {
                throw new ArgumentNullException(nameof(globalSectionBlocks));
            }

            HeaderLines = headerLines.ToList();
            VisualStudioVersionLineOpt = visualStudioVersionLineOpt;
            MinimumVisualStudioVersionLineOpt = minimumVisualStudioVersionLineOpt;
            ProjectBlocks = projectBlocks.ToList();
            GlobalSectionBlocks = globalSectionBlocks.ToList();
        }

        public IEnumerable<string> HeaderLines { get; }

        public string VisualStudioVersionLineOpt { get; }

        public string MinimumVisualStudioVersionLineOpt { get; }

        public IEnumerable<ProjectBlock> ProjectBlocks { get; }

        public IEnumerable<SectionBlock> GlobalSectionBlocks { get; }

        public string GetText()
        {
            var builder = new StringBuilder();

            builder.AppendLine();

            foreach (var headerLine in HeaderLines)
            {
                builder.AppendLine(headerLine);
            }

            foreach (var block in ProjectBlocks)
            {
                builder.Append(block.GetText());
            }

            builder.AppendLine("Global");

            foreach (var block in GlobalSectionBlocks)
            {
                builder.Append(block.GetText(indent: 1));
            }

            builder.AppendLine("EndGlobal");

            return builder.ToString();
        }

        public static SolutionFile Parse(TextReader reader)
        {
            var headerLines = new List<string>();

            var headerLine1 = GetNextNonEmptyLine(reader);
            if (headerLine1 == null || !headerLine1.StartsWith("Microsoft Visual Studio Solution File"))
            {
                //throw new Exception(string.Format(WorkspacesResources.MissingHeaderInSolutionFile, "Microsoft Visual Studio Solution File"));
                throw new Exception();
            }

            headerLines.Add(headerLine1);

            // skip comment lines and empty lines
            while (reader.Peek() != -1 && "#\r\n".Contains((char)reader.Peek()))
            {
                headerLines.Add(reader.ReadLine());
            }

            string visualStudioVersionLineOpt = null;
            if (reader.Peek() == 'V')
            {
                visualStudioVersionLineOpt = GetNextNonEmptyLine(reader);
                if (!visualStudioVersionLineOpt.StartsWith("VisualStudioVersion"))
                {
                    //throw new Exception(string.Format(WorkspacesResources.MissingHeaderInSolutionFile, "VisualStudioVersion"));
                    throw new Exception();
                }
            }

            string minimumVisualStudioVersionLineOpt = null;
            if (reader.Peek() == 'M')
            {
                minimumVisualStudioVersionLineOpt = GetNextNonEmptyLine(reader);
                if (!minimumVisualStudioVersionLineOpt.StartsWith("MinimumVisualStudioVersion"))
                {
                    //throw new Exception(string.Format(WorkspacesResources.MissingHeaderInSolutionFile, "MinimumVisualStudioVersion"));
                    throw new Exception();
                }
            }

            var projectBlocks = new List<ProjectBlock>();

            // Parse project blocks while we have them
            while (reader.Peek() == 'P')
            {
                projectBlocks.Add(ProjectBlock.Parse(reader));
                while (reader.Peek() != -1 && "#\r\n".Contains((char)reader.Peek()))
                {
                    // Comments and Empty Lines between the Project Blocks are skipped
                    reader.ReadLine();
                }
            }

            // We now have a global block
            var globalSectionBlocks = ParseGlobal(reader);

            // We should now be at the end of the file
            if (reader.Peek() != -1)
            {
                //throw new Exception(WorkspacesResources.MissingEndOfFileInSolutionFile);
                throw new Exception();
            }

            return new SolutionFile(headerLines, visualStudioVersionLineOpt, minimumVisualStudioVersionLineOpt, projectBlocks, globalSectionBlocks);
        }

        //[SuppressMessage("", "RS0001")] // TODO: This suppression should be removed once we have rulesets in place for Roslyn.sln
        private static IEnumerable<SectionBlock> ParseGlobal(TextReader reader)
        {
            if (reader.Peek() == -1)
            {
                return Enumerable.Empty<SectionBlock>();
            }

            if (GetNextNonEmptyLine(reader) != "Global")
            {
                //throw new Exception(string.Format(WorkspacesResources.MissingLineInSolutionFile, "Global"));
                throw new Exception();
            }

            var globalSectionBlocks = new List<SectionBlock>();

            // The blocks inside here are indented
            while (reader.Peek() != -1 && char.IsWhiteSpace((char)reader.Peek()))
            {
                globalSectionBlocks.Add(SectionBlock.Parse(reader));
                ConsumeEmptyLines(reader);
            }

            if (GetNextNonEmptyLine(reader) != "EndGlobal")
            {
                //throw new Exception(string.Format(WorkspacesResources.MissingLineInSolutionFile, "EndGlobal"));
                throw new Exception();
            }

            ConsumeEmptyLines(reader);
            return globalSectionBlocks;
        }

        private static void ConsumeEmptyLines(TextReader reader)
        {
            // Consume potential empty lines at the end of the global block
            while (reader.Peek() != -1 && "\r\n".Contains((char)reader.Peek()))
            {
                reader.ReadLine();
            }
        }

        private static string GetNextNonEmptyLine(TextReader reader)
        {
            string line = null;

            do
            {
                line = reader.ReadLine();
            }
            while (line != null && line.Trim() == string.Empty);

            return line;
        }
    }
}
