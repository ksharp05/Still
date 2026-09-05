using UnityEngine;

/// <summary>
/// Fixed-angle follow camera with lookahead and shake.
///
/// The rotation never changes — that's what lets W always mean "up the screen".
/// Lookahead pushes the view slightly toward where you're aiming, so you see the
/// enemy you're about to hit rather than the wall behind you.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }

    [Header("Framing")]
    [SerializeField] private float height = 18f;
    [SerializeField] private float distance = 12.5f;
    [SerializeField] private float followSmooth = 9f;

    [Tooltip("How far the camera leans toward the aim point, in world units.")]
    [SerializeField] private float lookAhead = 3.2f;

    [Header("Shake")]
    [SerializeField] private float shakeDecay = 4.5f;
    [SerializeField] private float shakeMagnitude = 0.55f;

    [Header("Dash kick")]
    [SerializeField] private float baseFov = 55f;
    [SerializeField] private float dashFov = 62f;

    private Transform _target;
    private PlayerController _player;
    private Camera _cam;
    private Vector3 _smoothedFocus;
    private float _shake;

    private void Awake()
    {
        Instance = this;
        _cam = GetComponent<Camera>();

        // Point the camera down at the fixed angle implied by height/distance.
        float pitch = Mathf.Atan2(height, distance) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetTarget(Transform target)
    {
        _target = target;
        _player = target != null ? target.GetComponent<PlayerController>() : null;

        if (_target != null)
        {
            _smoothedFocus = _target.position;
            transform.position = _smoothedFocus + new Vector3(0f, height, -distance);
        }
    }

    public void Shake(float strength) => _shake = Mathf.Max(_shake, HUD.ReducedEffects ? 0f : strength);

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 focus = _target.position;
        if (_player != null) focus += _player.AimDirection * lookAhead;

        // Exponential smoothing — frame-rate independent, and never overshoots.
        float t = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
        _smoothedFocus = Vector3.Lerp(_smoothedFocus, focus, t);

        Vector3 position = _smoothedFocus + new Vector3(0f, height, -distance);

        if (_shake > 0.001f)
        {
            // Shake runs on unscaled time so it keeps moving through hit-stop.
            _shake = Mathf.MoveTowards(_shake, 0f, shakeDecay * Time.unscaledDeltaTime);
            position += Random.insideUnitSphere * (_shake * shakeMagnitude);
        }

        transform.position = position;

        if (_cam != null)
        {
            bool dashing = _player != null && _player.IsDashing;
            float wantFov = dashing ? dashFov : baseFov;
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, wantFov, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }
    }
}
