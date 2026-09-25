using UnityEngine;

/// <summary>
/// Builds an equirectangular UV sphere (with tangents for normal mapping) using GeoMath's mapping.
/// Put it on the Globe, Clouds and Atmosphere objects with different radii.
/// Only the Globe needs 'addSphereCollider' (CountryPicker raycasts against it).
/// </summary>
[ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GlobeMeshBuilder : MonoBehaviour
{
    public float radius = 5f;
    public int lonSegments = 256;
    public int latSegments = 128;
    public bool addSphereCollider = false;

    Mesh mesh;

    void OnEnable() { Build(); }

    [ContextMenu("Rebuild")]
    public void Build()
    {
        int W = Mathf.Max(8, lonSegments), H = Mathf.Max(4, latSegments);
        int vc = (W + 1) * (H + 1);
        var verts = new Vector3[vc]; var norms = new Vector3[vc]; var uvs = new Vector2[vc]; var tans = new Vector4[vc];

        for (int j = 0; j <= H; j++)
        {
            float v = j / (float)H, lat = (v - 0.5f) * Mathf.PI;
            for (int i = 0; i <= W; i++)
            {
                float u = i / (float)W, lon = (u - 0.5f) * 2f * Mathf.PI;
                int idx = j * (W + 1) + i;
                Vector3 dir = GeoMath.LatLonRadToDir(lat, lon);
                verts[idx] = dir * radius; norms[idx] = dir; uvs[idx] = new Vector2(u, v);
                tans[idx] = new Vector4(-Mathf.Cos(lon), 0f, -Mathf.Sin(lon), -1f); // tangent = east
            }
        }

        var tris = new int[W * H * 6]; int t = 0;
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                int a = j * (W + 1) + i, b = a + 1, c = a + W + 1, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;   // clockwise seen from outside
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }

        if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        mesh = new Mesh { name = "GlobeMesh", hideFlags = HideFlags.HideAndDontSave };
        if (vc > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts; mesh.normals = norms; mesh.uv = uvs; mesh.tangents = tans; mesh.triangles = tris;
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        if (addSphereCollider)
        {
            var sc = GetComponent<SphereCollider>();
            if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = radius; sc.center = Vector3.zero;
        }
    }
}
