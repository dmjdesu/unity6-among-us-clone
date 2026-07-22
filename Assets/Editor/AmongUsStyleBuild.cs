using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AmongUsClone.Editor
{
    public static class AmongUsStyleBuild
    {
        private const string DefaultOutputPath = "Builds/macOS/AmongUsStyle.app";

        [MenuItem("Build/Build macOS Player")]
        public static void BuildMacOS()
        {
            BuildMacOSPlayer(regenerateRoomPrefabs: true);
        }

        [MenuItem("Build/Build macOS Player Without Room Regeneration")]
        public static void BuildMacOSWithoutRoomRegeneration()
        {
            BuildMacOSPlayer(regenerateRoomPrefabs: false);
        }

        private static void BuildMacOSPlayer(bool regenerateRoomPrefabs)
        {
            var outputPath = GetCommandLineArg("-outputPath") ?? DefaultOutputPath;
            PlayerSettings.companyName = "AmongUsStyle";
            PlayerSettings.productName = "AmongUsStyle";
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Standalone,
                "com.koudaseiryuu.amongusstyle");
            if (regenerateRoomPrefabs)
            {
                ProductionRoomPrefabBuilder.BuildThreeRoomPrefabs();
            }

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes found in Build Settings.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"macOS build failed: {report.summary.result} ({report.summary.totalErrors} errors)");
            }

            Debug.Log($"macOS build created at {Path.GetFullPath(outputPath)}");
        }

        private static string GetCommandLineArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
