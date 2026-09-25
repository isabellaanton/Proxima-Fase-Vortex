using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Smoothly flies the orbit camera to a country (or any lat/lon) by driving GlobeController.
/// Framing distance is derived from the country's size, measured from the ID map by CountryPicker.
/// </summary>
public class CameraFocus : MonoBehaviour
{
    public GlobeController globe;
    public CountryPicker picker;
    public float duration = 1.4f;
    [Tooltip("How much wider than the country the view should be (visible cap = radius * this).")]
    public float framingPadding = 2.4f;
    [Tooltip("Extra pull-back in the middle of long flights.")]
    public float arcZoom = 2.5f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool IsFocusing { get; private set; }
    Coroutine running;

    public void FocusOn(CountryData c, Action onDone = null)
    {
        if (c == null || !picker.TryGetGeo(c, out var g)) { onDone?.Invoke(); return; }
        // Half-angle of the visible spherical cap: a camera at distance d sees acos(R/d) around the centre.
        float cap = Mathf.Clamp(g.angularRadiusDeg * framingPadding, 25f, 70f) * Mathf.Deg2Rad;
        float d = globe.globeRadius / Mathf.Cos(cap);
        FocusOnLatLon(g.latDeg, g.lonDeg, d, onDone);
    }

    public void FocusOnLatLon(float lat, float lon, float distance, Action onDone = null)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Run(lat, lon, distance, onDone));
    }

    IEnumerator Run(float lat, float lon, float distance, Action onDone)
    {
        IsFocusing = true; globe.InputLocked = true;
        float lat0 = globe.Lat, lon0 = globe.Lon, d0 = globe.Distance;
        float dLon = Mathf.DeltaAngle(lon0, lon);                       // shortest way round
        float angle = Mathf.Abs(dLon) + Mathf.Abs(lat - lat0);
        distance = Mathf.Clamp(distance, globe.minDistance, globe.maxDistance);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = ease.Evaluate(t / duration);
            float arc = Mathf.Sin(k * Mathf.PI) * arcZoom * Mathf.Clamp01(angle / 120f);
            globe.SetOrbit(Mathf.Lerp(lat0, lat, k), lon0 + dLon * k, Mathf.Lerp(d0, distance, k) + arc);
            yield return null;
        }
        globe.SetOrbit(lat, lon0 + dLon, distance);
        globe.InputLocked = false; IsFocusing = false; running = null;
        onDone?.Invoke();
    }
}
