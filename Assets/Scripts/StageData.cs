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
/// 셀 정보: 좌표(x,y), count(0이면 빈 공간).
/// </summary>
[Serializable]
public class CellData
{
    public int x;
    public int y;
    public int count;
}
