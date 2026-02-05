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
/// 셀 정보: 좌표(x,y), count(0이면 빈 공간), type(Normal/CrossBlast/ShortCircuit/FixedKnot 등). CrossBlast/FixedKnot 시 properties 사용.
/// </summary>
[Serializable]
public class CellData
{
    public int x;
    public int y;
    public int count;
    /// <summary>타일 종류. 없거나 빈 문자열이면 Normal.</summary>
    public string type = "Normal";
    /// <summary>CrossBlast/FixedKnot 등 타입별 옵션. JSON에 없으면 null → 기본값 사용.</summary>
    public CellProperties properties;
    /// <summary>ShortCircuit 타일용. "Up" / "Down" / "Left" / "Right" — 화살표 방향(이동 가능한 셀).</summary>
    public string direction;
    /// <summary>FixedKnot 타일용. 반드시 이 스텝 수에만 진입 가능 (예: 5면 5번째 스텝에 밟아야 함).</summary>
    public int targetOrder;
    /// <summary>TwinLink 타일용. 같은 linkID를 가진 타일끼리 count 동기화·전기 테두리 연동.</summary>
    public int linkID;
    /// <summary>TwinLink 타일용. 테두리 전기·숫자 발광색 (예: "#00FBFF"). 없으면 linkID로 코드에서 지정.</summary>
    public string color;
}

/// <summary>
/// CrossBlast 타일용 옵션: 맥동 속도/범위, 레이저 색상.
/// FixedKnot 타일용: isAbsolute — 순서가 틀리면 진입 불가(게임오버).
/// </summary>
[Serializable]
public class CellProperties
{
    public float pulseSpeed = 2f;
    public float pulseRange = 0.1f;
    public string beamColor = "#00FFFF";
    /// <summary>FixedKnot 전용. true면 순서가 틀리면 진입 불가 후 게임오버(암전·리셋).</summary>
    public bool isAbsolute;
}
