using Spine.Unity;
using UnityEngine;

public class SpineAnimController : MonoBehaviour
{
    private SkeletonGraphic skeletonGraphic;
    [SerializeField] private string animName;
    private bool isPlaying;

    public SkeletonGraphic SkeletonGraphic
    {
        get
        {
            if (skeletonGraphic == null)
            {
                skeletonGraphic = GetComponent<SkeletonGraphic>();
            }

            return skeletonGraphic;
        }
    }

    private void Awake()
    {
        skeletonGraphic = GetComponent<SkeletonGraphic>();
    }

    internal void Play(bool loop)
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic != null && !string.IsNullOrEmpty(animName))
        {
            if (!isPlaying)
            {
                var track = skeletonGraphic.AnimationState.SetAnimation(0, animName, loop);
                if (track != null)
                {
                    track.TimeScale = 0.5f;
                }

                isPlaying = true;
            }

            skeletonGraphic.freeze = false;
        }
    }

    internal void Pause()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic != null)
        {
            if (skeletonGraphic.SkeletonData == null)
            {
                skeletonGraphic.Initialize(false);
            }

            skeletonGraphic.freeze = true;
        }
    }

    internal void Resume()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic != null)
        {
            if (skeletonGraphic.SkeletonData == null)
            {
                skeletonGraphic.Initialize(false);
            }

            if (!isPlaying && !string.IsNullOrEmpty(animName))
            {
                Play(true);
            }

            skeletonGraphic.freeze = false;
        }
    }

    internal void Stop()
    {
        if (isPlaying)
        {
            var track = skeletonGraphic.AnimationState.GetCurrent(0);
            if (track != null)
            {
                track.TrackTime = track.Animation.Duration;
                track.TimeScale = 0f;
            }

            isPlaying = false;
        }
    }

    internal float GetAnimationDuration()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic != null)
        {
            if (skeletonGraphic.SkeletonData == null)
            {
                skeletonGraphic.Initialize(false);
            }

            if (skeletonGraphic.SkeletonData != null)
            {
                var anim = skeletonGraphic.SkeletonData.FindAnimation(animName);
                if (anim != null)
                {
                    return anim.Duration / 0.5f;
                }
            }
        }

        return 0f;
    }

    internal void SetSkeletonData(
        SkeletonDataAsset skeletonDataAsset,
        string overrideAnimName = null)
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        if (skeletonGraphic == null) return;

        string oldAnimName = animName;
        if (overrideAnimName != null)
        {
            animName = overrideAnimName;
        }

        if (skeletonGraphic.skeletonDataAsset != skeletonDataAsset)
        {
            skeletonGraphic.skeletonDataAsset = skeletonDataAsset;
            skeletonGraphic.Initialize(true);
            isPlaying = false;
        }

        if (skeletonGraphic.SkeletonData != null &&
            (string.IsNullOrEmpty(animName) ||
             skeletonGraphic.SkeletonData.FindAnimation(animName) == null))
        {
            var animations = skeletonGraphic.SkeletonData.Animations;
            animName = animations.Count > 0 ? animations.Items[0].Name : string.Empty;
        }

        if (animName != oldAnimName)
        {
            isPlaying = false;
        }
    }
}
