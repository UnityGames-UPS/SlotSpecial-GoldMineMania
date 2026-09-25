using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ImageAnimation : MonoBehaviour
{
    public enum ImageState
    {
        NONE,
        PLAYING,
        PAUSED
    }

    public static ImageAnimation Instance;

    public List<Sprite> textureArray;
    public List<Sprite> secondaryTextureArray;
    public Image rendererDelegate;
    public bool useSharedMaterial = true;
    public bool doLoopAnimation = true;
    
    public System.Action<int> onLoopComplete;
    private int currentLoopCount = 0;
    
    [SerializeField] private bool StartOnAwake;
    [SerializeField] private bool StartonEnable;

    [HideInInspector]
    public ImageState currentAnimationState;

    private int indexOfTexture;
    private float idealFrameRate = 0.0416666679f; // ~24 fps
    private float delayBetweenAnimation;
    private float loopDurationOverride;
    private bool isPlayingSecondaryArray;

    public float AnimationSpeed = 5f;
    public float delayBetweenLoop;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        EnsureRenderer();
        if (StartOnAwake)
        {
            StartAnimation();
        }
    }

    private void EnsureRenderer()
    {
        if (rendererDelegate == null)
        {
            rendererDelegate = GetComponent<Image>();
        }
    }

    void Start()
    {
        EnsureRenderer();
    }

    private void OnEnable()
    {
        EnsureRenderer();
        if (StartonEnable)
        {
            StartAnimation();
        }
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    private void AnimationProcess()
    {
        List<Sprite> activeTextureArray = GetActiveTextureArray();
        if (activeTextureArray == null || activeTextureArray.Count == 0) return;

        SetTextureOfIndex(activeTextureArray);
        indexOfTexture++;

        if (indexOfTexture >= activeTextureArray.Count)
        {
            indexOfTexture = 0;
            currentLoopCount++;
            onLoopComplete?.Invoke(currentLoopCount);

            if (!isPlayingSecondaryArray && secondaryTextureArray != null && secondaryTextureArray.Count > 0)
            {
                isPlayingSecondaryArray = true;
                UpdateFrameDelay(secondaryTextureArray.Count);
                Invoke(nameof(AnimationProcess), delayBetweenAnimation + delayBetweenLoop);
            }
            else if (doLoopAnimation)
            {
                Invoke(nameof(AnimationProcess), delayBetweenAnimation + delayBetweenLoop);
            }
            else
            {
                currentAnimationState = ImageState.NONE;
            }
        }
        else
        {
            Invoke(nameof(AnimationProcess), delayBetweenAnimation);
        }
    }

    public void StartAnimation()
    {
        if (textureArray == null || textureArray.Count == 0) return;

        EnsureRenderer();
        if (rendererDelegate == null) return;

        CancelInvoke(nameof(AnimationProcess));
        indexOfTexture = 0;
        currentLoopCount = 0;
        isPlayingSecondaryArray = false;
        currentAnimationState = ImageState.PLAYING;

        RevertToInitialState();

        UpdateFrameDelay(textureArray.Count);

        Invoke(nameof(AnimationProcess), delayBetweenAnimation);
    }

    public void PlayAnimation()
    {
        StartAnimation();
    }

    public void Play()
    {
        StartAnimation();
    }

    public void SetLoopDuration(float duration)
    {
        loopDurationOverride = Mathf.Max(0f, duration);
    }

    public void ClearLoopDuration()
    {
        loopDurationOverride = 0f;
    }

    public void PauseAnimation()
    {
        if (currentAnimationState == ImageState.PLAYING)
        {
            CancelInvoke(nameof(AnimationProcess));
            currentAnimationState = ImageState.PAUSED;
        }
    }

    public void ResumeAnimation()
    {
        if (currentAnimationState == ImageState.PAUSED && !IsInvoking(nameof(AnimationProcess)))
        {
            Invoke(nameof(AnimationProcess), delayBetweenAnimation);
            currentAnimationState = ImageState.PLAYING;
        }
    }

    public void StopAnimation()
    {
        if (currentAnimationState != ImageState.NONE)
        {
            EnsureRenderer();
            if (rendererDelegate != null && textureArray != null && textureArray.Count > 0)
            {
                rendererDelegate.sprite = textureArray[0];
            }
            CancelInvoke(nameof(AnimationProcess));
            currentAnimationState = ImageState.NONE;
            currentLoopCount = 0;
            isPlayingSecondaryArray = false;
        }
    }

    public void RevertToInitialState()
    {
        indexOfTexture = 0;
        SetTextureOfIndex();
    }

    private void SetTextureOfIndex()
    {
        SetTextureOfIndex(textureArray);
    }

    private void SetTextureOfIndex(List<Sprite> sprites)
    {
        if (sprites == null || sprites.Count == 0 || indexOfTexture < 0 || indexOfTexture >= sprites.Count) return;

        EnsureRenderer();
        if (rendererDelegate != null)
        {
            rendererDelegate.sprite = sprites[indexOfTexture];
        }
    }

    private List<Sprite> GetActiveTextureArray()
    {
        return isPlayingSecondaryArray ? secondaryTextureArray : textureArray;
    }

    private void UpdateFrameDelay(int frameCount)
    {
        delayBetweenAnimation = loopDurationOverride > 0f
            ? loopDurationOverride / frameCount
            : idealFrameRate * (float)frameCount / AnimationSpeed;
        if (delayBetweenAnimation <= 0) delayBetweenAnimation = 0.05f;
    }
}
