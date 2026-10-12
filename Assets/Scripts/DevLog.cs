using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// 개발용 로그. 에디터·개발 빌드에서만 호출이 남고, 릴리스 빌드에서는 호출과 인자 문자열 생성까지 컴파일러가 제거한다.
/// (이동마다 문자열 보간 로그가 쌓여 생기던 릴리스 빌드 GC·성능 부담 방지)
/// </summary>
public static class DevLog
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message)
    {
        Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message, UnityEngine.Object context)
    {
        Debug.Log(message, context);
    }
}
