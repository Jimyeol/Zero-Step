using UnityEngine;
using UnityEditor;

/// <summary>
/// [Tools > Puzzle > Create PS_NeonSpark Prefab] 실행 시 네온 파티클 프리팹 생성.
/// Tile 숫자 감소 시 타일 색상과 동기화된 '파티 가루'가 살짝 떨어지는 이펙트.
/// </summary>
public static class CreateNeonSparkPrefab
{
    private const string PrefabPath = "Assets/Prefabs/PS_NeonSpark.prefab";

    [MenuItem("Tools/Puzzle/Create PS_NeonSpark Prefab")]
    public static void Create()
    {
        GameObject go = new GameObject("PS_NeonSpark");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = 0.8f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 120;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 75) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.48f;
        shape.arc = 360f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 0.85f),
            new Keyframe(1f, 0.5f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sortingOrder = 2;
        Material particleMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat")
            ?? AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
        if (particleMat != null)
            rend.material = particleMat;

        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
        Debug.Log($"[CreateNeonSparkPrefab] 생성 완료: {PrefabPath}. Tile 프리팹 자식으로 넣거나 Tile.hitEffect에 할당하세요.");
    }
}
