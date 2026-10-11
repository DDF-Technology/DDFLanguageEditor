using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DDFLanguageEditor.Core;
using DDFLanguageEditor.Runtime;

namespace DDFLanguageEditor.Compiler
{
    public sealed class DdfDotNetExecutableReferenceDistribution
    {
        public DdfDotNetExecutableReferenceDistribution(
            string assemblyIdentity, string licensePath,
            string noticePath = null)
        {
            AssemblyIdentity = assemblyIdentity ?? throw new
                ArgumentNullException(nameof(assemblyIdentity));
            LicensePath = licensePath ?? throw new ArgumentNullException(
                nameof(licensePath));
            NoticePath = noticePath ?? string.Empty;
        }

        public string AssemblyIdentity { get; }
        public string LicensePath { get; }
        public string NoticePath { get; }
    }

    public sealed class DdfDotNetExecutablePublishOptions
    {
        public DdfDotNetExecutablePublishOptions(string destination,
            string runtimeIdentifier, string executableName,
            int maxInstructions, string productLicensePath,
            string thirdPartyNoticesPath,
            IEnumerable<DdfDotNetExecutableReferenceDistribution>
                externalReferenceDistributions = null,
            string iconPath = null)
        {
            Destination = destination;
            RuntimeIdentifier = runtimeIdentifier;
            ExecutableName = executableName;
            MaxInstructions = maxInstructions;
            ProductLicensePath = productLicensePath;
            ThirdPartyNoticesPath = thirdPartyNoticesPath;
            ExternalReferenceDistributions = (externalReferenceDistributions ??
                    Array.Empty<DdfDotNetExecutableReferenceDistribution>())
                .ToList().AsReadOnly();
            IconPath = iconPath ?? string.Empty;
        }

        public string Destination { get; }
        public string RuntimeIdentifier { get; }
        public string ExecutableName { get; }
        public int MaxInstructions { get; }
        public string ProductLicensePath { get; }
        public string ThirdPartyNoticesPath { get; }
        public IReadOnlyList<DdfDotNetExecutableReferenceDistribution>
            ExternalReferenceDistributions { get; }
        public string IconPath { get; }
    }

    public sealed class DdfDotNetExecutablePublishResult
    {
        internal DdfDotNetExecutablePublishResult(string outputRoot,
            string executablePath, IEnumerable<string> publishedFiles,
            IEnumerable<DdfDotNetContractIssue> issues)
        {
            OutputRoot = outputRoot ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
            PublishedFiles = (publishedFiles ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList().AsReadOnly();
            Issues = (issues ?? Array.Empty<DdfDotNetContractIssue>())
                .ToList().AsReadOnly();
        }

        public string OutputRoot { get; }
        public string ExecutablePath { get; }
        public IReadOnlyList<string> PublishedFiles { get; }
        public IReadOnlyList<DdfDotNetContractIssue> Issues { get; }
        public bool Success => Issues.Count == 0 &&
            !string.IsNullOrEmpty(ExecutablePath) && File.Exists(ExecutablePath);
    }

    /// <summary>
    /// Publishes a generated launcher and verified DDF modules as a self-contained
    /// executable. Roslyn and the Compiler assembly are build-time only and are
    /// rejected if they appear in the final payload.
    /// </summary>
    public static class DdfDotNetExecutablePublisher
    {
        public const string ManifestFormat = "ddf-dotnet-executable-package";
        public const string ManifestVersion = "1.2";
        public const string SupportDirectoryName = "support";
        private const string DefaultIconResourceName =
            "DDFLanguageEditor.Compiler.DefaultAppIcon.ico";
        private static readonly Regex ExecutableNamePattern = new(
            "^[A-Za-z][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant);

        public static DdfDotNetExecutablePublishResult Publish(
            DdfDotNetCompilationPlan plan, DdfDotNetLinkedBundle bundle,
            DdfDotNetArtifactEmissionResult emission,
            DdfDotNetExecutablePublishOptions options)
        {
            var issues = new List<DdfDotNetContractIssue>();
            Validate(plan, bundle, emission, options, issues);
            if (issues.Count != 0)
                return new DdfDotNetExecutablePublishResult(null, null, null,
                    issues);

            string destination = Path.GetFullPath(options.Destination);
            string parent = Path.GetDirectoryName(destination);
            string leaf = Path.GetFileName(destination);
            if (Directory.Exists(destination) || File.Exists(destination))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC600",
                    "La destinazione del package esiste già: " +
                    destination + "."));
                return new DdfDotNetExecutablePublishResult(null, null, null,
                    issues);
            }

