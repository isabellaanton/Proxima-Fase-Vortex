using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where a country is on the globe (derived automatically from the ID map).</summary>
public struct CountryGeo { public float latDeg, lonDeg, angularRadiusDeg; }

/// <summary>
/// Identifies the country under the pointer:
///   ray -> globe SphereCollider hit -> local direction -> (u,v) -> ID-map pixel -> CountryData.
/// Also scans the ID map once at startup to work out each country's centre and size, which
/// CameraFocus uses (so you never have to type coordinates by hand).
/// </summary>
public class CountryPicker : MonoBehaviour
{
    [Header("References")]
    public Camera cam;
    public Transform globe;
    [Tooltip("The Globe's SphereCollider (added automatically by GlobeMeshBuilder > Add Sphere Collider).")]
    public Collider globeCollider;
    [Tooltip("ID map. Import settings: Read/Write ON, Compression None, Filter Point, sRGB OFF, no mipmaps.")]
    public Texture2D idMap;
    public CountryDatabase database;
    public GlobeController controller;

    [Header("Settings")]
    public float clickMaxMovePixels = 10f;
    public int colorTolerance = 2;

    public bool PickingEnabled { get; set; }
    /// <summary>Fired on a click/tap that hit the globe. Argument is null if the spot is not a fictional country.</summary>
    public event Action<CountryData> CountryClicked;
    /// <summary>Fired when the hovered country changes (desktop only).</summary>
    public event Action<CountryData> HoverChanged;

    Color32[] pixels; int w, h;
    readonly Dictionary<CountryData, CountryGeo> geo = new Dictionary<CountryData, CountryGeo>();
    CountryData hovered; bool pressOnUI;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (idMap == null || !idMap.isReadable)
        {
            Debug.LogError("CountryPicker: ID map missing or not Read/Write enabled. Fix the texture import settings.");
            enabled = false; return;
        }
        pixels = idMap.GetPixels32(); w = idMap.width; h = idMap.height;
        BuildGeoCache();
    }

    void Update()
    {
        if (!PickingEnabled) { SetHover(null); return; }
        bool overUI = PointerInput.IsOverUI();

        if (!PointerInput.HasTouch)   // hover only makes sense with a mouse
        {
            CountryData c = null;
            if (!overUI) TryPick(PointerInput.Position, out c);
            SetHover(c);
        }

        if (PointerInput.Down) pressOnUI = overUI;
        if (PointerInput.Up && !pressOnUI && controller.PressMoved <= clickMaxMovePixels)
        {
            if (TryPick(PointerInput.Position, out var c)) CountryClicked?.Invoke(c);
        }
    }

    void SetHover(CountryData c)
    {
        if (c == hovered) return;
        hovered = c; HoverChanged?.Invoke(c);
    }

    /// <summary>Returns true if the ray hit the globe. 'country' is null when no fictional country is there.</summary>
    public bool TryPick(Vector2 screenPos, out CountryData country)
    {
        country = null;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!globeCollider.Raycast(ray, out RaycastHit hit, 1000f)) return false;

        Vector3 local = globe.InverseTransformPoint(hit.point).normalized;
        Vector2 uv = GeoMath.DirToUV(local);
        int px = Mathf.Clamp((int)(uv.x * w), 0, w - 1);
        int py = Mathf.Clamp((int)(uv.y * h), 0, h - 1);
        Color32 c = pixels[py * w + px];
        if (c.r == 0 && c.g == 0 && c.b == 0) return true;          // ocean / real land
        country = database.FindByColor(c, colorTolerance);
        return true;
    }

    public bool TryGetGeo(CountryData c, out CountryGeo g) => geo.TryGetValue(c, out g);

    // Scans the ID map (every 2nd pixel) to get each country's centre direction and angular radius.
    void BuildGeoCache()
    {
        var list = database.countries; int n = list.Count;
        var keyToIdx = new Dictionary<int, int>();
        for (int i = 0; i < n; i++) if (list[i] != null) keyToIdx[list[i].ColorKey] = i;

        void Scan(Action<int, Vector3> visit)
        {
            const int stride = 2;
            for (int y = 0; y < h; y += stride)
            {
                float lat = ((y + 0.5f) / h - 0.5f) * Mathf.PI;
                for (int x = 0; x < w; x += stride)
                {
                    Color32 p = pixels[y * w + x];
                    int key = (p.r << 16) | (p.g << 8) | p.b;
                    if (key == 0 || !keyToIdx.TryGetValue(key, out int idx)) continue;
                    float lon = ((x + 0.5f) / w - 0.5f) * 2f * Mathf.PI;
                    visit(idx, GeoMath.LatLonRadToDir(lat, lon));
                }
            }
        }

        var sum = new Vector3[n]; var cnt = new int[n];
        Scan((i, d) => { sum[i] += d; cnt[i]++; });

        var centre = new Vector3[n]; var maxAng = new float[n];
        for (int i = 0; i < n; i++) centre[i] = cnt[i] > 0 ? sum[i].normalized : Vector3.forward;
        Scan((i, d) => { float a = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, centre[i]), -1f, 1f)) * Mathf.Rad2Deg; if (a > maxAng[i]) maxAng[i] = a; });

        for (int i = 0; i < n; i++)
        {
            if (list[i] == null) continue;
            if (cnt[i] == 0) { Debug.LogWarning($"CountryPicker: no pixels in ID map for '{list[i].countryName}' (colour {list[i].idColor}). Check idColor."); continue; }
            GeoMath.DirToLatLon(centre[i], out float lat, out float lon);
            geo[list[i]] = new CountryGeo { latDeg = lat, lonDeg = lon, angularRadiusDeg = maxAng[i] };
        }
    }
}
