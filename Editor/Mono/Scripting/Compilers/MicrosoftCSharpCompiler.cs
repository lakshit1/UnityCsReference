// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor.Modules;
using UnityEditor.Utils;
using UnityEngine;

namespace UnityEditor.Scripting.Compilers
{
    internal class MicrosoftCSharpCompiler : ScriptCompilerBase
    {
        public MicrosoftCSharpCompiler(MonoIsland island, bool runUpdater) : base(island, runUpdater)
        {
        }

        private BuildTarget BuildTarget { get { return _island._target; } }

        private string[] GetClassLibraries()
        {
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(BuildTarget);
            if (PlayerSettings.GetScriptingBackend(buildTargetGroup) != ScriptingImplementation.WinRTDotNET)
            {
                return new string[] {};
            }

            if (BuildTarget != BuildTarget.WSAPlayer)
                throw new InvalidOperationException(string.Format("MicrosoftCSharpCompiler cannot build for .NET Scripting backend for BuildTarget.{0}.", BuildTarget));

            var resolver = new NuGetPackageResolver { ProjectLockFile = @"UWP\project.lock.json" };
            return resolver.Resolve();
        }

        private void FillCompilerOptions(List<string> arguments, out string argsPrefix)
        {
            // This will ensure that csc.exe won't include csc.rsp
            // csc.rsp references .NET 4.5 assemblies which cause conflicts for us
            argsPrefix = "/noconfig ";
            arguments.Add("/nostdlib+");

            // Case 755238: Always use english for outputing errors, the same way as Mono compilers do
            arguments.Add("/preferreduilang:en-US");
            arguments.Add("/langversion:latest");

            var platformSupportModule = ModuleManager.FindPlatformSupportModule(ModuleManager.GetTargetStringFromBuildTarget(BuildTarget));
            if (platformSupportModule != null)
            {
                var compilationExtension = platformSupportModule.CreateCompilationExtension();

                arguments.AddRange(GetClassLibraries().Select(r => "/reference:\"" + r + "\""));
                arguments.AddRange(compilationExtension.GetAdditionalAssemblyReferences().Select(r => "/reference:\"" + r + "\""));
                arguments.AddRange(compilationExtension.GetWindowsMetadataReferences().Select(r => "/reference:\"" + r + "\""));
                arguments.AddRange(compilationExtension.GetAdditionalDefines().Select(d => "/define:" + d));
                arguments.AddRange(compilationExtension.GetAdditionalSourceFiles());
            }
        }

        private static void ThrowCompilerNotFoundException(string path)
        {
            throw new Exception(string.Format("'{0}' not found. Is your Unity installation corrupted?", path));
        }

        private Program StartCompilerImpl(List<string> arguments, string argsPrefix)
        {
            foreach (string dll in _island._references)
                arguments.Add("/reference:" + PrepareFileName(dll));

            foreach (string define in _island._defines.Distinct())
                arguments.Add("/define:" + define);

            foreach (string source in _island._files)
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                    arguments.Add(PrepareFileName(source).Replace('/', '\\'));
                else
                    arguments.Add(PrepareFileName(source).Replace('\\', '/'));
            }

            var useNetCore = true;
            var csc = Paths.Combine(EditorApplication.applicationContentsPath, "Tools", "Roslyn", "csc.exe");
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                csc = csc.Replace('/', '\\');
            }
            else
            {
                if (UseNetCoreCompiler())
                {
                    csc = Paths.Combine(EditorApplication.applicationContentsPath, "Tools", "Roslyn", "csc").Replace('\\', '/');
                }
                else
                {
                    useNetCore = false;
                    csc = Paths.Combine(EditorApplication.applicationContentsPath, "Tools", "RoslynNet46", "csc.exe").Replace('\\', '/');
                }
            }


            if (!File.Exists(csc))
                ThrowCompilerNotFoundException(csc);

            AddCustomResponseFileIfPresent(arguments, "csc.rsp");

            var responseFile = CommandLineFormatter.GenerateResponseFile(arguments);

            RunAPIUpdaterIfRequired(responseFile);

            if (useNetCore)
            {
                var psi = new ProcessStartInfo() { Arguments = argsPrefix + " /shared " + "@" + responseFile, FileName = csc, CreateNoWindow = true };
                var program = new Program(psi);
                program.Start();
                return program;
            }
            else
            {
                var program = new ManagedProgram(
                        MonoInstallationFinder.GetMonoBleedingEdgeInstallation(),
                        "not needed",
                        csc,
                        argsPrefix + "@" + responseFile,
                        false,
                        null);
                program.Start();
                return program;
            }
        }

        static bool UseNetCoreCompiler()
        {
            bool shouldUse = false;
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                if (SystemInfo.operatingSystem.StartsWith("Mac OS X 10."))
                {
                    var versionText = SystemInfo.operatingSystem.Substring(9);
                    var version = new Version(versionText);

                    if (version >= new Version(10, 12))
                        shouldUse = true;
                }
                else
                {
                    shouldUse = true;
                }
            }
            return shouldUse;
        }

        protected override Program StartCompiler()
        {
            var outputPath = PrepareFileName(_island._output);

            // Always build with "/debug:pdbonly", "/optimize+", because even if the assembly is optimized
            // it seems you can still succesfully debug C# scripts in Visual Studio
            var arguments = new List<string>
            {
                "/target:library",
                "/nowarn:0169",
                "/out:" + outputPath
            };

            if (_island._allowUnsafeCode)
                arguments.Add("/unsafe");

            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(BuildTarget);
            if (!_island._development_player)
            {
                if (PlayerSettings.GetScriptingBackend(buildTargetGroup) == ScriptingImplementation.WinRTDotNET)
                    arguments.Add("/debug:pdbonly");
                else
                    arguments.Add("/debug:portable");
                arguments.Add("/optimize+");
            }
            else
            {
                if (PlayerSettings.GetScriptingBackend(buildTargetGroup) == ScriptingImplementation.WinRTDotNET)
                    arguments.Add("/debug:full");
                else
                    arguments.Add("/debug:portable");
                arguments.Add("/optimize-");
            }

            string argsPrefix;
            FillCompilerOptions(arguments, out argsPrefix);
            return StartCompilerImpl(arguments, argsPrefix);
        }

        protected override string[] GetStreamContainingCompilerMessages()
        {
            return GetStandardOutput();
        }

        protected override CompilerOutputParserBase CreateOutputParser()
        {
            return new MicrosoftCSharpCompilerOutputParser();
        }
    }
}
