using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ExcelDna.AddIn.Tasks.Logging;
using ExcelDna.AddIn.Tasks.Utils;

namespace ExcelDna.AddIn.Tasks
{
    public class CreateExcelAddIn : AbstractTask
    {
        private readonly IBuildLogger _log;
        private readonly IExcelDnaFileSystem _fileSystem;
        private ITaskItem[] _configFilesInProject;
        private List<ITaskItem> _dnaFilesToPack;
        private BuildTaskCommon _common;

        public CreateExcelAddIn()
        {
            _log = new BuildLogger(this, "ExcelDnaBuild");
            _fileSystem = new ExcelDnaPhysicalFileSystem();
        }

        internal CreateExcelAddIn(IBuildLogger log, IExcelDnaFileSystem fileSystem)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public override bool Execute()
        {
            try
            {
                _log.Debug("Running CreateExcelAddIn MSBuild Task");

                LogDiagnostics();

                RunSanityChecks();

                _dnaFilesToPack = new List<ITaskItem>();
                DnaFilesToPack = new ITaskItem[0];

                FilesInProject = FilesInProject ?? new ITaskItem[0];
                _log.Debug("Number of files in project: " + FilesInProject.Length);

                _configFilesInProject = GetConfigFilesInProject();
                _common = new BuildTaskCommon(FilesInProject, OutDirectory, FileSuffix32Bit, FileSuffix64Bit, ProjectName, AddInFileName);

                var buildItemsForDnaFiles = _common.GetBuildItemsForDnaFiles();

                TryBuildAddInFor32Bit(buildItemsForDnaFiles);

                _log.Information("---", MessageImportance.High);

                TryBuildAddInFor64Bit(buildItemsForDnaFiles);

                DnaFilesToPack = _dnaFilesToPack.ToArray();

                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, ex.Message);
                _log.Error(ex, ex.ToString());
                return false;
            }
        }

        private void LogDiagnostics()
        {
            _log.Debug("----Arguments----");
            _log.Debug("FilesInProject: " + (FilesInProject ?? new ITaskItem[0]).Length);

            if (FilesInProject != null)
            {
                foreach (var f in FilesInProject)
                {
                    _log.Debug($"  {f.ItemSpec}");
                }
            }

            _log.Debug("OutDirectory: " + OutDirectory);
            _log.Debug("Xll32FilePath: " + Xll32FilePath);
            _log.Debug("Xll64FilePath: " + Xll64FilePath);
            _log.Debug("Create32BitAddIn: " + Create32BitAddIn);
            _log.Debug("Create64BitAddIn: " + Create64BitAddIn);
            _log.Debug("FileSuffix32Bit: " + FileSuffix32Bit);
            _log.Debug("FileSuffix64Bit: " + FileSuffix64Bit);
            _log.Debug("-----------------");
        }

