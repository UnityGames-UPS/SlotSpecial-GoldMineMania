using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Reuses authored UI Images as a sequential sparkle stream between feature
/// boxes and the main win display.
/// </summary>
[DisallowMultipleComponent]
public sealed class PrizeTrailController : MonoBehaviour
{
    [Header("Collection Target")]
    [SerializeField] private RectTransform targetPosition;

    [Header("Particle Stream")]
    [SerializeField, Min(1)] private int maxParticlesPerStream = 100;
    [SerializeField, Min(0f)] private float particleSpawnInterval = 0.0045f;
    [SerializeField, Min(0.01f)] private float particleTravelDuration = 1.5f;
    [SerializeField, Range(0f, 0.5f)] private float travelDurationVariation = 0.18f;
    [SerializeField, Min(0f)] private float pathArcHeight = 110f;
    [SerializeField, Min(0f)] private float pathArcVariation = 36f;
    [SerializeField, Min(0f)] private float startJitterRadius = 18f;
    [SerializeField, Min(0f)] private float bunchWidth = 32f;
    [SerializeField, Min(0f)] private float movementWobble = 11f;
    [FormerlySerializedAs("leaderScale")]
    [SerializeField, Min(0.01f)] private float maximumParticleScale = 1.35f;
    [FormerlySerializedAs("tailScale")]
    [SerializeField, Min(0.01f)] private float minimumParticleScale = 0.45f;
    [SerializeField, Range(0f, 1f)] private float outerSparkleRatio = 0.3f;
    [SerializeField, Range(0f, 1f)] private float minimumParticleAlpha = 0.5f;
    [SerializeField, Min(0f)] private float delayBetweenSources = 0.08f;

    private readonly List<RectTransform> particles = new List<RectTransform>();
    private readonly Dictionary<RectTransform, Vector3> particleBaseScales =
        new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<RectTransform, Color> particleBaseColors =
        new Dictionary<RectTransform, Color>();

    private RectTransform poolRoot;

    private void Awake()
    {
        CacheParticles();
        ResetParticles();
    }

