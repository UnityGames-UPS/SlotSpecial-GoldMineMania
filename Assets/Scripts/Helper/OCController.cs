using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class OCController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OrientationChange orientationChange;
    [SerializeField] private CanvasScaler canvasScaler;
    [SerializeField, InspectorName("Slot BG")] private Transform slotObject;
    [SerializeField] private List<RectTransform> resizedObjects = new List<RectTransform>();
    [SerializeField] private List<RectTransform> squareResizedObjects = new List<RectTransform>();

    [Header("Panel Toggle Settings")]
    [SerializeField] private GameObject landscapePanelObject;
    [SerializeField] private GameObject portraitPanelObject;

    [Header("Background Toggle Settings")]
    [SerializeField] private GameObject landscapeBackground;
    [SerializeField] private GameObject portraitBackground;
    [SerializeField] private GameObject wheelLandscapeBackground;
    [SerializeField] private GameObject wheelPortraitBackground;

    [Header("Feature Dark Background")]
    [SerializeField] private RectTransform featureDarkBackground;
    [SerializeField] private Vector2 portraitFeatureDarkBackgroundSize = new Vector2(3000f, 4000f);

    [Header("Canvas Scaler Resolutions")]
    [SerializeField] private Vector2 landscapeReferenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 portraitReferenceResolution = new Vector2(1080f, 1920f);

    [Header("Resized Object Dimensions")]
    [SerializeField] private Vector2 landscapeResizedObjectSize = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 portraitResizedObjectSize = new Vector2(1080f, 1920f);

    [Header("Square Resized Object Dimensions")]
    [SerializeField] private Vector2 landscapeSquareResizedObjectSize = new Vector2(1920f, 1080f);
    [SerializeField] private Vector2 portraitSquareResizedObjectSize = new Vector2(1920f, 1920f);

    [Header("Portrait Slot Settings")]
    [SerializeField] private Vector2 portrait5x3Position = new Vector2(0f, -380f);
    [SerializeField, Min(0.01f)] private float portrait5x3Scale = 0.78f;
    [SerializeField] private Vector2 portrait7x3Position = new Vector2(0f, -380f);
    [SerializeField, Min(0.01f)] private float portrait7x3Scale = 0.8f;
    [SerializeField] private Vector2 portraitTwo7x3Position = new Vector2(0f, -380f);
    [SerializeField, Min(0.01f)] private float portraitTwo7x3Scale = 1f;

    // These scene references are already wired and are only used to detect
    // which scale to apply. They do not need to clutter the Inspector.
    [SerializeField, HideInInspector] private RectTransform portraitBaseSlotLayout;
    [SerializeField, HideInInspector] private RectTransform portraitMegaSlotLayout;
    [SerializeField, HideInInspector] private RectTransform portraitUltimateSlotLayout;

    [Header("Info Page & Guide Settings")]
    [SerializeField] private RectTransform infoPageScrollObject;
    [SerializeField] private RectTransform guideScrollObject;

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.2f;

    private List<Tween> activeTweens = new List<Tween>();
    private Vector3 authoredLandscapeSlotScale;
    private Vector3 authoredLandscapeSlotPosition;
    private Vector2 authoredLandscapeFeatureDarkBackgroundSize;
    private bool slotWasAdjustedForPortrait;
    private bool isMobilePortraitLayoutActive;
    private bool hasAppliedPortraitSlotLayout;
    private PortraitSlotLayout appliedPortraitSlotLayout;

    private enum PortraitSlotLayout
    {
        BaseFiveByThree,
        MegaSevenByThree,
        UltimateTwoSevenByThree
    }

    private void Awake()
    {
        if (orientationChange == null)
        {
            orientationChange = GetComponent<OrientationChange>();
            if (orientationChange == null)
            {
                orientationChange = Object.FindFirstObjectByType<OrientationChange>();
            }
        }
        if (canvasScaler == null && orientationChange != null)
        {
            canvasScaler = orientationChange.GetComponent<CanvasScaler>();
        }

        if (slotObject != null)
        {
            authoredLandscapeSlotScale = slotObject.localScale;
            authoredLandscapeSlotPosition = slotObject.localPosition;
        }

        if (featureDarkBackground != null)
        {
            authoredLandscapeFeatureDarkBackgroundSize = featureDarkBackground.sizeDelta;
        }
    }

    private void OnEnable()
    {
        if (orientationChange != null)
        {
            orientationChange.OnOrientationChangedInstance += HandleOrientationChange;
        }
        else
        {
            OrientationChange.OnOrientationChanged += HandleOrientationChange;
        }
    }

    private void OnDisable()
    {
        if (orientationChange != null)
        {
            orientationChange.OnOrientationChangedInstance -= HandleOrientationChange;
        }
        else
        {
            OrientationChange.OnOrientationChanged -= HandleOrientationChange;
        }
    }

    private void HandleOrientationChange(OrientationChange.OrientationMode mode, int width, int height)
    {
        KillActiveTweens();

        bool isMobilePortrait = (mode == OrientationChange.OrientationMode.MobilePortrait);
        isMobilePortraitLayoutActive = isMobilePortrait;

        // 1. Toggle Landscape vs Portrait Panel Objects
        if (landscapePanelObject != null)
        {
            landscapePanelObject.SetActive(!isMobilePortrait);
        }
        if (portraitPanelObject != null)
        {
            portraitPanelObject.SetActive(isMobilePortrait);
        }

        // 2. Toggle Landscape vs Portrait Background Objects
        if (landscapeBackground != null)
        {
            landscapeBackground.SetActive(!isMobilePortrait);
        }
        if (portraitBackground != null)
        {
            portraitBackground.SetActive(isMobilePortrait);
        }

        // Toggle Wheel Landscape vs Portrait Background Objects
        if (wheelLandscapeBackground != null)
        {
            wheelLandscapeBackground.SetActive(!isMobilePortrait);
        }
        if (wheelPortraitBackground != null)
        {
            wheelPortraitBackground.SetActive(isMobilePortrait);
        }

        if (featureDarkBackground != null)
        {
            Vector2 darkBackgroundSize = isMobilePortrait
                ? portraitFeatureDarkBackgroundSize
                : authoredLandscapeFeatureDarkBackgroundSize;

            if (transitionDuration > 0f)
            {
                Tween darkBackgroundTween = featureDarkBackground
                    .DOSizeDelta(darkBackgroundSize, transitionDuration)
                    .SetEase(Ease.OutCubic);
                activeTweens.Add(darkBackgroundTween);
            }
            else
            {
                featureDarkBackground.sizeDelta = darkBackgroundSize;
            }
        }

        // 3. Update Canvas Scaler Reference Resolution
        if (canvasScaler != null)
        {
            Vector2 targetRefRes = isMobilePortrait ? portraitReferenceResolution : landscapeReferenceResolution;
            canvasScaler.referenceResolution = targetRefRes;
        }

        // 4. Resize Target RectTransforms
        Vector2 targetSize = isMobilePortrait ? portraitResizedObjectSize : landscapeResizedObjectSize;
        if (resizedObjects != null)
        {
            foreach (var rect in resizedObjects)
            {
                if (rect != null)
                {
                    if (transitionDuration > 0)
                    {
                        Tween t = rect.DOSizeDelta(targetSize, transitionDuration).SetEase(Ease.OutCubic);
                        activeTweens.Add(t);
                    }
                    else
                    {
                        rect.sizeDelta = targetSize;
                    }
                }
            }
        }

        // 4b. Resize Target RectTransforms (1920x1080 Landscape, 1920x1920 Portrait)
        Vector2 targetSquareSize = isMobilePortrait ? portraitSquareResizedObjectSize : landscapeSquareResizedObjectSize;
        if (squareResizedObjects != null)
        {
            foreach (var rect in squareResizedObjects)
            {
                if (rect != null)
                {
                    if (transitionDuration > 0)
                    {
                        Tween t = rect.DOSizeDelta(targetSquareSize, transitionDuration).SetEase(Ease.OutCubic);
                        activeTweens.Add(t);
                    }
                    else
                    {
                        rect.sizeDelta = targetSquareSize;
                    }
                }
            }
        }

        // 5. Leave the authored landscape slot transform untouched at startup.
        // Only restore it when returning from a portrait layout.
        if (slotObject != null)
        {
            if (isMobilePortrait)
            {
                PortraitSlotLayout activeLayout = ResolveActivePortraitSlotLayout();
                GetPortraitSlotTransform(
                    activeLayout,
                    out Vector3 targetScale,
                    out Vector3 targetPosition);
                ApplySlotTransform(targetScale, targetPosition);
                slotWasAdjustedForPortrait = true;
                appliedPortraitSlotLayout = activeLayout;
                hasAppliedPortraitSlotLayout = true;
            }
            else if (slotWasAdjustedForPortrait)
            {
                ApplySlotTransform(authoredLandscapeSlotScale, authoredLandscapeSlotPosition);
                slotWasAdjustedForPortrait = false;
                hasAppliedPortraitSlotLayout = false;
            }
        }

        // 6. Update Info Page Scroll Object Height (1080 for Landscape, 1920 for Mobile Portrait)
        if (infoPageScrollObject != null)
        {
            float targetHeight = isMobilePortrait ? 1920f : 1080f;
            Vector2 targetScrollSize = new Vector2(infoPageScrollObject.sizeDelta.x, targetHeight);
            if (transitionDuration > 0)
            {
                Tween scrollTween = infoPageScrollObject.DOSizeDelta(targetScrollSize, transitionDuration).SetEase(Ease.OutCubic);
                activeTweens.Add(scrollTween);
            }
            else
            {
                infoPageScrollObject.sizeDelta = targetScrollSize;
            }
        }

        // 7. Update Guide Scroll Object Height (1080 for Landscape, 1920 for Mobile Portrait)
        if (guideScrollObject != null)
        {
            float targetHeight = isMobilePortrait ? 1920f : 1080f;
            Vector2 targetScrollSize = new Vector2(guideScrollObject.sizeDelta.x, targetHeight);
            if (transitionDuration > 0)
            {
                Tween scrollTween = guideScrollObject.DOSizeDelta(targetScrollSize, transitionDuration).SetEase(Ease.OutCubic);
                activeTweens.Add(scrollTween);
            }
            else
            {
                guideScrollObject.sizeDelta = targetScrollSize;
            }
        }
    }

    private void LateUpdate()
    {
        if (!isMobilePortraitLayoutActive || slotObject == null) return;

        PortraitSlotLayout activeLayout = ResolveActivePortraitSlotLayout();
        if (hasAppliedPortraitSlotLayout &&
            activeLayout == appliedPortraitSlotLayout)
        {
            return;
        }

        KillActiveTweens();
        GetPortraitSlotTransform(
            activeLayout,
            out Vector3 targetScale,
            out Vector3 targetPosition);
        ApplySlotTransform(targetScale, targetPosition);
        appliedPortraitSlotLayout = activeLayout;
        hasAppliedPortraitSlotLayout = true;
        slotWasAdjustedForPortrait = true;
    }

    private PortraitSlotLayout ResolveActivePortraitSlotLayout()
    {
        if (portraitUltimateSlotLayout != null &&
            portraitUltimateSlotLayout.gameObject.activeSelf)
        {
            return PortraitSlotLayout.UltimateTwoSevenByThree;
        }

        if (portraitMegaSlotLayout != null &&
            portraitMegaSlotLayout.gameObject.activeSelf)
        {
            return PortraitSlotLayout.MegaSevenByThree;
        }

        if (portraitBaseSlotLayout != null &&
            portraitBaseSlotLayout.gameObject.activeSelf)
        {
            return PortraitSlotLayout.BaseFiveByThree;
        }

        if (hasAppliedPortraitSlotLayout)
        {
            return appliedPortraitSlotLayout;
        }

        return PortraitSlotLayout.BaseFiveByThree;
    }

    private void GetPortraitSlotTransform(
        PortraitSlotLayout activeLayout,
        out Vector3 targetScale,
        out Vector3 targetPosition)
    {
        float fittedScale = GetCustomPortraitSlotScale(activeLayout);
        Vector2 slotPosition = GetCustomPortraitSlotPosition(activeLayout);
        targetScale = new Vector3(
            fittedScale,
            fittedScale,
            authoredLandscapeSlotScale.z);
        targetPosition = new Vector3(
            slotPosition.x,
            slotPosition.y,
            authoredLandscapeSlotPosition.z);
    }

    private float GetCustomPortraitSlotScale(PortraitSlotLayout layout)
    {
        return Mathf.Max(
            0.01f,
            layout switch
            {
                PortraitSlotLayout.MegaSevenByThree => portrait7x3Scale,
                PortraitSlotLayout.UltimateTwoSevenByThree => portraitTwo7x3Scale,
                _ => portrait5x3Scale
            });
    }

    private Vector2 GetCustomPortraitSlotPosition(PortraitSlotLayout layout)
    {
        return layout switch
        {
            PortraitSlotLayout.MegaSevenByThree => portrait7x3Position,
            PortraitSlotLayout.UltimateTwoSevenByThree => portraitTwo7x3Position,
            _ => portrait5x3Position
        };
    }

    private void ApplySlotTransform(Vector3 targetScale, Vector3 targetPosition)
    {
        if (transitionDuration > 0)
        {
            Tween scaleTween = slotObject.DOScale(targetScale, transitionDuration).SetEase(Ease.OutCubic);
            Tween posTween = slotObject.DOLocalMove(targetPosition, transitionDuration).SetEase(Ease.OutCubic);
            activeTweens.Add(scaleTween);
            activeTweens.Add(posTween);
        }
        else
        {
            slotObject.localScale = targetScale;
            slotObject.localPosition = targetPosition;
        }
    }

    private void KillActiveTweens()
    {
        foreach (var t in activeTweens)
        {
            if (t != null && t.IsActive())
            {
                t.Kill();
            }
        }
        activeTweens.Clear();
    }
}
