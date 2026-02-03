using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CrossBlast 타일 중심으로만 Link(link.png) 배치. 인접 방향에만 붙임. 타일 사라지면 해당 링크 제거.
/// </summary>
public class LinkSystem : MonoBehaviour
{
    [Header("링크 스프라이트")]
    [Tooltip("Resources 폴더 기준 경로 (Assets/Resources/Sprites/link → Sprites/link)")]
    [SerializeField] private string linkSpritePath = "Sprites/link";
    [Tooltip("스프라이트가 안 보이면 스케일 조정 (타일 간격에 맞게)")]
    [SerializeField] private float linkScale = 0.4f;
    [Tooltip("평소 어두운 색")]
    [SerializeField] private Color linkDefaultColor = new Color(0.2f, 0.2f, 0.25f, 0.9f);

    /// <summary>셀 쌍 (ax,ay)-(bx,by) → Link. CrossBlast 기준 인접 셀에만 존재.</summary>
    private Dictionary<(int, int, int, int), Link> linksByCells = new Dictionary<(int, int, int, int), Link>();
    private List<Link> allLinks = new List<Link>();

    /// <summary>
    /// CrossBlast 타일 중심으로만 링크 생성. 위/아래/왼/오 중 인접한 타일이 있는 방향에만 link.png 배치.
    /// </summary>
    public void CreateLinksForCrossBlastOnly(Tile[,] tiles, int stageWidth, int stageHeight)
    {
        ClearLinks();

        Sprite linkSprite = Resources.Load<Sprite>(linkSpritePath);
        if (linkSprite == null)
        {
            Debug.LogWarning($"[LinkSystem] Resources에서 스프라이트를 찾을 수 없습니다: {linkSpritePath}. Assets/Resources/Sprites/ 에 link.png를 넣어주세요.");
            return;
        }

        // 상하좌우 방향
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { 1, -1, 0, 0 };

        for (int row = 0; row < stageHeight; row++)
        {
            for (int col = 0; col < stageWidth; col++)
            {
                Tile center = tiles[row, col];
                if (center == null) continue;
                if (center.GetComponent<CrossBlastTile>() == null) continue;

                // CrossBlast 타일만: 인접한 타일이 있는 방향에만 링크 생성
                for (int d = 0; d < 4; d++)
                {
                    int nc = col + dx[d];
                    int nr = row + dy[d];
                    if (nr < 0 || nr >= stageHeight || nc < 0 || nc >= stageWidth) continue;
                    Tile neighbor = tiles[nr, nc];
                    if (neighbor == null) continue;

                    Vector3 posA = center.transform.position;
                    Vector3 posB = neighbor.transform.position;
                    Vector3 mid = (posA + posB) * 0.5f;
                    mid.z = -0.4f;
                    Vector2 dir = ((Vector2)(posB - posA)).normalized;
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                    GameObject go = new GameObject($"Link_CB_{col}_{row}_{nc}_{nr}");
                    go.transform.SetParent(transform);
                    go.transform.position = mid;
                    go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    go.transform.localScale = Vector3.one * linkScale;

                    SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = linkSprite;
                    sr.color = linkDefaultColor;
                    sr.sortingOrder = 0;

                    Link link = go.AddComponent<Link>();
                    link.SetCells(col, row, nc, nr);
                    link.SetDefaultColor(linkDefaultColor);

                    var key = (col, row, nc, nr);
                    linksByCells[key] = link;
                    allLinks.Add(link);
                }
            }
        }
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
