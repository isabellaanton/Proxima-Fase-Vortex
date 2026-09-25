using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tiny input abstraction: mouse + touch, works with the old Input Manager AND the new Input System.
/// Everything else in the project reads input only through here, which keeps mobile support cheap.
/// </summary>
public static class PointerInput
{
    static float lastPinchDist, pinchDelta;
    static int lastPinchFrame = -10, deltaFrame = -1;

    /// <summary>Change in distance between two fingers this frame (pixels). >0 = spreading (zoom in).</summary>
    public static float PinchDelta
    {
        get
        {
            if (deltaFrame != Time.frameCount)
            {
                deltaFrame = Time.frameCount; pinchDelta = 0f;
                if (TryGetTwoTouches(out var a, out var b))
                {
                    float d = Vector2.Distance(a, b);
                    if (lastPinchFrame == Time.frameCount - 1) pinchDelta = d - lastPinchDist;
                    lastPinchDist = d; lastPinchFrame = Time.frameCount;
                }
            }
            return pinchDelta;
        }
    }

    public static bool IsPinching => TryGetTwoTouches(out _, out _);

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    static bool TouchActive { get { var t = Touchscreen.current; return t != null && (t.primaryTouch.press.isPressed || t.primaryTouch.press.wasReleasedThisFrame); } }
    public static bool HasTouch => Touchscreen.current != null && Touchscreen.current.touches.Count > 0 && TouchActive;

    public static Vector2 Position
    {
        get
        {
            if (TouchActive) return Touchscreen.current.primaryTouch.position.ReadValue();
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        }
    }
    public static bool Down => (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                            || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
    public static bool Held => (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                            || (Mouse.current != null && Mouse.current.leftButton.isPressed);
    public static bool Up => (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
                          || (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame);
    public static float Scroll
    {
        get { if (Mouse.current == null) return 0f; float y = Mouse.current.scroll.ReadValue().y; return Mathf.Approximately(y, 0f) ? 0f : Mathf.Sign(y); }
    }
    static bool TryGetTwoTouches(out Vector2 a, out Vector2 b)
    {
        a = b = default; var t = Touchscreen.current; if (t == null) return false;
        int n = 0;
        foreach (var tc in t.touches)
        {
            if (!tc.press.isPressed) continue;
            if (n == 0) a = tc.position.ReadValue(); else if (n == 1) b = tc.position.ReadValue();
            n++;
        }
        return n >= 2;
    }
    public static bool IsOverUI()
    {
        var es = EventSystem.current; if (es == null) return false;
        var t = Touchscreen.current;
        if (t != null && t.primaryTouch.press.isPressed) return es.IsPointerOverGameObject(t.primaryTouch.touchId.ReadValue());
        return es.IsPointerOverGameObject();
    }
#else
    public static bool HasTouch => Input.touchCount > 0;
    public static Vector2 Position => Input.mousePosition;      // Unity simulates the mouse from the first touch
    public static bool Down => Input.GetMouseButtonDown(0);
    public static bool Held => Input.GetMouseButton(0);
    public static bool Up => Input.GetMouseButtonUp(0);
    public static float Scroll { get { float y = Input.mouseScrollDelta.y; return Mathf.Approximately(y, 0f) ? 0f : Mathf.Sign(y); } }
    static bool TryGetTwoTouches(out Vector2 a, out Vector2 b)
    {
        a = b = default;
        if (Input.touchCount < 2) return false;
        a = Input.GetTouch(0).position; b = Input.GetTouch(1).position; return true;
    }
    public static bool IsOverUI()
    {
        var es = EventSystem.current; if (es == null) return false;
        if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return es.IsPointerOverGameObject();
    }
#endif
}
