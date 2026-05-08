using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.Events;

/// <summary>
/// [Tools > Puzzle > Create Next Stage UI] 실행 시 현재 씬 Hierarchy에 Canvas → Panel → NextStageButton 추가.
/// 버튼 클릭 시 GameManager.LoadNextStageImmediateFromUI() 호출되도록 영구 리스너 연결.
/// </summary>
public static class CreateNextStageUI
{
    [MenuItem("Tools/Puzzle/Create Next Stage UI")]
    public static void Create()
    {
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogWarning("[CreateNextStageUI] 씬에 GameManager가 없습니다. 먼저 GameManager 오브젝트를 넣은 뒤 다시 실행하세요.");
        }

        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        RectTransform panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, 0f);
        panelRect.sizeDelta = new Vector2(0f, 80f);
        Image panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);
        panelImg.raycastTarget = true;

        GameObject buttonGo = new GameObject("NextStageButton");
        buttonGo.transform.SetParent(panelGo.transform, false);
        RectTransform btnRect = buttonGo.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.pivot = new Vector2(0.5f, 0.5f);
        btnRect.anchoredPosition = Vector2.zero;
        btnRect.sizeDelta = new Vector2(220f, 56f);
        Image btnImg = buttonGo.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.6f, 1f, 1f);
        btnImg.raycastTarget = true;
        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = btnImg;
        ColorBlock cb = button.colors;
        cb.highlightedColor = new Color(0.3f, 0.7f, 1f, 1f);
        cb.pressedColor = new Color(0.15f, 0.5f, 0.9f, 1f);
        button.colors = cb;

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(buttonGo.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        Text text = textGo.AddComponent<Text>();
        text.text = "다음 스테이지";
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) text.font = font;
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;

        if (gm != null)
        {
            UnityEventTools.AddVoidPersistentListener(button.onClick, gm.LoadNextStageImmediateFromUI);
        }
        else
        {
            Debug.Log("[CreateNextStageUI] 버튼 생성 완료. Inspector에서 Button > On Click () 에 GameManager.LoadNextStageImmediateFromUI 를 수동으로 연결하세요.");
        }

        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Next Stage UI");
        Selection.activeGameObject = canvasGo;
        Debug.Log("[CreateNextStageUI] Hierarchy에 Canvas > Panel > NextStageButton 생성 완료.");
    }
}
