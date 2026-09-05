using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds the entire game at runtime from a single component in an otherwise empty scene.
///
/// This is why the project runs on the first press of Play with nothing to wire up: there
/// are no prefabs, no configured lights, no camera to position and no UI to lay out. Drop
/// this on one GameObject and everything else assembles itself in Awake.
///
/// Order matters — cameras and systems exist before GameManager's Start builds floor one.
/// </summary>
[DefaultExecutionOrder(-100)]
public class Bootstrap : MonoBehaviour
{
    private void Awake()
    {
        // Static events survive between play sessions when Unity's domain reload is disabled,
        // so start every session from a clean slate.
        GameEvents.Clear();
        Projectile.Active.Clear();
        WorldTime.ResetToFrozen();
        Time.timeScale = 1f;
        Juice.TimeLocked = false;

        // Generate the stone textures before anything asks Palette for a material. Doing it
        // here puts the cost on the title screen, where the simulation is already stopped.
        ProceduralTexture.Warm();

        BuildEnvironmentSettings();
        BuildCamera();
        BuildSunlight();
        BuildPostFx();
        BuildSystems();
    }

    private void BuildEnvironmentSettings()
    {
        // Flat, dim ambient — the room lights should do the talking.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.32f, 0.38f, 0.48f);

        // Fog does two jobs: it hides the edge of the generated floor, and it makes distant
        // corridors fall away into black so the dungeon feels deeper than it is.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.024f, 0.031f, 0.055f);
        RenderSettings.fogDensity = 0.022f;
    }

    private void BuildCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";

        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.016f, 0.02f, 0.035f);
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 240f;

        go.AddComponent<AudioListener>();

        // URP's per-camera settings live on a companion component.
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;

        go.AddComponent<CameraRig>();
    }

    private void BuildSunlight()
    {
        var go = new GameObject("Sun");
        go.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        // Kept deliberately weak and cold — it exists to give shapes a readable top edge,
        // not to light the level. The point lights per room do that.
        light.color = new Color(0.55f, 0.66f, 1f);
        light.intensity = 1.1f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.72f;
    }

    private void BuildPostFx()
    {
        var go = new GameObject("PostFX");
        // TimeVisuals requires a Volume, which Unity adds for us.
        go.AddComponent<TimeVisuals>();
    }

    private void BuildSystems()
    {
        var go = new GameObject("Systems");

        go.AddComponent<Juice>();
        go.AddComponent<GameVfx>();
        go.AddComponent<Sound>();
        go.AddComponent<TimeDirector>();
        go.AddComponent<HUD>();

        // Last: its Start() generates floor one, and it expects everything above to exist.
        go.AddComponent<GameManager>();

        // Inert unless the player was launched with -autoshot <dir>.
        AutoCapture.InstallIfRequested(go);
    }
}

