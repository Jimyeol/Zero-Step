using UnityEngine;
using TMPro;

/// <summary>
/// BlindCurtain 타일: 게임 규칙은 일반 타일과 같고, 숫자 대신 물음표만 표시한다.
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
        if (numberText == null)
            return;

        bool shouldShow = tile == null || tile.IsActive;

        numberText.text = "?";
        if (numberText.gameObject.activeSelf != shouldShow)
            numberText.gameObject.SetActive(shouldShow);

        if (shouldShow)
            numberText.ForceMeshUpdate(true, true);
    }

    private void LateUpdate()
    {
        RefreshVisualState();
    }
}
