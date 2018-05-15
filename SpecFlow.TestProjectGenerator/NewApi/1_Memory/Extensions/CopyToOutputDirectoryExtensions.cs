﻿using System;

namespace SpecFlow.TestProjectGenerator.NewApi._1_Memory.Extensions
{
    public static class CopyToOutputDirectoryExtensions
    {
        public static string GetCopyToOutputDirectoryString(this CopyToOutputDirectory fileCopyToOutputDirectory)
        {
            switch (fileCopyToOutputDirectory)
            {
                case CopyToOutputDirectory.CopyIfNewer:
                    return "PreserveNewest";
                case CopyToOutputDirectory.CopyAlways:
                    return "Always";
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileCopyToOutputDirectory), fileCopyToOutputDirectory, null);
            }
        }
    }
}