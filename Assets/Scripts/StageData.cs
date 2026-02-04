using System;
using UnityEngine;

/// <summary>
/// JSON 스테이지 데이터. JsonUtility 직렬화용.
/// </summary>
[Serializable]
public class StageData
{
    public int stageID;
    public int width;
    public int height;
    public StartPointData startPoint;
    public CellData[] cells;
}

/// <summary>
/// 시작 좌표 (JSON: {"x":0, "y":2}).
/// </summary>
[Serializable]
public class StartPointData
{
    public int x;
    public int y;
}

/// <summary>
/// 셀 정보: 좌표(x,y), count(0이면 빈 공간), type(Normal/CrossBlast/ShortCircuit 등). CrossBlast 시 properties 사용.
/// </summary>
[Serializable]
public class CellData
{
    public int x;
    public int y;
    public int count;
    /// <summary>타일 종류. 없거나 빈 문자열이면 Normal.</summary>
    public string type = "Normal";
    /// <summary>CrossBlast 등 타입별 옵션. JSON에 없으면 null → 기본값 사용.</summary>
    public CellProperties properties;
    /// <summary>ShortCircuit 타일용. "Up" / "Down" / "Left" / "Right" — 화살표 방향(이동 가능한 셀).</summary>
    public string direction;
}

/// <summary>
/// CrossBlast 타일용 옵션: 맥동 속도/범위, 레이저 색상.
/// </summary>
[Serializable]
public class CellProperties
{
    public float pulseSpeed = 2f;
    public float pulseRange = 0.1f;
    public string beamColor = "#00FFFF";
}
