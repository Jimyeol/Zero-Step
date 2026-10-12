using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 배치 모드 빌드용. 예:
///   Unity -batchmode -quit -projectPath . -executeMethod BuildScripts.BuildAndroidDebug -logFile build.log
/// 결과: Builds/Android/ZeroStep-debug.apk (개발 빌드, 디버그 서명)
/// </summary>
public static class BuildScripts
{
    [MenuItem("ZeroStep/Build/Android Debug APK")]
    public static void BuildAndroidDebug()
    {
        string output = Path.Combine("Builds", "Android", "ZeroStep-debug.apk");
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        EditorUserBuildSettings.buildAppBundle = false;
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[BuildScripts] result={report.summary.result} size={report.summary.totalSize} errors={report.summary.totalErrors} path={output}");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
