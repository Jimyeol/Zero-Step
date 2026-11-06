using System.Collections.Generic;
using UnityEngine;

/// <summary>스테이지마다 하나씩 붙는 도전 목표(세 번째 별의 조건).</summary>
public enum StageChallengeKind
{
    /// <summary>실패(게임오버·리셋)와 되돌리기 없이 클리어.</summary>
    Flawless,
    /// <summary>손가락을 한 번도 떼지 않고 클리어.</summary>
    OneStroke,
    /// <summary>별이 붙은 칸에서 끝내기.</summary>
    EndOnMarked
}

/// <summary>
/// 스테이지 번호와 검증된 정답(solutions.txt)만으로 도전 목표를 정한다. 스테이지 데이터는 건드리지 않는다.
/// 모든 목표는 저장된 정답으로 달성할 수 있다: 정답은 전부 한 획이고, 별 칸은 그 정답의 마지막 칸이다.
/// </summary>
public static class StageChallenge
{
    public static StageChallengeKind KindFor(int stageIndex)
    {
        int slot = ((stageIndex % 3) + 3) % 3;
        if (slot == 2)
            return StageChallengeKind.OneStroke;
        if (slot == 0 && TryGetEndCell(stageIndex, out _))
            return StageChallengeKind.EndOnMarked;
        return StageChallengeKind.Flawless;
    }

    /// <summary>정답의 마지막 칸. 정답이 없으면 false.</summary>
    public static bool TryGetEndCell(int stageIndex, out Vector2Int cell)
    {
        List<Vector2Int> moves = StageManager.LoadSolutionMoves(stageIndex);
        if (moves == null || moves.Count == 0)
        {
            cell = default;
            return false;
        }
        cell = moves[moves.Count - 1];
        return true;
    }

    public static string LocalizationKey(StageChallengeKind kind)
    {
        switch (kind)
        {
            case StageChallengeKind.OneStroke: return "challenge_one_stroke";
            case StageChallengeKind.EndOnMarked: return "challenge_end_marked";
            default: return "challenge_flawless";
        }
    }
}
