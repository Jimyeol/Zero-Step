using UnityEngine;

/// <summary>
/// 점토 테마 특수 타일 아이콘의 "살아 있는" 움직임. 기준 위치·크기·회전·색에서 조금만 벗어나게 움직이고,
/// 효과 강도를 끄면(GameplayFeedback.Intensity = 0) 멈춘다. 아이콘이 꺼지면(칸이 0이 되거나 숨으면) 계산하지 않는다.
/// 숫자를 가리지 않도록 모서리 아이콘 크기의 일부만큼만 움직인다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ClayIconMotion : MonoBehaviour
{
    public enum Motion
    {
        None,
        /// <summary>십자폭발: 2초마다 두근두근, 살짝 흔들림.</summary>
        Heartbeat,
        /// <summary>한방향: 나가는 쪽으로 쓱 밀려 나갔다 돌아옴.</summary>
        Flow,
        /// <summary>짝꿍: 모든 짝꿍이 같은 박자로 숨 쉼.</summary>
        Breathe,
        /// <summary>충전: 천천히 밝아졌다 어두워짐(+충전 순간 톡).</summary>
        Glow,
        /// <summary>순서: 차례가 가까워질수록 좌우로 흔들림.</summary>
        Countdown,
        /// <summary>켜기: 가끔 번쩍.</summary>
        Flicker,
        /// <summary>가림: 몇 초마다 눈 깜빡.</summary>
        Blink,
        /// <summary>깜깜: 천천히 좌우로 흔들림.</summary>
        Sway
    }

    private Motion motion;
    private Vector3 flowDirection;
    private SpriteRenderer sr;
    private Vector3 basePosition;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private Color baseColor;
    private bool hasBase;
    private float phase;
    private float nextEvent;
    private float eventStart = -10f;
    private float punchStart = -10f;

    /// <summary>순서 타일 전용: 0 = 가만히, 1 = 바로 다음 이동이 차례.</summary>
    public float Urgency { get; set; }

    /// <summary>아이콘에 움직임을 붙이거나 바꾼다. 기준값은 지금의 위치·크기·회전·색이다.</summary>
    public static ClayIconMotion Attach(SpriteRenderer icon, Motion motion, Vector2 flowDirection = default)
    {
        if (icon == null)
            return null;
        ClayIconMotion m = icon.GetComponent<ClayIconMotion>();
        if (m == null)
            m = icon.gameObject.AddComponent<ClayIconMotion>();
        m.motion = motion;
        m.flowDirection = new Vector3(flowDirection.x, flowDirection.y, 0f);
        m.CaptureBase();
        return m;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        phase = Random.Range(0f, 10f);
        nextEvent = Time.unscaledTime + Random.Range(1f, 3f);
    }

    /// <summary>기준 상태를 다시 잡는다(아이콘을 다시 붙이거나 색을 바꾼 뒤).</summary>
    public void CaptureBase()
    {
        if (sr == null)
            sr = GetComponent<SpriteRenderer>();
        basePosition = transform.localPosition;
        baseScale = transform.localScale;
        baseRotation = transform.localRotation;
        baseColor = sr != null ? sr.color : Color.white;
        hasBase = true;
    }

    /// <summary>충전 등 순간 반응: 크게 톡 커졌다 돌아온다.</summary>
    public void Punch() => punchStart = Time.unscaledTime;

    private void OnDisable()
    {
        Restore();
    }

    private void Restore()
    {
        if (!hasBase)
            return;
        transform.localPosition = basePosition;
        transform.localScale = baseScale;
        transform.localRotation = baseRotation;
        if (sr != null)
            sr.color = baseColor;
    }

    private void LateUpdate()
    {
        if (!hasBase || sr == null || !sr.enabled)
            return;
        float intensity = GameplayFeedback.Intensity;
        if (intensity <= 0f || motion == Motion.None)
        {
            Restore();
            return;
        }

        float now = Time.unscaledTime;
        float t = now + phase;
        float scale = 1f;
        float angle = 0f;
        Vector3 offset = Vector3.zero;
        float alpha = 1f;
        float scaleY = 1f;
        float iconSize = sr.sprite != null ? sr.sprite.bounds.size.x : 1f;

        switch (motion)
        {
            case Motion.Heartbeat:
            {
                // 두근(크게)·두근(작게) 후 쉰다.
                float u = Mathf.Repeat(t, 2f);
                float beat = Bump(u, 0f, 0.22f) + 0.5f * Bump(u, 0.28f, 0.2f);
                scale = 1f + 0.18f * beat;
                angle = Mathf.Sin(t * 30f) * 7f * beat;
                break;
            }
            case Motion.Flow:
            {
                // 나가는 쪽으로 쓱 밀려 나갔다가 돌아온다(나갈 때 빠르고 돌아올 때 느리게).
                float u = Mathf.Repeat(t / 1.3f, 1f);
                float push = u < 0.35f ? Mathf.Sin(u / 0.35f * Mathf.PI * 0.5f) : 1f - Smooth((u - 0.35f) / 0.65f);
                // 아이콘은 방향만큼 회전돼 있으므로 부모(타일) 기준 방향을 아이콘 크기에 맞춰 옮긴다.
                offset = flowDirection * iconSize * baseScale.x * 0.32f * push;
                alpha = 1f - 0.25f * push;
                break;
            }
            case Motion.Breathe:
            {
                // 같은 시계를 써서 모든 짝꿍이 같은 박자로 움직인다.
                scale = 1f + 0.09f * Mathf.Sin(now * Mathf.PI * 2f / 1.6f);
                break;
            }
            case Motion.Glow:
            {
                float s = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 1.8f);
                alpha = 0.6f + 0.4f * s;
                scale = 1f + 0.06f * s;
                break;
            }
            case Motion.Countdown:
            {
                float urgency = Mathf.Clamp01(Urgency);
                if (urgency > 0f)
                {
                    float freq = Mathf.Lerp(1.6f, 5f, urgency);
                    angle = Mathf.Sin(now * Mathf.PI * 2f * freq) * Mathf.Lerp(5f, 13f, urgency);
                    scale = 1f + 0.08f * urgency;
                }
                break;
            }
            case Motion.Flicker:
            {
                if (now >= nextEvent)
                {
                    eventStart = now;
                    nextEvent = now + Random.Range(1.8f, 3.2f);
                }
                float e = now - eventStart;
                if (e >= 0f && e < 0.3f)
                {
                    // 번쩍-번쩍 두 번
                    float flash = Bump(e, 0f, 0.1f) + Bump(e, 0.16f, 0.1f);
                    scale = 1f + 0.16f * flash;
                    alpha = 1f - 0.55f * Mathf.Abs(Mathf.Sin(e * Mathf.PI / 0.08f)) * (1f - e / 0.3f);
                }
                break;
            }
            case Motion.Blink:
            {
                if (now >= nextEvent)
                {
                    eventStart = now;
                    nextEvent = now + Random.Range(3f, 4.5f);
                }
                float e = now - eventStart;
                if (e >= 0f && e < 0.16f)
                    scaleY = 1f - 0.8f * Bump(e, 0f, 0.16f);
                break;
            }
            case Motion.Sway:
            {
                angle = Mathf.Sin(t * Mathf.PI * 2f / 2.6f) * 9f;
                break;
            }
        }

        float punch = (now - punchStart) / 0.25f;
        if (punch >= 0f && punch < 1f)
            scale *= 1f + 0.35f * Mathf.Sin(punch * Mathf.PI);

        // 효과 강도에 비례해 움직임 폭을 줄인다.
        scale = 1f + (scale - 1f) * intensity;
        scaleY = 1f + (scaleY - 1f) * intensity;
        angle *= intensity;
        offset *= intensity;
        alpha = 1f - (1f - alpha) * intensity;

        transform.localPosition = basePosition + offset;
        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
        transform.localScale = new Vector3(baseScale.x * scale, baseScale.y * scale * scaleY, baseScale.z);
        Color c = baseColor;
        c.a = baseColor.a * alpha;
        sr.color = c;
    }

    /// <summary>start부터 length 동안 0→1→0으로 부드럽게 솟는 값.</summary>
    private static float Bump(float x, float start, float length)
    {
        float u = (x - start) / length;
        return u <= 0f || u >= 1f ? 0f : Mathf.Sin(u * Mathf.PI);
    }

    private static float Smooth(float u)
    {
        u = Mathf.Clamp01(u);
        return u * u * (3f - 2f * u);
    }
}