        private void RunSanityChecks()
        {
            if (!_fileSystem.FileExists(Xll32FilePath))
            {
                throw new InvalidOperationException("File does not exist (Xll32FilePath): " + Xll32FilePath);
            }

            if (!_fileSystem.FileExists(Xll64FilePath))
            {
                throw new InvalidOperationException("File does not exist (Xll64FilePath): " + Xll64FilePath);
            }

            if (string.Equals(FileSuffix32Bit, FileSuffix64Bit, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("32-bit add-in suffix and 64-bit add-in suffix cannot be identical");
            }
        }

        private ITaskItem[] GetConfigFilesInProject()
        {
            var configFilesInProject = FilesInProject
                .Where(file => string.Equals(Path.GetExtension(file.ItemSpec), ".config", StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.ItemSpec)
                .ToArray();

            return configFilesInProject;
        }

        private void TryBuildAddInFor32Bit(BuildItemSpec[] buildItemsForDnaFiles)
        {
            foreach (var item in buildItemsForDnaFiles)
            {
                if (Create32BitAddIn && ShouldCopy32BitDnaOutput(item, buildItemsForDnaFiles))
                {
                    // Copy .dna file to build output folder for 32-bit
                    if (_fileSystem.FileExists(item.InputDnaFileName))
                        CopyFileToBuildOutput(item.InputDnaFileName, item.OutputDnaFileNameAs32Bit, overwrite: true);
                    else
                        WriteFileToBuildOutput(GetDefaultDnaText(), item.OutputDnaFileNameAs32Bit);

                    // Copy .xll file to build output folder for 32-bit
                    CopyFileToBuildOutput(Xll32FilePath, item.OutputXllFileNameAs32Bit, overwrite: true);

                    // Copy .config file to build output folder for 32-bit (if exist)
                    TryCopyConfigFileToOutput(item.InputConfigFileNameAs32Bit, item.InputConfigFileNameFallbackAs32Bit, item.OutputConfigFileNameAs32Bit);

                    AddDnaToListOfFilesToPack(item.OutputDnaFileNameAs32Bit, item.OutputXllFileNameAs32Bit, item.OutputConfigFileNameAs32Bit);
                }
            }
        }

        private void TryBuildAddInFor64Bit(BuildItemSpec[] buildItemsForDnaFiles)
        {
            foreach (var item in buildItemsForDnaFiles)
            {
                if (Create64BitAddIn && ShouldCopy64BitDnaOutput(item, buildItemsForDnaFiles))
                {
                    // Copy .dna file to build output folder for 64-bit
                    if (_fileSystem.FileExists(item.InputDnaFileName))
                        CopyFileToBuildOutput(item.InputDnaFileName, item.OutputDnaFileNameAs64Bit, overwrite: true);
                    else
                        WriteFileToBuildOutput(GetDefaultDnaText(), item.OutputDnaFileNameAs64Bit);

                    // Copy .xll file to build output folder for 64-bit
                    CopyFileToBuildOutput(Xll64FilePath, item.OutputXllFileNameAs64Bit, overwrite: true);

                    // Copy .config file to build output folder for 64-bit (if exist)
                    TryCopyConfigFileToOutput(item.InputConfigFileNameAs64Bit, item.InputConfigFileNameFallbackAs64Bit, item.OutputConfigFileNameAs64Bit);

                    AddDnaToListOfFilesToPack(item.OutputDnaFileNameAs64Bit, item.OutputXllFileNameAs64Bit, item.OutputConfigFileNameAs64Bit);
                }
            }
        }

        private static bool ShouldCopy32BitDnaOutput(BuildItemSpec item, IEnumerable<BuildItemSpec> buildItems)
        {
            if (item.InputDnaFileName.Equals(item.InputDnaFileNameAs32Bit))
            {
                return true;
            }

            var specificFileExists = buildItems
                .Any(bi => item.InputDnaFileNameAs32Bit.Equals(bi.InputDnaFileName, StringComparison.OrdinalIgnoreCase));

            return !specificFileExists;
        }

        private static bool ShouldCopy64BitDnaOutput(BuildItemSpec item, IEnumerable<BuildItemSpec> buildItems)
        {
            if (item.InputDnaFileName.Equals(item.InputDnaFileNameAs64Bit))
            {
                return true;
            }

            var specificFileExists = buildItems
                .Any(bi => item.InputDnaFileNameAs64Bit.Equals(bi.InputDnaFileName, StringComparison.OrdinalIgnoreCase));

            return !specificFileExists;
        }

        private void TryCopyConfigFileToOutput(string inputConfigFile, string inputFallbackConfigFile, string outputConfigFile)
        {
            var configFile = TryFindAppConfigFileName(inputConfigFile, inputFallbackConfigFile);
            if (!string.IsNullOrWhiteSpace(configFile))
            {
                CopyFileToBuildOutput(configFile, outputConfigFile, overwrite: true);
            }
        }

        private string TryFindAppConfigFileName(string preferredConfigFileName, string fallbackConfigFileName)
        {
            if (_configFilesInProject.Any(c => c.ItemSpec.Equals(preferredConfigFileName, StringComparison.OrdinalIgnoreCase)))
            {
                return preferredConfigFileName;
            }

            if (_configFilesInProject.Any(c => c.ItemSpec.Equals(fallbackConfigFileName, StringComparison.OrdinalIgnoreCase)))
            {
                return fallbackConfigFileName;

            }

            var appConfigFile = _configFilesInProject.FirstOrDefault(c => c.ItemSpec.Equals("App.config", StringComparison.OrdinalIgnoreCase));
            if (appConfigFile != null)
            {
                return appConfigFile.ItemSpec;
            }

            var linkedAppConfigFile = _configFilesInProject.FirstOrDefault(c => c.GetMetadata("Link").Equals("App.config", StringComparison.OrdinalIgnoreCase));
            if (linkedAppConfigFile != null)
            {
                return linkedAppConfigFile.ItemSpec;
            }

            return null;
        }

        private void CopyFileToBuildOutput(string sourceFile, string destinationFile, bool overwrite)
        {
            _log.Information(_fileSystem.GetRelativePath(sourceFile) + " -> " + _fileSystem.GetRelativePath(destinationFile));

            var destinationFolder = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationFolder) && !_fileSystem.DirectoryExists(destinationFolder))
            {
                _fileSystem.CreateDirectory(destinationFolder);
            }

            _fileSystem.CopyFile(sourceFile, destinationFile, overwrite);
        }

