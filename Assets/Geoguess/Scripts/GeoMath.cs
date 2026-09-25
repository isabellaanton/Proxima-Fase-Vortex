using UnityEngine;

/// <summary>
/// Single source of truth for the lat/lon <-> direction <-> UV mapping.
/// The globe mesh (GlobeMeshBuilder) uses exactly this mapping, so raycast hits can be
/// converted to ID-map pixels without relying on Unity's built-in sphere UV layout.
/// Convention: lon 0 faces +Z, east is to the RIGHT when looking at the globe from outside,
/// u = lon/360 + 0.5, v = lat/180 + 0.5 (v = 1 is the north pole = TOP row of the image).
/// </summary>
public static class GeoMath
{
    public static Vector3 LatLonRadToDir(float lat, float lon)
    {
        float c = Mathf.Cos(lat);
        return new Vector3(-c * Mathf.Sin(lon), Mathf.Sin(lat), c * Mathf.Cos(lon));
    }

    public static Vector3 LatLonToDir(float latDeg, float lonDeg)
        => LatLonRadToDir(latDeg * Mathf.Deg2Rad, lonDeg * Mathf.Deg2Rad);

    public static void DirToLatLon(Vector3 d, out float latDeg, out float lonDeg)
    {
        d.Normalize();
        latDeg = Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg;
        lonDeg = Mathf.Atan2(-d.x, d.z) * Mathf.Rad2Deg;
    }

    public static Vector2 DirToUV(Vector3 d)
    {
        DirToLatLon(d, out float lat, out float lon);
        return new Vector2(lon / 360f + 0.5f, lat / 180f + 0.5f);
    }
}
