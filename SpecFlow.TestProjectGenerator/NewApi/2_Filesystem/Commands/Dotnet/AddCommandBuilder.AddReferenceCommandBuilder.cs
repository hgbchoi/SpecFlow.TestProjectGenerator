﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpecFlow.TestProjectGenerator.NewApi._2_Filesystem.Commands.Dotnet
{
    public partial class AddCommandBuilder
    {
        public class AddReferenceCommandBuilder : BaseCommandBuilder
        {
            private string _projectFilePath;
            private readonly List<string> _referencedProjects = new List<string>();


            public AddReferenceCommandBuilder ToProject(string projectFilePath)
            {
                _projectFilePath = projectFilePath;
                return this;
            }

            public AddReferenceCommandBuilder ReferencingProject(string projectFilePath)
            {
                _referencedProjects.Add(projectFilePath);
                return this;
            }

            protected override string BuildArguments()
            {
                var projectDirectoryPath = Path.GetDirectoryName(_projectFilePath);
                var absoluteReferenceProject = _referencedProjects.Select(p => Path.Combine(projectDirectoryPath, p));
                return $"add {_projectFilePath} reference {string.Join(" ", absoluteReferenceProject)}";
            }

            public AddReferenceCommandBuilder(IOutputWriter outputWriter) : base(outputWriter)
            {
            }
        }
    }
}
