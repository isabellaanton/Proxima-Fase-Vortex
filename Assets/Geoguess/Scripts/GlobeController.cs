using UnityEngine;

/// <summary>
/// Orbit-style globe control. The globe stays fixed at the origin; the CAMERA orbits it using
/// latitude / longitude / distance. This makes "focus on a country" trivial (the camera's
/// lat/lon simply becomes the country's lat/lon) and avoids gimbal problems.
///  - Drag (mouse or touch) rotates with inertia.
///  - Scroll wheel / pinch zooms between min/max distance.
///  - Auto-rotates when idle (if autoRotate is on).
/// Do NOT rotate the Globe object itself.
/// </summary>
public class GlobeController : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Transform globe;
    [Tooltip("Optional cloud shell; slowly spun for ambience.")]
    public Transform cloudLayer;

    [Header("Geometry")]
    public float globeRadius = 5f;

    [Header("Rotation")]
    [Tooltip("Degrees per pixel when fully zoomed out (scaled down automatically when zoomed in).")]
    public float dragSensitivity = 0.25f;
    [Tooltip("Higher = inertia stops sooner.")]
    public float inertiaDamping = 3f;
    public float maxLatitude = 80f;

    [Header("Zoom (camera distance from globe centre)")]
    public float minDistance = 6.5f;
    public float maxDistance = 20f;
    public float scrollStep = 1.5f;
    public float pinchSpeed = 0.02f;
    public float zoomSmoothing = 8f;

    [Header("Idle / ambience")]
    public bool autoRotate = true;
    public float idleDelay = 3f;
    public float idleSpeed = 4f;      // degrees / second
    public float cloudSpeed = 1.5f;   // degrees / second

    /// <summary>Set true while CameraFocus (or a menu) drives the camera.</summary>
    public bool InputLocked { get; set; }
    public float Lat => lat;
    public float Lon => lon;
    public float Distance => dist;
    /// <summary>Pixels the pointer moved since the last press. CountryPicker uses it to tell clicks from drags.</summary>
    public float PressMoved { get; private set; }

    float lat = 15f, lon = 0f, dist = 16f, targetDist = 16f;
    Vector2 velocity;              // degrees/sec (lon, lat)
    bool dragging;
    Vector2 lastPos, pressPos;
    float lastInteract;

    void Start()
    {
        if (cam == null) cam = Camera.main;
        SetOrbit(lat, lon, dist);
    }

    /// <summary>Snap the orbit to these values (also used by CameraFocus every frame).</summary>
    public void SetOrbit(float newLat, float newLon, float newDist)
    {
        lat = Mathf.Clamp(newLat, -maxLatitude, maxLatitude);
        lon = Mathf.Repeat(newLon + 180f, 360f) - 180f;
        dist = targetDist = Mathf.Clamp(newDist, minDistance, maxDistance);
        Apply();
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (!InputLocked) ReadInput(dt);
        else { dragging = false; velocity = Vector2.zero; }

        if (!dragging && !InputLocked)
        {
            lon += velocity.x * dt; lat += velocity.y * dt;
            velocity = Vector2.Lerp(velocity, Vector2.zero, 1f - Mathf.Exp(-inertiaDamping * dt));
            if (autoRotate && Time.time - lastInteract > idleDelay && velocity.sqrMagnitude < 1f)
                lon += idleSpeed * dt;
        }

        dist = Mathf.Lerp(dist, targetDist, 1f - Mathf.Exp(-zoomSmoothing * dt));
        lat = Mathf.Clamp(lat, -maxLatitude, maxLatitude);
        lon = Mathf.Repeat(lon + 180f, 360f) - 180f;

        if (cloudLayer != null) cloudLayer.Rotate(Vector3.up, cloudSpeed * dt, Space.World);
    }

    void LateUpdate() { Apply(); }

    void ReadInput(float dt)
    {
        // --- zoom ---
        float scroll = PointerInput.Scroll;
        if (scroll != 0f && !PointerInput.IsOverUI()) targetDist -= scroll * scrollStep;
        float pinch = PointerInput.PinchDelta;
        if (pinch != 0f) targetDist -= pinch * pinchSpeed;
        targetDist = Mathf.Clamp(targetDist, minDistance, maxDistance);
        if (scroll != 0f || pinch != 0f) lastInteract = Time.time;

        // --- drag ---
        if (PointerInput.IsPinching) { dragging = false; return; }

        if (PointerInput.Down)
        {
            dragging = !PointerInput.IsOverUI();      // drags that start on UI are ignored
            lastPos = pressPos = PointerInput.Position;
            PressMoved = 0f;
            velocity = Vector2.zero;
        }
        if (dragging && PointerInput.Held)
        {
            Vector2 pos = PointerInput.Position, delta = pos - lastPos; lastPos = pos;
            PressMoved = (pos - pressPos).magnitude;
            float k = DegreesPerPixel();
            // dragging right => camera moves west (lon decreases); dragging up => camera moves south.
            Vector2 deg = new Vector2(-delta.x * k, -delta.y * k);
            lon += deg.x; lat += deg.y;
            if (dt > 0f) velocity = Vector2.Lerp(velocity, deg / dt, 0.5f);
            lastInteract = Time.time;
        }
        if (PointerInput.Up) dragging = false;
    }

    float DegreesPerPixel()
    {
        float t = Mathf.InverseLerp(globeRadius, maxDistance, dist);
        return dragSensitivity * Mathf.Lerp(0.12f, 1f, t);
    }

    void Apply()
    {
        if (cam == null || globe == null) return;
        Vector3 dir = GeoMath.LatLonToDir(lat, lon);
        cam.transform.position = globe.position + dir * dist;
        cam.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
    }
}
