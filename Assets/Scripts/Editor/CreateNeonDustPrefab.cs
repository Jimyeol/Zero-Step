using UnityEngine;
using UnityEditor;

/// <summary>
/// [Tools > Puzzle > Create PS_NeonDust Prefab] 실행 시 네온 가루 파티클 프리팹 생성.
/// 타일 밟을 때마다 해당 타일 색상을 가진 가루가 중력으로 아래로 자연스럽게 떨어지는 이펙트.
/// </summary>
public static class CreateNeonDustPrefab
{
    private const string PrefabPath = "Assets/Prefabs/PS_NeonDust.prefab";

    [MenuItem("Tools/Puzzle/Create PS_NeonDust Prefab")]
    public static void Create()
    {
        GameObject go = new GameObject("PS_NeonDust");
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.6f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 60;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;
        shape.arc = 360f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.5f;
        noise.quality = ParticleSystemNoiseQuality.Medium;
        noise.scrollSpeed = 0f;
        noise.damping = true;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
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
        Debug.Log($"[CreateNeonDustPrefab] 생성 완료: {PrefabPath}. Tile 프리팹 자식으로 넣거나 Tile.hitEffect에 할당하세요.");
    }
}
