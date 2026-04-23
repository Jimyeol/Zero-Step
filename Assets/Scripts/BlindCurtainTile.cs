using UnityEngine;
using TMPro;

/// <summary>
/// BlindCurtain 타일: 게임 규칙은 일반 타일과 같고, 숫자 대신 타일 자체의 "?" 표시를 사용해 시각적 구분을 준다.
/// </summary>
[RequireComponent(typeof(Tile))]
public class BlindCurtainTile : MonoBehaviour
{
    private Tile tile;
    private TMP_Text numberText;

    private void Awake()
    {
        tile = GetComponent<Tile>();
        numberText = tile != null ? tile.GetNumberText() : GetComponentInChildren<TMP_Text>(true);
        RefreshVisualState();
    }

    public void RefreshVisualState()
    {
        if (tile == null || numberText == null)
            return;

        bool shouldShow = tile.IsActive;
        if (numberText.gameObject.activeSelf != shouldShow)
            numberText.gameObject.SetActive(shouldShow);
    }
}
