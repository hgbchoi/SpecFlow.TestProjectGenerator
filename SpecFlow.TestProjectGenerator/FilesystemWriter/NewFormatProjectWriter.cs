﻿using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using TechTalk.SpecFlow.TestProjectGenerator.Data;
using TechTalk.SpecFlow.TestProjectGenerator.Dotnet;
using TechTalk.SpecFlow.TestProjectGenerator.Extensions;
using TechTalk.SpecFlow.TestProjectGenerator.Helpers;

namespace TechTalk.SpecFlow.TestProjectGenerator.FilesystemWriter
{
    public class NewFormatProjectWriter : IProjectWriter
    {
        private readonly IOutputWriter _outputWriter;

        public NewFormatProjectWriter(IOutputWriter outputWriter)
        {
            _outputWriter = outputWriter;
        }

        public virtual string WriteProject(Project project, string projRootPath)
        {
            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            CreateProjectFile(project, projRootPath);

            string projFileName = $"{project.Name}.{project.ProgrammingLanguage.ToProjectFileExtension()}";

            string projectFilePath = Path.Combine(projRootPath, projFileName);

            var xd = XDocument.Load(projectFilePath);
            var projectElement = xd.Element(XName.Get("Project")) ?? throw new ProjectCreationNotPossibleException($"No 'Project' tag could be found in project file '{projectFilePath}'");

            AdjustForASPNetCore(project, projectElement);
            SetTargetFramework(project, projectElement);
            WriteAssemblyReferences(project, projectElement);
            WriteNuGetPackages(project, projectElement);
            WriteFileReferences(project, projectElement);

            xd.Save(projectFilePath);

            WriteProjectFiles(project, projRootPath);

            return projectFilePath;
        }

        private void AdjustForASPNetCore(Project project, XElement projectElement)
        {
            if (project.NuGetPackages.Any(n => n.Name == "Microsoft.AspNetCore.App"))
            {
                projectElement.Attribute("Sdk").SetValue("Microsoft.NET.Sdk.Web");

                var itemGroup = new XElement("ItemGroup");
                using (var xw = itemGroup.CreateWriter())
                {
                    xw.WriteStartElement("Content");
                    xw.WriteAttributeString("Remove", "*.cshtml");
                    xw.WriteEndElement();
                }

                projectElement.Add(itemGroup);
            }
            
        }

        private void WriteFileReferences(Project project, XElement projectElement)
        {
            bool created = false;

            var itemGroup = new XElement("ItemGroup");
            using (var xw = itemGroup.CreateWriter())
            {
                if (project.ProgrammingLanguage == ProgrammingLanguage.FSharp)
                {
                    foreach (var file in project.Files.Where(f => f.BuildAction.ToUpper() == "COMPILE"))
                    {
                        WriteFileReference(xw, file);
                        created = true;
                    }
                }

                foreach (var file in project.Files.Where(f => f.BuildAction.ToUpper() == "CONTENT" || f.BuildAction.ToUpper() == "NONE" && (f.CopyToOutputDirectory != CopyToOutputDirectory.DoNotCopy || f.AdditionalMsBuildProperties.Any())))
                {
                    WriteFileReference(xw, file);
                    created = true;
                }
            }

            if (created)
            {
                projectElement.Add(itemGroup);
            }
        }

        private void WriteFileReference(XmlWriter xw, ProjectFile projectFile)
        {
            xw.WriteStartElement(projectFile.BuildAction);
            xw.WriteAttributeString("Include", projectFile.Path);

            if (projectFile.CopyToOutputDirectory != CopyToOutputDirectory.DoNotCopy)
            {
                xw.WriteElementString("CopyToOutputDirectory", projectFile.CopyToOutputDirectory.GetCopyToOutputDirectoryString());
            }

            foreach (var additionalMsBuildProperty in projectFile.AdditionalMsBuildProperties)
            {
                xw.WriteElementString(additionalMsBuildProperty.Key, additionalMsBuildProperty.Value);
            }

            xw.WriteEndElement();
        }

