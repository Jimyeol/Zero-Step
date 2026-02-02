using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// 메뉴에서 Tile 프리팹을 한 번에 생성. URP Sprite-Lit + 월드 스페이스 TMP(Glow) 적용.
/// [Tools > Puzzle > Create Tile Prefab] 실행 시 Assets/Prefabs/Tile.prefab 생성.
/// </summary>
public static class CreateTilePrefab
{
    private const string TileSpritePath = "Assets/Sprites/tile.png";
    private const string PrefabPath = "Assets/Prefabs/Tile.prefab";
    private const string MaterialPath = "Assets/Materials/TileNeon.mat";

    [MenuItem("Tools/Puzzle/Create Tile Prefab")]
    public static void Create()
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TileSpritePath);
        if (sprite == null)
        {
            Debug.LogError($"[CreateTilePrefab] 스프라이트를 찾을 수 없습니다: {TileSpritePath}");
            return;
        }

        // URP Sprite-Lit 머티리얼 (Emission은 런타임에 SpriteRenderer.color HDR로 적용)
        Material tileMat = GetOrCreateTileMaterial();

        // 루트: Tile
        GameObject root = new GameObject("Tile");

        SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.material = tileMat;
        sr.sortingOrder = 0;
        sr.color = Color.white;

        BoxCollider2D col = root.AddComponent<BoxCollider2D>();
        col.size = sprite.bounds.size;
        col.offset = Vector2.zero;

        root.AddComponent<Tile>();

        // 자식: NumberText (월드 스페이스 TextMeshPro - 2D에서 숫자 보이도록)
        GameObject numberTextGo = new GameObject("NumberText");
        numberTextGo.transform.SetParent(root.transform, false);
        numberTextGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        numberTextGo.transform.localScale = Vector3.one;

        TextMeshPro tmp = numberTextGo.AddComponent<TextMeshPro>();
        tmp.text = "0";
        tmp.fontSize = 2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 1;

        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        else
            Debug.LogWarning("[CreateTilePrefab] TMP 기본 폰트가 없습니다. Window > TextMeshPro > Import TMP Essential Resources 실행 후 다시 생성하세요.");

        Tile tile = root.GetComponent<Tile>();
        SerializedObject so = new SerializedObject(tile);
        so.FindProperty("numberText").objectReferenceValue = tmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Debug.Log($"[CreateTilePrefab] 생성 완료: {PrefabPath}");
    }

    private static Material GetOrCreateTileMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
            return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (shader == null)
        {
            Debug.LogWarning("[CreateTilePrefab] URP 2D Sprite-Lit 셰이더를 찾을 수 없습니다. 기본 스프라이트 머티리얼 사용.");
            return null;
        }

        Material mat = new Material(shader);
        mat.name = "TileNeon";
        mat.SetColor("_Color", Color.white);

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        AssetDatabase.CreateAsset(mat, MaterialPath);
        return mat;
    }
}
