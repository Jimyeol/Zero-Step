using UnityEngine;

/// <summary>
/// 스테이지 번호로 JSON 파일을 불러와 StageData로 파싱.
/// Resources/Stages/stage_{번호}.json 사용.
/// </summary>
public static class StageManager
{
    private const string StagesPath = "Stages/stage_";

    /// <summary>
    /// 스테이지 번호에 해당하는 JSON을 로드해 StageData 반환. 없으면 null.
    /// </summary>
    public static StageData LoadStage(int stageNumber)
    {
        string path = StagesPath + stageNumber;
        TextAsset asset = Resources.Load<TextAsset>(path);
        if (asset == null)
        {
            Debug.LogWarning($"[StageManager] 스테이지 리소스 없음: {path}");
            return null;
        }

        StageData data = JsonUtility.FromJson<StageData>(asset.text);
        if (data == null || data.cells == null)
        {
            Debug.LogWarning($"[StageManager] 스테이지 파싱 실패: {path}");
            return null;
        }

        return data;
    }
}
