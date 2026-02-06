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
    /// <summary>스테이지별 모드·옵션 (Spotlight 등). 없으면 null.</summary>
    public StageConfig config;
    public CellData[] cells;
}

/// <summary>
/// 스테이지별 모드·세부 옵션. JSON config 객체.
/// </summary>
[Serializable]
public class StageConfig
{
    /// <summary>"Spotlight" 등. 없으면 일반 플레이.</summary>
    public string mode = "";
    /// <summary>"Normal" 또는 "Hard". Normal=밟은 타일 영구 밝힘, Hard=드래그 중인 위치만 밝음.</summary>
    public string difficulty = "Normal";
    /// <summary>스포트라이트 반경 (월드 단위).</summary>
    public float spotlightRadius = 2.5f;
    /// <summary>어둠 속에서 그리드 선을 살짝 보여줄지.</summary>
    public bool showGridLines = false;
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
    /// <summary>Igniter 타일용. 이 타일을 밟으면 targetID와 일치하는 Hidden 그룹을 활성화.</summary>
    public string targetID;
    /// <summary>Hidden 타일용. Igniter의 targetID와 일치하면 이 그룹이 릴레이 점등.</summary>
    public string groupID;
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