            string sourceRoot = Path.Combine(Path.GetTempPath(),
                "ddf-publish-source-" + Guid.NewGuid().ToString("N"));
            string staging = Path.Combine(parent, "." + leaf +
                ".ddf-publish-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(sourceRoot);
                Directory.CreateDirectory(parent);
                Directory.CreateDirectory(staging);
                string referencesRoot = Path.Combine(sourceRoot, "refs");
                Directory.CreateDirectory(referencesRoot);

                var referencePaths = new List<string>();
                foreach (DdfDotNetEmittedArtifact artifact in emission.Artifacts)
                {
                    string path = Path.Combine(referencesRoot,
                        Path.GetFileName(artifact.AssemblyPath));
                    if (referencePaths.Any(existing => string.Equals(
                            Path.GetFileName(existing), Path.GetFileName(path),
                            StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException(
                            "Nome assembly DDF duplicato nel package.");
                    File.WriteAllBytes(path, artifact.AssemblyBytes);
                    referencePaths.Add(path);
                }
                string runtimePath = Path.Combine(referencesRoot,
                    "DDFLanguageEditor.Runtime.dll");
                File.Copy(typeof(DdfRuntimeAbi).Assembly.Location, runtimePath);
                referencePaths.Add(runtimePath);
                foreach (DdfDotNetPackagedReference reference in
                         bundle.PackagedReferences)
                {
                    string path = Path.Combine(referencesRoot,
                        ReferenceFileName(reference.AssemblyIdentity));
                    if (referencePaths.Any(existing => string.Equals(
                            Path.GetFileName(existing), Path.GetFileName(path),
                            StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException(
                            "Nome assembly duplicato nel package: " +
                            Path.GetFileName(path) + ".");
                    File.WriteAllBytes(path, reference.GetAssemblyBytes());
                    referencePaths.Add(path);
                }

                DdfDotNetCompilationWorkItem[] entryCandidates = plan.WorkItems
                    .Where(item => item.Identity == plan.EntryModuleIdentity)
                    .ToArray();
                if (entryCandidates.Length != 1)
                    throw new InvalidDataException(
                        "Il piano deve contenere un solo modulo di ingresso; " +
                        "trovati: " + entryCandidates.Length + ".");
                DdfDotNetCompilationWorkItem entry = entryCandidates[0];
                DdfBoundSymbol[] mainCandidates = entry.BoundUnit.Symbols
                    .Where(symbol =>
                        symbol.Kind == DdfBoundSymbolKind.Function &&
                        symbol.Name == "main" &&
                        symbol.ParameterTypes.Count == 0)
                    .ToArray();
                if (mainCandidates.Length != 1)
                    throw new InvalidDataException(
                        "Il modulo di ingresso deve esporre una sola funzione " +
                        "main senza parametri; trovate: " +
                        mainCandidates.Length + ".");
                DdfBoundSymbol main = mainCandidates[0];
                string source = CreateLauncherSource(entry, main,
                    options.MaxInstructions);
                PublishedIcon icon = PrepareIcon(options, sourceRoot);
                File.WriteAllText(Path.Combine(sourceRoot, "Program.cs"),
                    source, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(sourceRoot, "Published.csproj"),
                    CreateProject(options, referencePaths, icon.Path),
                    new UTF8Encoding(false));

                string supportRoot = Path.Combine(staging,
                    SupportDirectoryName);
                Directory.CreateDirectory(supportRoot);
                RunPublish(sourceRoot, supportRoot,
                    options.RuntimeIdentifier);
                string supportExecutable = Path.Combine(supportRoot,
                    options.ExecutableName + (OperatingSystem.IsWindows()
                        ? ".exe" : string.Empty));
                if (!File.Exists(supportExecutable))
                    throw new InvalidDataException(
                        "dotnet publish non ha prodotto l'eseguibile atteso.");
                EnsureRuntimeOnly(supportRoot);
                EnsurePublishedReferences(supportRoot,
                    bundle.PackagedReferences);
                CopyNotice(options.ProductLicensePath, supportRoot,
                    "LICENSE.txt");
                CopyNotice(options.ThirdPartyNoticesPath, supportRoot,
                    "THIRD-PARTY-NOTICES.txt");
                CopyDotNetNotices(supportRoot, sourceRoot,
                    options.RuntimeIdentifier, options.ExecutableName);
                CopyExternalReferenceNotices(supportRoot, options);
                string executable = Path.Combine(staging,
                    Path.GetFileName(supportExecutable));
                CreateRelocatedAppHost(sourceRoot, executable, options);
                File.Delete(supportExecutable);
                EnsureRootAllowlist(staging, executable);
                WriteManifest(staging, supportRoot, options, plan, bundle,
                    icon);
                Directory.Move(staging, destination);
                string finalExecutable = Path.Combine(destination,
                    Path.GetFileName(executable));
                return new DdfDotNetExecutablePublishResult(destination,
                    finalExecutable, Directory.EnumerateFiles(destination,
                        "*", SearchOption.AllDirectories), null);
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException ||
                                               exception is InvalidDataException ||
                                               exception is ArgumentException ||
                                               exception is InvalidOperationException ||
                                               exception is System.ComponentModel.Win32Exception)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC601",
                    "Pubblicazione self-contained fallita: " +
                    exception.Message));
                return new DdfDotNetExecutablePublishResult(null, null, null,
                    issues);
            }
            finally
            {
                TryDelete(sourceRoot);
                TryDelete(staging);
            }
        }

        private static void Validate(DdfDotNetCompilationPlan plan,
            DdfDotNetLinkedBundle bundle,
            DdfDotNetArtifactEmissionResult emission,
            DdfDotNetExecutablePublishOptions options,
            ICollection<DdfDotNetContractIssue> issues)
        {
            if (plan == null || bundle == null || emission?.Success != true ||
                options == null)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC600",
                    "Piano, bundle, emissione e opzioni package sono richiesti."));
                return;
            }
            if (bundle.EntryModuleIdentity != plan.EntryModuleIdentity ||
                bundle.Artifacts.Count != emission.Artifacts.Count)
                issues.Add(new DdfDotNetContractIssue("DDFC600",
                    "Piano, bundle ed emissione non coincidono."));
            if (bundle.RequiredHostCapabilities.Count != 0)
                issues.Add(new DdfDotNetContractIssue("DDFC602",
                    "Un EXE standalone non può essere pubblicato senza " +
                    "implementazioni delle capability host richieste."));
            if (options.RuntimeIdentifier != "win-x64")
                issues.Add(new DdfDotNetContractIssue("DDFC603",
                    "Il primo profilo self-contained supporta soltanto win-x64."));
            if (!OperatingSystem.IsWindows() ||
                RuntimeInformation.ProcessArchitecture != Architecture.X64)
                issues.Add(new DdfDotNetContractIssue("DDFC603",
                    "La pubblicazione win-x64 richiede un host Windows x64."));
            if (!ExecutableNamePattern.IsMatch(options.ExecutableName ?? "") ||
                IsReservedWindowsName(options.ExecutableName))
                issues.Add(new DdfDotNetContractIssue("DDFC600",
                    "Nome eseguibile non valido."));
            if (options.MaxInstructions <= 0)
                issues.Add(new DdfDotNetContractIssue("DDFC600",
                    "Budget istruzioni non valido."));
            if (string.IsNullOrWhiteSpace(options.Destination) ||
                !Path.IsPathRooted(options.Destination))
                issues.Add(new DdfDotNetContractIssue("DDFC600",
                    "Destinazione package assoluta richiesta."));
            if (!File.Exists(options.ProductLicensePath) ||
                !File.Exists(options.ThirdPartyNoticesPath))
                issues.Add(new DdfDotNetContractIssue("DDFC604",
                    "Licenza o avvisi di terze parti non disponibili."));
            if (options.IconPath.Length != 0)
            {
                if (!File.Exists(options.IconPath) ||
                    !string.Equals(Path.GetExtension(options.IconPath), ".ico",
                        StringComparison.OrdinalIgnoreCase))
                    issues.Add(new DdfDotNetContractIssue("DDFC606",
                        "L'icona personalizzata deve essere un file .ico " +
                        "esistente."));
                else
                {
                    try
                    {
                        ValidateIcon(File.ReadAllBytes(options.IconPath));
                    }
                    catch (Exception exception) when (
                        exception is IOException ||
                        exception is UnauthorizedAccessException ||
                        exception is InvalidDataException)
                    {
                        issues.Add(new DdfDotNetContractIssue("DDFC606",
                            "Icona personalizzata non valida: " +
                            exception.Message));
                    }
                }
            }
            ValidateExternalReferenceDistribution(bundle, options, issues);
        }