    internal IEnumerator PlaySequentially(
        IReadOnlyList<RectTransform> sources,
        Action<int> onFirstParticleArrived = null)
    {
        CacheParticles();
        ResetParticles();

        if (targetPosition == null || sources == null || particles.Count == 0)
        {
            yield break;
        }

        int particleCount = Mathf.Min(maxParticlesPerStream, particles.Count);
        for (int sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            RectTransform source = sources[sourceIndex];
            if (source == null || !source.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 start = WorldCenterToPoolLocal(source);
            Vector3 end = WorldCenterToPoolLocal(targetPosition);
            int activeSourceIndex = sourceIndex;
            bool firstArrivalReported = false;
            Action reportFirstArrival = () =>
            {
                if (firstArrivalReported) return;

                firstArrivalReported = true;
                onFirstParticleArrived?.Invoke(activeSourceIndex);
            };
            for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
            {
                PlayParticle(
                    particles[particleIndex],
                    start,
                    end,
                    particleIndex,
                    reportFirstArrival);
            }

            float streamDuration = particleTravelDuration *
                                   (1f + travelDurationVariation) +
                                   particleSpawnInterval * (particleCount - 1);
            if (streamDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(streamDuration);
            }

            if (delayBetweenSources > 0f && sourceIndex < sources.Count - 1)
            {
                yield return new WaitForSecondsRealtime(delayBetweenSources);
            }
        }

        ResetParticles();
    }

    internal void ResetParticles()
    {
        CacheParticles();
        foreach (RectTransform particle in particles)
        {
            if (particle == null) continue;

            DOTween.Kill(particle);
            if (particleBaseScales.TryGetValue(particle, out Vector3 baseScale))
            {
                particle.localScale = baseScale;
            }
            if (particleBaseColors.TryGetValue(particle, out Color baseColor) &&
                particle.TryGetComponent(out Image image))
            {
                image.color = baseColor;
            }
            particle.gameObject.SetActive(false);
        }
    }

    private void CacheParticles()
    {
        if (poolRoot == null)
        {
            poolRoot = transform as RectTransform;
        }
        if (poolRoot == null) return;

        for (int index = 0; index < poolRoot.childCount; index++)
        {
            if (!(poolRoot.GetChild(index) is RectTransform particle) ||
                particle.GetComponent<Image>() == null)
            {
                continue;
            }

            if (!particles.Contains(particle))
            {
                particles.Add(particle);
                particleBaseScales[particle] = particle.localScale;
                particleBaseColors[particle] = particle.GetComponent<Image>().color;
            }
        }
    }

    private void PlayParticle(
        RectTransform particle,
        Vector3 start,
        Vector3 end,
        int particleIndex,
        Action onArrived)
    {
        if (particle == null) return;

        DOTween.Kill(particle);
        Vector2 jitter = GetDeterministicJitter(particleIndex) * startJitterRadius;
        Vector3 particleStart = start + new Vector3(jitter.x, jitter.y, 0f);
        Vector3 direction = (end - particleStart).normalized;
        Vector3 pathNormal = new Vector3(-direction.y, direction.x, 0f);
        bool isOuterSparkle = GetDeterministic01(particleIndex + 29) <
                              outerSparkleRatio;
        float widthMultiplier = isOuterSparkle ? 1.8f : 1f;
        float signedOffset = (GetDeterministic01(particleIndex + 31) * 2f - 1f) *
                             bunchWidth * widthMultiplier;
        float arcOffset = (GetDeterministic01(particleIndex + 41) * 2f - 1f) *
                          pathArcVariation;
        Vector3 control = Vector3.Lerp(particleStart, end, 0.5f) +
                          Vector3.up * (pathArcHeight + arcOffset) +
                          pathNormal * signedOffset * 0.35f;
        float wavePhase = GetDeterministic01(particleIndex + 53) * Mathf.PI * 2f;
        float delay = particleSpawnInterval * particleIndex;
        float durationMultiplier = Mathf.Lerp(
            1f - travelDurationVariation,
            1f + travelDurationVariation,
            GetDeterministic01(particleIndex + 67));
        float travelDuration = particleTravelDuration * durationMultiplier;
        Vector3 baseScale = particleBaseScales.TryGetValue(
            particle,
            out Vector3 cachedScale)
            ? cachedScale
            : Vector3.one;
        float scaleMultiplier = Mathf.Lerp(
            minimumParticleScale,
            maximumParticleScale,
            GetDeterministic01(particleIndex + 79));
        if (isOuterSparkle)
        {
            scaleMultiplier *= 0.72f;
        }
        Vector3 particleScale = baseScale * scaleMultiplier;
        Image image = particle.GetComponent<Image>();
        Color baseColor = particleBaseColors.TryGetValue(particle, out Color cachedColor)
            ? cachedColor
            : Color.white;
        Color trailColor = baseColor;
        trailColor.a *= Mathf.Lerp(
            minimumParticleAlpha,
            1f,
            GetDeterministic01(particleIndex + 97));
        if (isOuterSparkle)
        {
            trailColor.a *= 0.72f;
        }
        float authoredAlpha = trailColor.a;

        particle.localPosition = particleStart;
        particle.localScale = Vector3.zero;
        if (image != null)
        {
            image.color = trailColor;
        }
        particle.gameObject.SetActive(true);

        Sequence sequence = DOTween.Sequence()
            .SetTarget(particle)
            .SetUpdate(true);
        if (delay > 0f)
        {
            sequence.AppendInterval(delay);
        }

        Tween movement = DOVirtual.Float(
                0f,
                1f,
                travelDuration,
                progress =>
                {
                    Vector3 pathPosition = EvaluateQuadraticBezier(
                        particleStart,
                        control,
                        end,
                        progress);
                    float movementEnvelope = Mathf.Sin(progress * Mathf.PI);
                    float wobble = Mathf.Sin(progress * Mathf.PI * 3f + wavePhase) *
                                   movementWobble;
                    particle.localPosition = pathPosition +
                                             pathNormal *
                                             (signedOffset + wobble) *
                                             movementEnvelope;
                    if (image != null)
                    {
                        float sparkle = 0.78f + 0.22f * Mathf.Sin(
                            progress * Mathf.PI * 8f + wavePhase);
                        float arrivalFade = 1f - Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(0.76f, 1f, progress));
                        Color movingColor = trailColor;
                        movingColor.a = authoredAlpha * sparkle * arrivalFade;
                        image.color = movingColor;
                    }
                })
            .SetEase(Ease.InOutSine);
        sequence.Append(movement);
        sequence.Insert(
            delay,
            particle.DOScale(particleScale, travelDuration * 0.14f)
                .SetEase(Ease.OutBack));
        sequence.Insert(
            delay + travelDuration * 0.78f,
            particle.DOScale(Vector3.zero, travelDuration * 0.22f)
                .SetEase(Ease.InQuad));
        sequence.OnComplete(() =>
        {
            onArrived?.Invoke();
            if (particle != null)
            {
                particle.localScale = baseScale;
                if (image != null)
                {
                    image.color = baseColor;
                }
                particle.gameObject.SetActive(false);
            }
        });
    }

    private Vector3 WorldCenterToPoolLocal(RectTransform rectTransform)
    {
        Vector3 worldCenter = rectTransform.TransformPoint(rectTransform.rect.center);
        Vector3 localCenter = poolRoot.InverseTransformPoint(worldCenter);
        localCenter.z = 0f;
        return localCenter;
    }

    private static Vector3 EvaluateQuadraticBezier(
        Vector3 start,
        Vector3 control,
        Vector3 end,
        float progress)
    {
        float remaining = 1f - progress;
        return remaining * remaining * start +
               2f * remaining * progress * control +
               progress * progress * end;
    }

    private static Vector2 GetDeterministicJitter(int index)
    {
        float angle = GetDeterministic01(index) * Mathf.PI * 2f;
        float radius = Mathf.Sqrt(GetDeterministic01(index + 17));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static float GetDeterministic01(int index)
    {
        return Mathf.Repeat(Mathf.Sin(index * 12.9898f + 78.233f) * 43758.5453f, 1f);
    }
}
