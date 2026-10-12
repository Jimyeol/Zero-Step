using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// 화면 확인용 캡처. 그래픽이 켜진 배치 모드에서 실행해야 한다(-nographics 사용 불가).
///   ZEROSTEP_CAPTURE_DIR : PNG 저장 폴더
///   ZEROSTEP_CAPTURE_STAGES : 보드 캡처할 스테이지 목록(쉼표 구분, 예 "1,52,203")
/// </summary>
public class VisualCaptureTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const int CaptureWidth = 1080;
    private const int CaptureHeight = 2340;

    [UnityTest, Timeout(600000)]
    public IEnumerator CaptureIntroFrames()
    {
        string outDir = Environment.GetEnvironmentVariable("ZEROSTEP_CAPTURE_DIR");
        if (string.IsNullOrEmpty(outDir))
            Assert.Ignore("ZEROSTEP_CAPTURE_DIR not set");

        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
        }

        outDir = Environment.GetEnvironmentVariable("ZEROSTEP_CAPTURE_DIR");
        Directory.CreateDirectory(outDir);
        var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
        PanelSettings panelSettings = CreateCapturePanelSettings(rt);
        var go = new GameObject("IntroCapture");
        var document = go.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        yield return null;

        VisualElement root = document.rootVisualElement;
        StyleSheet uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Resources/GameMainUI.uss");
        if (uss != null)
            root.styleSheets.Add(uss);
        root.style.backgroundColor = Color.black;
        var intro = new NeonIntroElement();
        root.Add(intro);

        float[] times = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f, 1.0f, 1.08f, 1.16f, 1.25f, 1.35f, 1.5f, 1.7f, 1.9f, 2.05f, 2.2f, 2.5f };
        foreach (float t in times)
        {
            intro.SetTime(t);
            for (int i = 0; i < 3; i++)
                yield return null;
            SaveRenderTexture(rt, Path.Combine(outDir, $"intro_{Mathf.RoundToInt(t * 100):000}.png"));
        }

        UnityEngine.Object.Destroy(go);
        yield return new ExitPlayMode();
    }

    [UnityTest, Timeout(900000)]
    public IEnumerator CaptureBoards()
    {
        string outDir = Environment.GetEnvironmentVariable("ZEROSTEP_CAPTURE_DIR");
        string stageList = Environment.GetEnvironmentVariable("ZEROSTEP_CAPTURE_STAGES");
        if (string.IsNullOrEmpty(outDir) || string.IsNullOrEmpty(stageList))
            Assert.Ignore("ZEROSTEP_CAPTURE_DIR / ZEROSTEP_CAPTURE_STAGES not set");

        if (!EditorApplication.isPlaying)
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
        }

        outDir = Environment.GetEnvironmentVariable("ZEROSTEP_CAPTURE_DIR");
        stageList = Environment.GetEnvironmentVariable("ZEROSTEP_CAPTURE_STAGES");
        Directory.CreateDirectory(outDir);

        GameManager gm = null;
        float waitStart = Time.realtimeSinceStartup;
        while (gm == null && Time.realtimeSinceStartup - waitStart < 30f)
        {
            gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            yield return null;
        }
        Assert.IsNotNull(gm);
        var ui = UnityEngine.Object.FindFirstObjectByType<GameMainUIController>();
        var uiDocument = ui != null ? ui.GetComponent<UIDocument>() : null;
        Camera cam = Camera.main;

        var worldRt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
        var uiRt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
        if (cam != null)
            cam.targetTexture = worldRt;
        if (uiDocument != null && uiDocument.panelSettings != null)
        {
            PanelSettings clone = UnityEngine.Object.Instantiate(uiDocument.panelSettings);
            clone.targetTexture = uiRt;
            clone.clearColor = true;
            clone.colorClearValue = new Color(0f, 0f, 0f, 0f);
            uiDocument.panelSettings = clone;
        }

        // 스플래시가 끝날 때까지 대기
        float splashWait = Time.realtimeSinceStartup;
        while (ui != null && ui.IsSplashActive && Time.realtimeSinceStartup - splashWait < 15f)
            yield return null;

        foreach (string token in stageList.Split(','))
        {
            if (!int.TryParse(token.Trim(), out int stage))
                continue;
            while (gm.AutomationGameOverPlaying)
                yield return null;
            gm.LoadStageImmediateForDebug(stage);
            yield return WaitRealtime(1.2f);
            if (ui != null && ui.IsTutorialPopupOpen)
            {
                yield return WaitRealtime(1.0f);
                CaptureBoth(cam, worldRt, uiRt, outDir, $"stage{stage}_tutorial");
            }
            CloseTutorialIfOpen(ui);
            yield return WaitRealtime(0.6f);
            CaptureBoth(cam, worldRt, uiRt, outDir, $"stage{stage}_start");

            // 힌트 미리보기(경로 화살표)
            if (gm.AutomationHintAvailableNow())
            {
                gm.ShowHintPreview();
                yield return WaitRealtime(0.5f);
                CaptureBoth(cam, worldRt, uiRt, outDir, $"stage{stage}_hint");
                yield return WaitRealtime(0.4f);
            }
        }

        if (cam != null)
            cam.targetTexture = null;
        yield return new ExitPlayMode();
    }

    private static void CloseTutorialIfOpen(GameMainUIController ui)
    {
        if (ui == null || !ui.IsTutorialPopupOpen)
            return;
        typeof(GameMainUIController).GetMethod("CloseTutorialPopup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(ui, null);
    }

    private static void CaptureBoth(Camera cam, RenderTexture worldRt, RenderTexture uiRt, string outDir, string name)
    {
        if (cam != null)
            cam.Render();
        SaveRenderTexture(worldRt, Path.Combine(outDir, name + "_world.png"));
        SaveRenderTexture(uiRt, Path.Combine(outDir, name + "_ui.png"));
    }

    private static PanelSettings CreateCapturePanelSettings(RenderTexture rt)
    {
        PanelSettings source = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Resources/SettingUI.asset");
        PanelSettings settings = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<PanelSettings>();
        settings.targetTexture = rt;
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1080, 1920);
        settings.match = 0f;
        settings.clearColor = true;
        settings.colorClearValue = Color.black;
        return settings;
    }

    private static void SaveRenderTexture(RenderTexture rt, string path)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.Destroy(tex);
    }

    private static IEnumerator WaitRealtime(float seconds)
    {
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < seconds)
            yield return null;
    }
}