        private static void ValidateExternalReferenceDistribution(
            DdfDotNetLinkedBundle bundle,
            DdfDotNetExecutablePublishOptions options,
            ICollection<DdfDotNetContractIssue> issues)
        {
            DdfDotNetExecutableReferenceDistribution[] supplied = options
                .ExternalReferenceDistributions.Where(item => item != null)
                .ToArray();
            if (supplied.Length != options.ExternalReferenceDistributions.Count ||
                supplied.Select(item => item.AssemblyIdentity).Distinct(
                    StringComparer.Ordinal).Count() != supplied.Length)
            {
                issues.Add(new DdfDotNetContractIssue("DDFC605",
                    "Le ricevute di redistribuzione delle reference esterne " +
                    "sono assenti o duplicate."));
                return;
            }
            string[] expected = bundle.PackagedReferences.Select(item =>
                    item.AssemblyIdentity).OrderBy(value => value,
                    StringComparer.Ordinal).ToArray();
            string[] actual = supplied.Select(item => item.AssemblyIdentity)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                issues.Add(new DdfDotNetContractIssue("DDFC605",
                    "Ogni DLL esterna inclusa richiede una ricevuta esplicita " +
                    "di redistribuzione e non sono ammesse ricevute estranee."));
                return;
            }
            foreach (DdfDotNetExecutableReferenceDistribution distribution in
                     supplied)
            {
                try
                {
                    _ = ReferenceFileName(distribution.AssemblyIdentity);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is FileLoadException)
                {
                    issues.Add(new DdfDotNetContractIssue("DDFC605",
                        "Identità redistribuibile non valida: " +
                        exception.Message));
                }
                if (!File.Exists(distribution.LicensePath) ||
                    (distribution.NoticePath.Length != 0 &&
                     !File.Exists(distribution.NoticePath)))
                    issues.Add(new DdfDotNetContractIssue("DDFC605",
                        "Licenza o avviso di redistribuzione non disponibile " +
                        "per " + distribution.AssemblyIdentity + "."));
            }
        }

        private static string CreateLauncherSource(
            DdfDotNetCompilationWorkItem entry, DdfBoundSymbol main,
            int maxInstructions)
        {
            string container = entry.GeneratedNames.NamespaceName + "." +
                entry.GeneratedNames.ContainerTypeName;
            string mainName = entry.GeneratedNames.GetIdentifier(main.Id);
            bool returnsVoid = main.Type.Name == "void" &&
                main.Type.ArrayRank == 0;
            string invoke = returnsVoid
                ? container + "." + mainName +
                  "(runtime); return null;"
                : "return " + container + "." + mainName + "(runtime);";
            return "using System;\n" +
                "using DDFLanguageEditor.Runtime;\n\n" +
                "internal static class Program\n{\n" +
                "    private static int Main()\n    {\n" +
                "        try\n        {\n" +
                "            var context = new DdfRuntimeContext(" +
                maxInstructions + ", output: Console.WriteLine, " +
                "input: Console.ReadLine);\n" +
                "            DdfRuntimeExecutionResult result = " +
                "context.Execute(runtime =>\n            {\n" +
                "                " + container +
                ".DdfGenerated_Helper_ModuleInitialize(runtime);\n" +
                "                " + invoke + "\n" +
                "            });\n" +
                "            Console.WriteLine(\"Valore restituito: \" + " +
                "DdfRuntimeContext.Format(result.Value));\n" +
                "            return 0;\n        }\n" +
                "        catch (DdfRuntimeException exception)\n        {\n" +
                "            Console.Error.WriteLine(exception.Code + \": \" + " +
                "exception.Message);\n            return 3;\n        }\n" +
                "        catch (Exception exception)\n        {\n" +
                "            Console.Error.WriteLine(\"DDFEXE001: \" + " +
                "exception.Message);\n            return 1;\n        }\n" +
                "    }\n}\n";
        }

        private static string CreateProject(
            DdfDotNetExecutablePublishOptions options,
            IEnumerable<string> referencePaths, string iconPath)
        {
            var references = new StringBuilder();
            foreach (string path in referencePaths.OrderBy(value => value,
                         StringComparer.OrdinalIgnoreCase))
            {
                string include = Path.GetFileNameWithoutExtension(path);
                references.Append("    <Reference Include=\"")
                    .Append(Xml(include)).Append("\"><HintPath>")
                    .Append(Xml(path)).Append("</HintPath><Private>true</Private>")
                    .AppendLine("</Reference>");
            }
            return "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
                "  <PropertyGroup>\n" +
                "    <OutputType>Exe</OutputType>\n" +
                "    <TargetFramework>net10.0</TargetFramework>\n" +
                "    <RuntimeIdentifier>" + Xml(options.RuntimeIdentifier) +
                "</RuntimeIdentifier>\n" +
                "    <SelfContained>true</SelfContained>\n" +
                "    <UseAppHost>true</UseAppHost>\n" +
                "    <PublishSingleFile>false</PublishSingleFile>\n" +
                "    <PublishTrimmed>false</PublishTrimmed>\n" +
                "    <DebugType>none</DebugType>\n" +
                "    <DebugSymbols>false</DebugSymbols>\n" +
                "    <Deterministic>true</Deterministic>\n" +
                "    <ApplicationIcon>" + Xml(iconPath) +
                "</ApplicationIcon>\n" +
                "    <AssemblyName>" + Xml(options.ExecutableName) +
                "</AssemblyName>\n" +
                "    <Version>1.0.0</Version>\n" +
                "  </PropertyGroup>\n" +
                "  <ItemGroup>\n" + references +
                "  </ItemGroup>\n" +
                "  <Target Name=\"DdfCreateRelocatedAppHost\" " +
                "DependsOnTargets=\"_GetAppHostPaths;" +
                "_GetAppHostCreationConfiguration\">\n" +
                "    <Error Condition=\"'$(DdfAppHostDestination)' == ''\" " +
                "Text=\"DdfAppHostDestination richiesto.\" />\n" +
                "    <Error Condition=\"'$(DdfAppBinaryRelativePath)' == ''\" " +
                "Text=\"DdfAppBinaryRelativePath richiesto.\" />\n" +
                "    <CreateAppHost AppHostSourcePath=\"$(AppHostSourcePath)\" " +
                "AppHostDestinationPath=\"$(DdfAppHostDestination)\" " +
                "AppBinaryName=\"$(DdfAppBinaryRelativePath)\" " +
                "IntermediateAssembly=\"@(IntermediateAssembly->" +
                "'%(FullPath)')\" " +
                "WindowsGraphicalUserInterface=\"$(_UseWindowsGraphicalUserInterface)\" " +
                "Retries=\"$(CopyRetryCount)\" " +
                "RetryDelayMilliseconds=\"$(CopyRetryDelayMilliseconds)\" " +
                "EnableMacOSCodeSign=\"$(_EnableMacOSCodeSign)\" " +
                "DisableCetCompat=\"$(_DisableCetCompat)\" />\n" +
                "  </Target>\n" +
                "</Project>\n";
        }

        private static PublishedIcon PrepareIcon(
            DdfDotNetExecutablePublishOptions options, string sourceRoot)
        {
            byte[] bytes;
            string source;
            string name;
            if (options.IconPath.Length != 0)
            {
                bytes = File.ReadAllBytes(options.IconPath);
                source = "custom";
                name = Path.GetFileName(options.IconPath);
            }
            else
            {
                using Stream stream = typeof(DdfDotNetExecutablePublisher)
                    .Assembly.GetManifestResourceStream(
                        DefaultIconResourceName) ??
                    throw new InvalidDataException(
                        "Icona DDF predefinita non incorporata.");
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                bytes = memory.ToArray();
                source = "ddf-default";
                name = "DDFLanguageEditor.ico";
            }
            ValidateIcon(bytes);
            string path = Path.Combine(sourceRoot, "app.ico");
            File.WriteAllBytes(path, bytes);
            return new PublishedIcon(path, source, name,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }

        private static void ValidateIcon(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 22 ||
                BitConverter.ToUInt16(bytes, 0) != 0 ||
                BitConverter.ToUInt16(bytes, 2) != 1)
                throw new InvalidDataException(
                    "Intestazione ICO assente o non valida.");
            int count = BitConverter.ToUInt16(bytes, 4);
            if (count <= 0 || bytes.Length < 6 + count * 16)
                throw new InvalidDataException(
                    "Directory immagini ICO non valida.");
            for (int index = 0; index < count; index++)
            {
                int entry = 6 + index * 16;
                uint size = BitConverter.ToUInt32(bytes, entry + 8);
                uint offset = BitConverter.ToUInt32(bytes, entry + 12);
                if (size == 0 || offset > bytes.Length ||
                    size > bytes.Length - offset)
                    throw new InvalidDataException(
                        "Immagine ICO fuori dai limiti del file.");
            }
        }

        private static void CreateRelocatedAppHost(string sourceRoot,
            string executablePath,
            DdfDotNetExecutablePublishOptions options)
        {
            var arguments = new[]
            {
                "msbuild", "Published.csproj",
                "-t:DdfCreateRelocatedAppHost",
                "-p:Configuration=Release",
                "-p:RuntimeIdentifier=" + options.RuntimeIdentifier,
                "-p:SelfContained=true",
                "-p:DdfAppHostDestination=" + executablePath,
                "-p:DdfAppBinaryRelativePath=" + SupportDirectoryName +
                    "/" + options.ExecutableName + ".dll",
                "-p:NuGetAudit=false", "-v:minimal"
            };
            RunDotNet(sourceRoot, arguments,
                "creazione dell'apphost rilocato");
            if (!File.Exists(executablePath))
                throw new InvalidDataException(
                    "La creazione dell'apphost non ha prodotto l'EXE radice.");
        }

        private static void RunPublish(string sourceRoot, string outputRoot,
            string runtimeIdentifier)
        {
            RunDotNet(sourceRoot, new[]
            {
                "publish", "Published.csproj", "-c", "Release",
                "-r", runtimeIdentifier, "--self-contained", "true",
                "--output", outputRoot,
                "-p:PublishSingleFile=false",
                "-p:PublishTrimmed=false",
                "-p:NuGetAudit=false",
                "-p:RestoreIgnoreFailedSources=true",
                "-p:UseSharedCompilation=false", "-v:minimal"
            }, "dotnet publish");
        }

        private static void RunDotNet(string sourceRoot,
            IEnumerable<string> arguments, string operation)
        {
            var start = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable(
                    "DOTNET_HOST_PATH") ?? "dotnet",
                WorkingDirectory = sourceRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (string argument in arguments)
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ??
                throw new InvalidOperationException(
                    "Impossibile avviare " + operation + ".");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            if (!process.WaitForExit((int)TimeSpan.FromMinutes(5)
                    .TotalMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                throw new InvalidOperationException(
                    "Timeout durante " + operation + ".");
            }
            if (process.ExitCode != 0)
                throw new InvalidOperationException(
                    operation + " è terminata con codice " +
                    process.ExitCode + ": " + Limit(standardError + "\n" +
                        standardOutput, 8000));
        }

        private static void EnsureRootAllowlist(string packageRoot,
            string executablePath)
        {
            string[] rootFiles = Directory.EnumerateFiles(packageRoot,
                    "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFullPath).ToArray();
            if (rootFiles.Length != 1 || !string.Equals(rootFiles[0],
                    Path.GetFullPath(executablePath),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "La radice del package deve contenere soltanto l'EXE " +
                    "principale.");
            string[] rootDirectories = Directory.EnumerateDirectories(
                packageRoot, "*", SearchOption.TopDirectoryOnly).ToArray();
            if (rootDirectories.Length != 1 || !string.Equals(
                    Path.GetFileName(rootDirectories[0]), SupportDirectoryName,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "La radice del package deve contenere soltanto la " +
                    "directory support oltre all'EXE.");
        }

        private static void EnsureRuntimeOnly(string root)
        {
            string[] forbidden = Directory.EnumerateFiles(root, "*",
                    SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(name => name.Contains("CodeAnalysis",
                                   StringComparison.OrdinalIgnoreCase) ||
                               name.Equals("DDFLanguageEditor.Compiler.dll",
                                   StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (forbidden.Length != 0)
                throw new InvalidDataException(
                    "Dipendenze di compilazione presenti nel package: " +
                    string.Join(", ", forbidden) + ".");
        }

        private static void EnsurePublishedReferences(string root,
            IEnumerable<DdfDotNetPackagedReference> references)
        {
            foreach (DdfDotNetPackagedReference reference in references)
            {
                string path = Path.Combine(root,
                    ReferenceFileName(reference.AssemblyIdentity));
                if (!File.Exists(path))
                    throw new InvalidDataException(
                        "DLL esterna non pubblicata: " +
                        reference.AssemblyIdentity + ".");
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.LongLength != reference.Size ||
                    DdfDotNetArtifactContract.Hash(bytes) != reference.Sha256)
                    throw new InvalidDataException(
                        "DLL esterna pubblicata diversa dal bundle verificato: " +
                        reference.AssemblyIdentity + ".");
            }
        }

        private static void CopyExternalReferenceNotices(string root,
            DdfDotNetExecutablePublishOptions options)
        {
            foreach (DdfDotNetExecutableReferenceDistribution distribution in
                     options.ExternalReferenceDistributions.OrderBy(item =>
                         item.AssemblyIdentity, StringComparer.Ordinal))
            {
                string simpleName = Path.GetFileNameWithoutExtension(
                    ReferenceFileName(distribution.AssemblyIdentity));
                string destination = Path.Combine(root,
                    "THIRD-PARTY-LICENSES", simpleName);
                Directory.CreateDirectory(destination);
                CopyNotice(distribution.LicensePath, destination,
                    "LICENSE.txt");
                if (distribution.NoticePath.Length != 0)
                    CopyNotice(distribution.NoticePath, destination,
                        "NOTICE.txt");
            }
        }

        private static void CopyDotNetNotices(string outputRoot,
            string sourceRoot, string runtimeIdentifier,
            string executableName)
        {
            string dependencyPath = Path.Combine(sourceRoot, "obj",
                "project.assets.json");
            if (!File.Exists(dependencyPath))
                throw new InvalidDataException(
                    "Il restore non ha prodotto project.assets.json.");
            using JsonDocument assets = JsonDocument.Parse(
                File.ReadAllText(dependencyPath, Encoding.UTF8));
            string runtimeConfigPath = Path.Combine(outputRoot,
                executableName + ".runtimeconfig.json");
            if (!File.Exists(runtimeConfigPath))
                throw new InvalidDataException(
                    "dotnet publish non ha prodotto il runtimeconfig atteso.");
            using JsonDocument runtimeConfig = JsonDocument.Parse(
                File.ReadAllText(runtimeConfigPath, Encoding.UTF8));
            JsonElement runtimeOptions = runtimeConfig.RootElement
                .GetProperty("runtimeOptions");
            string runtimeVersion = ReadIncludedRuntimeVersion(
                runtimeOptions);
            string packageId = "microsoft.netcore.app.runtime." +
                runtimeIdentifier.ToLowerInvariant();
            string[] packageRoots = assets.RootElement
                .GetProperty("packageFolders").EnumerateObject()
                .Select(item => item.Name).ToArray();
            if (packageRoots.Length == 0)
                throw new InvalidDataException(
                    "Il restore non dichiara alcuna cartella package NuGet.");
            string packageRoot = packageRoots
                .Select(Path.GetFullPath)
                .FirstOrDefault(root => Directory.Exists(Path.Combine(root,
                    packageId, runtimeVersion))) ?? string.Empty;
            if (packageRoot.Length == 0)
                throw new InvalidDataException(
                    "Il runtime pack risolto non è presente nelle cartelle " +
                    "package dichiarate dal restore: " + packageId + "/" +
                    runtimeVersion + ".");
            string packagePath = Path.Combine(packageRoot,
                packageId, runtimeVersion);
            CopyNotice(Path.Combine(packagePath, "LICENSE.TXT"), outputRoot,
                "DOTNET-LICENSE.txt");
            CopyNotice(Path.Combine(packagePath,
                    "THIRD-PARTY-NOTICES.TXT"), outputRoot,
                "DOTNET-THIRD-PARTY-NOTICES.txt");
        }

        private static string ReadIncludedRuntimeVersion(
            JsonElement runtimeOptions)
        {
            if (runtimeOptions.TryGetProperty("includedFrameworks",
                    out JsonElement frameworks))
            {
                string[] versions = frameworks.EnumerateArray()
                    .Where(item => item.TryGetProperty("name", out var name) &&
                        name.GetString() == "Microsoft.NETCore.App" &&
                        item.TryGetProperty("version", out _))
                    .Select(item => item.GetProperty("version").GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
                if (versions.Length == 1)
                    return versions[0];
                throw new InvalidDataException(
                    "Il runtimeconfig deve dichiarare una sola versione " +
                    "inclusa di Microsoft.NETCore.App; trovate: " +
                    versions.Length + ".");
            }
            throw new InvalidDataException(
                "Il runtimeconfig self-contained non dichiara " +
                "includedFrameworks.");
        }

        private static void WriteManifest(string packageRoot,
            string manifestRoot,
            DdfDotNetExecutablePublishOptions options,
            DdfDotNetCompilationPlan plan, DdfDotNetLinkedBundle bundle,
            PublishedIcon icon)
        {
            var files = Directory.EnumerateFiles(packageRoot, "*",
                    SearchOption.AllDirectories)
                .Select(path => new
                {
                    path = Path.GetRelativePath(packageRoot, path)
                        .Replace('\\', '/'),
                    size = new FileInfo(path).Length,
                    sha256 = Convert.ToHexString(SHA256.HashData(
                        File.ReadAllBytes(path))).ToLowerInvariant()
                }).OrderBy(item => item.path, StringComparer.Ordinal).ToArray();
            var externalReferences = bundle.PackagedReferences.Select(
                reference =>
                {
                    string fileName = ReferenceFileName(
                        reference.AssemblyIdentity);
                    DdfDotNetExecutableReferenceDistribution distribution =
                        options.ExternalReferenceDistributions.Single(item =>
                            item.AssemblyIdentity ==
                            reference.AssemblyIdentity);
                    string simpleName = Path.GetFileNameWithoutExtension(
                        fileName);
                    return new
                    {
                        assemblyIdentity = reference.AssemblyIdentity,
                        file = SupportDirectoryName + "/" + fileName,
                        reference.Size,
                        reference.Sha256,
                        license = SupportDirectoryName +
                            "/THIRD-PARTY-LICENSES/" + simpleName +
                            "/LICENSE.txt",
                        notice = distribution.NoticePath.Length == 0 ? null :
                            SupportDirectoryName +
                            "/THIRD-PARTY-LICENSES/" + simpleName +
                            "/NOTICE.txt"
                    };
                }).OrderBy(item => item.assemblyIdentity,
                    StringComparer.Ordinal).ToArray();
            string json = JsonSerializer.Serialize(new
            {
                format = ManifestFormat,
                version = ManifestVersion,
                runtimeIdentifier = options.RuntimeIdentifier,
                selfContained = true,
                singleFile = false,
                executable = options.ExecutableName + ".exe",
                supportDirectory = SupportDirectoryName,
                publicationFingerprint = PublicationFingerprint(options,
                    plan, bundle, icon),
                icon = new
                {
                    source = icon.Source,
                    name = icon.Name,
                    sha256 = icon.Sha256
                },
                planFingerprint = plan.Fingerprint,
                bundleFingerprint = bundle.Fingerprint,
                runtimeAbiVersion = DdfRuntimeAbi.Version,
                externalReferences,
                files
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(manifestRoot,
                    "ddf-publish.json"), json + Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static string PublicationFingerprint(
            DdfDotNetExecutablePublishOptions options,
            DdfDotNetCompilationPlan plan, DdfDotNetLinkedBundle bundle,
            PublishedIcon icon)
        {
            string value = string.Join("\n", ManifestFormat,
                ManifestVersion, options.RuntimeIdentifier,
                options.ExecutableName, options.MaxInstructions.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                plan.Fingerprint, bundle.Fingerprint, icon.Sha256);
            return Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }

        private sealed class PublishedIcon
        {
            public PublishedIcon(string path, string source, string name,
                string sha256)
            {
                Path = path;
                Source = source;
                Name = name;
                Sha256 = sha256;
            }

            public string Path { get; }
            public string Source { get; }
            public string Name { get; }
            public string Sha256 { get; }
        }

        private static void CopyNotice(string source, string destination,
            string name)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException(
                    "Avviso di licenza richiesto non trovato.", source);
            File.Copy(source, Path.Combine(destination, name));
        }

        private static bool IsReservedWindowsName(string name)
        {
            string stem = (name ?? string.Empty).Split('.')[0];
            return new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2",
                    "COM3", "COM4", "COM5", "COM6", "COM7", "COM8",
                    "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5",
                    "LPT6", "LPT7", "LPT8", "LPT9" }
                .Contains(stem, StringComparer.OrdinalIgnoreCase);
        }

        private static string ReferenceFileName(string assemblyIdentity)
        {
            var identity = new AssemblyName(assemblyIdentity);
            string name = identity.Name;
            if (string.IsNullOrWhiteSpace(name) ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                IsReservedWindowsName(name))
                throw new ArgumentException(
                    "Nome assembly esterno non valido.",
                    nameof(assemblyIdentity));
            return name + ".dll";
        }

        private static string Xml(string value) =>
            SecurityElement.Escape(value ?? string.Empty);

        private static string Limit(string value, int length) =>
            string.IsNullOrEmpty(value) || value.Length <= length
                ? value ?? string.Empty
                : value.Substring(0, length);

        private static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
