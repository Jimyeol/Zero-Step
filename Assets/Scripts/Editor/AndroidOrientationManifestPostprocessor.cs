#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor.Android;
using UnityEngine;

public sealed class AndroidOrientationManifestPostprocessor : IPostGenerateGradleAndroidProject
{
    private const string ZeroStepUnityActivity = "com.crewoong.zerostep.ZeroStepUnityActivity";
    private const string UnityPlayerActivity = "com.unity3d.player.UnityPlayerActivity";
    private const string UnityPlayerGameActivity = "com.unity3d.player.UnityPlayerGameActivity";
    private const string PortraitOrientation = "portrait";
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

    public int callbackOrder => 10000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        EnsurePortraitActivitySource(path);

        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning($"[AndroidOrientationManifestPostprocessor] Android manifest not found: {manifestPath}");
            return;
        }

        if (ForcePortraitOrientation(manifestPath))
            Debug.Log($"[AndroidOrientationManifestPostprocessor] Forced Unity activity orientation to portrait: {manifestPath}");
    }

    private static bool ForcePortraitOrientation(string manifestPath)
    {
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XDocument document = XDocument.Load(manifestPath, LoadOptions.PreserveWhitespace);
        XElement application = document.Root?.Element("application");
        if (application == null)
            return false;

        XElement activity = application
            .Elements("activity")
            .FirstOrDefault(element =>
            {
                string activityName = element.Attribute(android + "name")?.Value;
                return activityName == ZeroStepUnityActivity ||
                       activityName == UnityPlayerActivity ||
                       activityName == UnityPlayerGameActivity;
            });

        if (activity == null)
            return false;

        activity.SetAttributeValue(android + "name", ZeroStepUnityActivity);
        XAttribute orientation = activity.Attribute(android + "screenOrientation");
        if (orientation != null && orientation.Value == PortraitOrientation)
        {
            document.Save(manifestPath);
            return false;
        }

        activity.SetAttributeValue(android + "screenOrientation", PortraitOrientation);
        document.Save(manifestPath);
        return true;
    }

    private static void EnsurePortraitActivitySource(string projectPath)
    {
        string sourcePath = Path.Combine(projectPath, "src", "main", "java", "com", "crewoong", "zerostep", "ZeroStepUnityActivity.java");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        File.WriteAllText(sourcePath, GetPortraitActivitySource(), Utf8NoBom);
    }

    private static string GetPortraitActivitySource()
    {
        return @"package com.crewoong.zerostep;

import android.content.pm.ActivityInfo;
import android.os.Bundle;

public class ZeroStepUnityActivity extends com.unity3d.player.UnityPlayerGameActivity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
        super.onCreate(savedInstanceState);
        setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
    }

    @Override
    public void setRequestedOrientation(int requestedOrientation) {
        super.setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_PORTRAIT);
    }
}
";
    }
}
#endif
