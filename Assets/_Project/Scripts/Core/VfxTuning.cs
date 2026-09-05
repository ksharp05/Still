using UnityEngine;

[CreateAssetMenu(menuName = "STILL/Particle tuning")]
public class VfxTuning : ScriptableObject
{
    [Range(.1f, 2f)] public float Density = 1f;
    [Range(.2f, 3f)] public float Brightness = 1.4f;
    [Range(.1f, .6f)] public float ReducedDensity = .3f;
    [Range(10f, 60f)] public float AmbientDistance = 28f;
    [Range(0f, 8f)] public float DustPerSecond = 2f;
    [Range(.08f, .4f)] public float SlashLifetime = .22f;
    [Range(.06f, .4f)] public float SlashWidth = .22f;
}
