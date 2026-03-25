using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재는 CrossBlast 링크 비주얼을 사용하지 않는다.
/// </summary>
public class LinkSystem : MonoBehaviour
{
    /// <summary>셀 쌍 (ax,ay)-(bx,by) → Link. CrossBlast 기준 인접 셀에만 존재.</summary>
    private Dictionary<(int, int, int, int), Link> linksByCells = new Dictionary<(int, int, int, int), Link>();
    private List<Link> allLinks = new List<Link>();

    /// <summary>
    /// CrossBlast 링크 비주얼은 사용하지 않으므로 기존 링크 오브젝트만 정리한다.
    /// </summary>
    public void CreateLinksForCrossBlastOnly(Tile[,] tiles, int stageWidth, int stageHeight)
    {
        ClearLinks();
    }

    /// <summary>
    /// 드래그 경로에 포함된 링크를 네온 컬러로 점등.
    /// </summary>
    public void SetPathLit(IList<Tile> path, Color neonColor)
    {
        ClearPathLit();
        if (path == null || path.Count < 2) return;

        for (int i = 0; i < path.Count - 1; i++)
        {
            Tile a = path[i];
            Tile b = path[i + 1];
            Link link = GetLinkBetween(a.X, a.Y, b.X, b.Y);
            if (link != null)
                link.SetPathLit(neonColor);
        }
    }

    /// <summary>
    /// 경로 점등 해제.
    /// </summary>
    public void ClearPathLit()
    {
        foreach (Link link in allLinks)
            link.ClearPathLit();
    }

    /// <summary>
    /// CrossBlast 밟고 지나갈 때 인접 링크에 펄스 파동 (흰색 HDR 반복 플래시).
    /// </summary>
    public void LightUpAdjacentLinksPulse(int cx, int cy, int stageWidth, int stageHeight, float totalDuration)
    {
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { 1, -1, 0, 0 };
        for (int d = 0; d < 4; d++)
        {
            int nx = cx + dx[d];
            int ny = cy + dy[d];
            if (nx < 0 || nx >= stageWidth || ny < 0 || ny >= stageHeight) continue;
            Link link = GetLinkBetween(cx, cy, nx, ny);
            if (link != null)
                link.SetPulseFlash(totalDuration);
        }
    }

    /// <summary>
    /// CrossBlast 폭발 시 레이저 경로(상하좌우 직선) 위의 링크를 duration간 흰색 HDR로 점등.
    /// </summary>
    public void LightUpCrossBeamLinks(int cx, int cy, int stageWidth, int stageHeight, float duration)
    {
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { 1, -1, 0, 0 };

        for (int d = 0; d < 4; d++)
        {
            int px = cx;
            int py = cy;
            for (int step = 1; ; step++)
            {
                int nx = cx + step * dx[d];
                int ny = cy + step * dy[d];
                if (nx < 0 || nx >= stageWidth || ny < 0 || ny >= stageHeight)
                    break;
                Link link = GetLinkBetween(px, py, nx, ny);
                if (link != null)
                    link.SetChainLit(duration);
                px = nx;
                py = ny;
            }
        }
    }

    private Link GetLinkBetween(int ax, int ay, int bx, int by)
    {
        if (linksByCells.TryGetValue((ax, ay, bx, by), out Link link))
            return link;
        if (linksByCells.TryGetValue((bx, by, ax, ay), out link))
            return link;
        return null;
    }

    /// <summary>
    /// CrossBlast 타일이 사라졌을 때(숫자 0) 해당 타일에 붙어 있던 링크만 제거.
    /// </summary>
    public void RemoveLinksForTile(int cx, int cy)
    {
        var toRemove = new List<(int, int, int, int)>();
        foreach (var kv in linksByCells)
        {
            int ax = kv.Key.Item1, ay = kv.Key.Item2, bx = kv.Key.Item3, by = kv.Key.Item4;
            if ((ax == cx && ay == cy) || (bx == cx && by == cy))
                toRemove.Add(kv.Key);
        }
        foreach (var key in toRemove)
        {
            if (linksByCells.TryGetValue(key, out Link link) && link != null && link.gameObject != null)
            {
                allLinks.Remove(link);
                Destroy(link.gameObject);
            }
            linksByCells.Remove(key);
        }
    }

    /// <summary>
    /// 모든 링크 오브젝트 제거 (그리드 클리어 시 GameManager가 호출).
    /// </summary>
    public void ClearLinks()
    {
        foreach (Link link in allLinks)
        {
            if (link != null && link.gameObject != null)
                Destroy(link.gameObject);
        }
        allLinks.Clear();
        linksByCells.Clear();
    }

    private void OnDestroy()
    {
        ClearLinks();
    }
}