        private void WriteFileToBuildOutput(string sourceFileText, string destinationFile)
        {
            _log.Information(" -> " + _fileSystem.GetRelativePath(destinationFile));

            var destinationFolder = Path.GetDirectoryName(destinationFile);
            if (!string.IsNullOrWhiteSpace(destinationFolder) && !_fileSystem.DirectoryExists(destinationFolder))
            {
                _fileSystem.CreateDirectory(destinationFolder);
            }

            _fileSystem.WriteFile(sourceFileText, destinationFile);
        }

        private void AddDnaToListOfFilesToPack(string outputDnaFileName, string outputXllFileName, string outputXllConfigFileName)
        {
            if (!PackIsEnabled)
            {
                return;
            }

            var outputPackedXllFileName = !string.IsNullOrWhiteSpace(PackedFileSuffix)
                ? Path.Combine(Path.GetDirectoryName(outputXllFileName) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(outputXllFileName) + PackedFileSuffix + ".xll")
                : outputXllFileName;

            var metadata = new Hashtable
            {
                {"OutputDnaFileName", outputDnaFileName},
                {"OutputPackedXllFileName", outputPackedXllFileName},
                {"OutputXllConfigFileName", outputXllConfigFileName },
            };

            _dnaFilesToPack.Add(new TaskItem(outputDnaFileName, metadata));
        }

        private string GetDefaultDnaText()
        {
            string result = File.ReadAllText(TemplateDnaPath);
            if (!string.IsNullOrEmpty(AddInName))
                result = result.Replace("%ProjectName% Add-In", AddInName);
            else
                result = result.Replace("%ProjectName%", ProjectName);

            if (!string.IsNullOrEmpty(AddInInclude))
            {
                string includes = "";
                foreach (string i in AddInInclude.Split(';'))
                {
                    includes += $"  <Reference Path=\"{i}\" Pack=\"true\" />" + Environment.NewLine;
                }
                result = result.Replace("</DnaLibrary>", includes + "</DnaLibrary>");
            }

            return result.Replace("%OutputFileName%", !string.IsNullOrEmpty(AddInExternalLibraryPath) ? AddInExternalLibraryPath : TargetFileName);
        }

        /// <summary>
        /// The name of the project being compiled
        /// </summary>
        [Required]
        public string ProjectName { get; set; }

        /// <summary>
        /// The list of files in the project marked as Content or None
        /// </summary>
        [Required]
        public ITaskItem[] FilesInProject { get; set; }

        /// <summary>
        /// The directory in which the built files were written to
        /// </summary>
        [Required]
        public string OutDirectory { get; set; }

        /// <summary>
        /// The 32-bit .xll file path; set to <code>$(MSBuildThisFileDirectory)\ExcelDna.xll</code> by default
        /// </summary>
        [Required]
        public string Xll32FilePath { get; set; }

        /// <summary>
        /// The 64-bit .xll file path; set to <code>$(MSBuildThisFileDirectory)\ExcelDna64.xll</code> by default
        /// </summary>
        [Required]
        public string Xll64FilePath { get; set; }

        /// <summary>
        /// The file name of the primary output file for the build
        /// </summary>
        [Required]
        public string TargetFileName { get; set; }

        /// <summary>
        /// The path to ExcelDna-Template.dna
        /// </summary>
        [Required]
        public string TemplateDnaPath { get; set; }

        /// <summary>
        /// Enable/disable building 32-bit .dna files
        /// </summary>
        public bool Create32BitAddIn { get; set; }

        /// <summary>
        /// Enable/disable building 64-bit .dna files
        /// </summary>
        public bool Create64BitAddIn { get; set; }

        /// <summary>
        /// The name suffix for 32-bit .dna files
        /// </summary>
        public string FileSuffix32Bit { get; set; }

        /// <summary>
        /// The name suffix for 64-bit .dna files
        /// </summary>
        public string FileSuffix64Bit { get; set; }

        /// <summary>
        /// Enable/disable running ExcelDnaPack for .dna files
        /// </summary>
        public bool PackIsEnabled { get; set; }

        /// <summary>
        /// Enable/disable running ExcelDnaPack for .dna files
        /// </summary>
        public string PackedFileSuffix { get; set; }

        /// <summary>
        /// Custom add-in name
        /// </summary>
        public string AddInName { get; set; }

        /// <summary>
        /// Custom add-in file name
        /// </summary>
        public string AddInFileName { get; set; }

        /// <summary>
        /// Semicolon separated list of references written to the .dna file
        /// </summary>
        public string AddInInclude { get; set; }

        /// <summary>
        /// Custom path for ExternalLibrary
        /// </summary>
        public string AddInExternalLibraryPath { get; set; }

        /// <summary>
        /// The list of .dna files copied to the output
        /// </summary>
        [Output]
        public ITaskItem[] DnaFilesToPack { get; set; }
    }
}
