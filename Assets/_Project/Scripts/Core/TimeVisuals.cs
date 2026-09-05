using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The signature look: post-processing driven directly by <see cref="WorldTime"/>.
///
/// Stand still and the colour drains out of the world, the vignette closes in and the
/// image smears with chromatic aberration — time straining against itself. Move, and
/// saturation floods back and the frame opens up.
///
/// This is the single highest-value effect in the project, because it makes the core
/// mechanic *visible*. The player never has to be told the rule; they can see it.
///
/// The whole volume and profile are built in code, so there's nothing to configure.
/// </summary>
[RequireComponent(typeof(Volume))]
public class TimeVisuals : MonoBehaviour
{
    [Header("Frozen look")]
    [SerializeField] private float frozenSaturation = -68f;
    [SerializeField] private float frozenVignette = 0.24f;
    [SerializeField] private float frozenAberration = 0.75f;
    [SerializeField] private float frozenBloom = 1.5f;
    [SerializeField] private float frozenExposure = 0.25f;

    [Header("Moving look")]
    [SerializeField] private float movingSaturation = 8f;
    [SerializeField] private float movingVignette = 0.24f;
    [SerializeField] private float movingAberration = 0.04f;
    [SerializeField] private float movingBloom = 0.85f;
    [SerializeField] private float movingExposure = 0.1f;

    [Tooltip("Visuals lag the clock slightly so the transition reads as a swell, not a switch.")]
    [SerializeField] private float responsiveness = 7f;

    private ColorAdjustments _color;
    private Vignette _vignette;
    private ChromaticAberration _aberration;
    private Bloom _bloom;

    private float _smoothedFlow;

    private void Awake()
    {
        var volume = GetComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "STILL Profile";
        volume.profile = profile;

        // ACES tonemapping is the cheapest possible upgrade to how a scene reads.
        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;

        _bloom = profile.Add<Bloom>(true);
        _bloom.threshold.overrideState = true;
        _bloom.threshold.value = 0.75f;
        _bloom.intensity.overrideState = true;
        _bloom.intensity.value = movingBloom;
        _bloom.scatter.overrideState = true;
        _bloom.scatter.value = 0.72f;

        _color = profile.Add<ColorAdjustments>(true);
        _color.saturation.overrideState = true;
        _color.contrast.overrideState = true;
        _color.contrast.value = 6f;
        _color.postExposure.overrideState = true;

        _vignette = profile.Add<Vignette>(true);
        _vignette.intensity.overrideState = true;
        _vignette.smoothness.overrideState = true;
        _vignette.smoothness.value = 0.5f;

        _aberration = profile.Add<ChromaticAberration>(true);
        _aberration.intensity.overrideState = true;

        var grain = profile.Add<FilmGrain>(true);
        grain.intensity.overrideState = true;
        grain.intensity.value = 0.22f;
        grain.type.overrideState = true;
        grain.type.value = FilmGrainLookup.Medium2;
    }

    private void Update()
    {
        // Unscaled: this must not be driven by the clock it is describing.
        float t = 1f - Mathf.Exp(-responsiveness * Time.unscaledDeltaTime);
        _smoothedFlow = Mathf.Lerp(_smoothedFlow, WorldTime.Flow, t);

        float f = _smoothedFlow;
        _color.saturation.value    = Mathf.Lerp(frozenSaturation, movingSaturation, f);
        _color.postExposure.value  = Mathf.Lerp(frozenExposure, movingExposure, f);
        _vignette.intensity.value  = Mathf.Lerp(frozenVignette, movingVignette, f);
        _aberration.intensity.value = HUD.ReducedEffects ? 0f : Mathf.Lerp(frozenAberration, movingAberration, f) * 0.3f;
        _bloom.intensity.value     = HUD.ReducedEffects ? 0.3f : Mathf.Lerp(frozenBloom, movingBloom, f);
    }
}
