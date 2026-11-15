using System.Collections;
using UnityEngine;

/// <summary>
/// 피버 타임 소리: 칸 소리(피아노 멜로디) 대신 긴박하고 신나는 배경음악(Resources/Sounds/bgm_fever, 150BPM 반복)을 튼다.
/// 남은 시간이 5초 이하면 살짝 빨라지고, 피버가 끝나면 부드럽게 줄어든다. 소리 끄기(AudioListener.volume)를 그대로 따른다.
/// </summary>
public partial class GameManager
{
    private const float FeverMusicVolume = 0.6f;
    private const float FeverMusicUrgentPitch = 1.06f;
    private AudioSource feverMusicSource;
    private AudioClip feverMusicClip;
    private Coroutine feverMusicFadeRoutine;

    private void StartFeverMusic()
    {
        if (feverMusicClip == null)
            feverMusicClip = Resources.Load<AudioClip>("Sounds/bgm_fever");
        if (feverMusicClip == null)
            return;
        EnsureAudioSource(ref feverMusicSource, "FeverMusicAudioSource");
        feverMusicSource.loop = true;
        feverMusicSource.clip = feverMusicClip;
        feverMusicSource.pitch = 1f;
        feverMusicSource.volume = 0f;
        feverMusicPaused = false;
        feverMusicSource.Play();
        FadeFeverMusic(FeverMusicVolume, 0.25f, stopAtEnd: false);
    }

    /// <summary>남은 시간이 얼마 없으면 조금 빠르게(긴박하게).</summary>
    private void UpdateFeverMusic(int secondsLeft)
    {
        if (feverMusicSource == null || !feverMusicSource.isPlaying)
            return;
        float target = secondsLeft <= 5 ? FeverMusicUrgentPitch : 1f;
        feverMusicSource.pitch = Mathf.MoveTowards(feverMusicSource.pitch, target, Time.unscaledDeltaTime * 0.3f);
    }

    private bool feverMusicPaused;

    /// <summary>설정 창 등이 열려 피버 시간이 멈추면 음악도 멈춘다.</summary>
    private void SetFeverMusicPaused(bool paused)
    {
        if (feverMusicSource == null || paused == feverMusicPaused)
            return;
        feverMusicPaused = paused;
        if (paused)
            feverMusicSource.Pause();
        else
            feverMusicSource.UnPause();
    }

    private void StopFeverMusic(float fadeSeconds = 0.6f)
    {
        if (feverMusicSource == null)
            return;
        if (feverMusicPaused)
        {
            feverMusicPaused = false;
            feverMusicSource.Stop();
            return;
        }
        if (!feverMusicSource.isPlaying)
            return;
        FadeFeverMusic(0f, fadeSeconds, stopAtEnd: true);
    }

    private void FadeFeverMusic(float target, float seconds, bool stopAtEnd)
    {
        if (feverMusicFadeRoutine != null)
            StopCoroutine(feverMusicFadeRoutine);
        feverMusicFadeRoutine = StartCoroutine(FadeFeverMusicRoutine(target, seconds, stopAtEnd));
    }

    private IEnumerator FadeFeverMusicRoutine(float target, float seconds, bool stopAtEnd)
    {
        float from = feverMusicSource.volume;
        float elapsed = 0f;
        while (elapsed < seconds && feverMusicSource != null)
        {
            elapsed += Time.unscaledDeltaTime;
            feverMusicSource.volume = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        if (feverMusicSource != null)
        {
            feverMusicSource.volume = target;
            if (stopAtEnd)
                feverMusicSource.Stop();
        }
        feverMusicFadeRoutine = null;
    }
}
