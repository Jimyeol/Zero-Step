using UnityEditor;

/// <summary>
/// 플레이 모드 종료 시 선택을 해제하여, Inspector/PreviewWindow가
/// 파괴된 오브젝트를 참조해 SerializedObject Disposed 예외가 나는 것을 완화.
/// (Unity 에디터 내부 이슈에 대한 우회 처리)
/// </summary>
[InitializeOnLoad]
public static class PreventInspectorDisposedError
{
    static PreventInspectorDisposedError()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // 플레이 종료 직전 선택 해제 → Inspector가 런타임 오브젝트 참조를 놓음
            Selection.activeGameObject = null;
            Selection.activeObject = null;
        }
    }
}