        public void WriteReferences(Project project, string projectFilePath)
        {
            WriteProjectReferences(project, projectFilePath);
        }

        private void WriteNuGetPackages(Project project, XElement projectElement)
        {
            if (!project.NuGetPackages.Any())
            {
                return;
            }

            var newNode = new XElement("ItemGroup");

            using (var xw = newNode.CreateWriter())
            {
                foreach (var nugetPackage in project.NuGetPackages)
                {
                    WritePackageReference(xw, nugetPackage);
                }
            }

            projectElement.Add(newNode);
        }

        private void WritePackageReference(XmlWriter xw, NuGetPackage nuGetPackage)
        {
            xw.WriteStartElement("PackageReference");
            xw.WriteAttributeString("Include", nuGetPackage.Name);

            if (nuGetPackage.Version.IsNotNullOrWhiteSpace())
            {
                xw.WriteAttributeString("Version", nuGetPackage.Version);
            }

            xw.WriteEndElement();
        }

        private void WriteProjectReferences(Project project, string projFilePath)
        {
            if (project.ProjectReferences.Count > 0)
            {
                var reference = DotNet.Add(_outputWriter)
                                      .Reference();
                foreach (var projReference in project.ProjectReferences)
                {
                    reference.ReferencingProject(projReference.Path);
                }

                reference.ToProject(projFilePath)
                         .Build()
                         .Execute(innerException => new ProjectCreationNotPossibleException($"Writing ProjectRefences failed.", innerException));
            }
        }

        private void WriteProjectFiles(Project project, string projRootPath)
        {
            var fileWriter = new ProjectFileWriter();
            foreach (var file in project.Files)
            {
                fileWriter.Write(file, projRootPath);
            }
        }

        private void SetTargetFramework(Project project, XElement projectElement)
        {
            var targetFrameworkElement = projectElement.Element("PropertyGroup")?.Element("TargetFramework") ?? throw new ProjectCreationNotPossibleException();

            string newTargetFrameworks = project.TargetFrameworks.ToTargetFrameworkMoniker();
            targetFrameworkElement.SetValue(newTargetFrameworks);
        }

        private void WriteAssemblyReferences(Project project, XElement projectElement)
        {
            if (!project.References.Any())
            {
                return;
            }

            // GAC and library references cannot be added in new Csproj format (via dotnet CLI)
            // see https://github.com/dotnet/sdk/issues/987
            // Therefore, write them manually into the project file
            var itemGroup = new XElement("ItemGroup");

            using (var xw = itemGroup.CreateWriter())
            {
                foreach (var reference in project.References)
                {
                    WriteAssemblyReference(xw, reference);
                }
            }

            projectElement.Add(itemGroup);
        }

        private void WriteAssemblyReference(XmlWriter xw, Reference reference)
        {
            xw.WriteStartElement("Reference");
            xw.WriteAttributeString("Include", reference.Name);
            xw.WriteEndElement();
        }

        private void CreateProjectFile(Project project, string projRootPath)
        {
            string template;

            switch (project.ProjectType)
            {
                case ProjectType.Library:
                    template = "classlib";
                    break;
                case ProjectType.Exe:
                    template = "console";
                    break;
                case ProjectType.ASPNetCore:
                    template = "web";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(project.ProjectType), $"ProjectType {project.ProjectType} is not supported");
            }

            

            var newProjCommand = DotNet.New(_outputWriter)
                                       .Project()
                                       .InFolder(projRootPath)
                                       .WithName(project.Name)
                                       .UsingTemplate(template)
                                       .WithLanguage(project.ProgrammingLanguage)
                                       .Build();

            newProjCommand.Execute(innerExceptions => new ProjectCreationNotPossibleException("Execution of dotnet new failed.", innerExceptions));
        }
    }
}
