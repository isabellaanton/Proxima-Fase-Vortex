#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Geoguess > Generate Sample Data
/// Creates everything needed to run the prototype with NO external art:
///   placeholder Earth albedo, normal map, cloud texture, ID map (10 fictional countries),
///   10 procedural flag sprites, 10 CountryData assets, a CountryDatabase and 4 materials.
/// Replace the placeholder Earth textures with real ones later (see README).
/// </summary>
public static class SampleDataGenerator
{
    const string Root = "Assets/Geoguess/Generated";
    const int W = 2048, H = 1024;

    enum Flag { HStripes, VStripes, NordicCross, Disc, Crescent, Diagonal }

    class Spec
    {
        public string name; public Color32 id; public float lat, lon, rLat, rLon; public CountryDifficulty diff;
        public string c1, c2, c3; public Flag flag; public Color32 f1, f2, f3; public string decoys;
    }

    static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

    static Spec S(string name, Color32 id, float lat, float lon, float rLat, float rLon, CountryDifficulty d,
                  string c1, string c2, string c3, Flag flag, Color32 f1, Color32 f2, Color32 f3, string decoys)
        => new Spec { name = name, id = id, lat = lat, lon = lon, rLat = rLat, rLon = rLon, diff = d, c1 = c1, c2 = c2, c3 = c3, flag = flag, f1 = f1, f2 = f2, f3 = f3, decoys = decoys };

    static List<Spec> Specs() => new List<Spec>
    {
        S("Valdoria", C(230,60,60), 48, 62, 8, 16, CountryDifficulty.Easy,
          "A landlocked highland republic of cold steppes and snow-capped mountain ranges.",
          "Carved into the heart of Central Asia, far from any ocean.",
          "Flag: three horizontal stripes of green, white and crimson.",
          Flag.HStripes, C(30,130,60), C(245,245,245), C(200,30,50), "Kazoria|Ostrava Rise|Belmarch|Tundria"),
        S("Nerath Isles", C(60,120,230), -10, -30, 6, 10, CountryDifficulty.Easy,
          "A tropical archipelago of volcanic mountains and black-sand beaches.",
          "Mid-Atlantic islands lying between South America and Africa.",
          "Flag: a blue field with a white crescent and three stars.",
          Flag.Crescent, C(30,70,180), C(245,245,245), C(0,0,0), "Sarn Islands|Moriah Reef|Thal Archipelago|Quenna Isles"),
        S("Kessandra", C(230,190,40), 18, 22, 10, 14, CountryDifficulty.Medium,
          "A hot, arid desert nation of rocky plateaus and scattered oases.",
          "Inland in northern Africa, ringed by real desert countries.",
          "Flag: three vertical stripes of black, gold and orange.",
          Flag.VStripes, C(20,20,20), C(235,190,40), C(230,120,30), "Kassandria|Zarmoun|Dalvek|Ombara"),
        S("Solmara", C(60,200,120), -32, 160, 6, 9, CountryDifficulty.Easy,
          "A temperate island nation of green hills, deep fjords and sheep pastures.",
          "In the South Pacific, east of Australia.",
          "Flag: a yellow disc on a deep blue field.",
          Flag.Disc, C(20,50,140), C(250,210,40), C(0,0,0), "Solmaria|Tevaro|Anwick Land|Port Ellis"),
        S("Brennhold", C(180,80,220), 76, 90, 5, 24, CountryDifficulty.Medium,
          "A frozen polar island of glaciers, ice caps and aurora-lit winters.",
          "Floating in the Arctic Ocean, north of Russia's coast.",
          "Flag: a white cross on a blue field.",
          Flag.NordicCross, C(25,60,150), C(245,245,245), C(0,0,0), "Brennmark|Hvalheim|Skarnholt|Iskeland"),
        S("Tamarind Coast", C(240,130,40), -8, 75, 6, 9, CountryDifficulty.Medium,
          "Warm rainforest islands with spice plantations and coral reefs.",
          "In the Indian Ocean, south-west of India.",
          "Flag: a green field crossed by a yellow diagonal band.",
          Flag.Diagonal, C(30,140,70), C(245,210,50), C(0,0,0), "Cinnamon Reach|Tamara Bay|Saffron Isles|Malabrune"),
        S("Orvane", C(40,200,210), -8, -63, 8, 11, CountryDifficulty.Hard,
          "A dense rainforest nation of vast rivers, humid air and jaguars.",
          "Deep inside the Amazon basin of South America.",
          "Flag: three horizontal stripes of teal, yellow and teal.",
          Flag.HStripes, C(20,150,150), C(245,215,50), C(20,150,150), "Orvanha|Ucayara|Tapiro|Selvador"),
        S("Zephyria", C(200,60,150), 38, -40, 5, 8, CountryDifficulty.Hard,
          "Windswept temperate islands of high sea cliffs and mild, foggy summers.",
          "In the middle of the North Atlantic, between Europe and North America.",
          "Flag: a white disc on a sky-blue field.",
          Flag.Disc, C(110,180,235), C(250,250,250), C(0,0,0), "Zephyros|Aeolia|Marrow Isles|Skerrin"),
        S("Ashenmoor", C(130,200,50), 55, -110, 9, 15, CountryDifficulty.Medium,
          "Vast boreal forests, thousands of lakes and long, harsh winters.",
          "Embedded in the northern interior of North America.",
          "Flag: a dark red field crossed by a white diagonal band.",
          Flag.Diagonal, C(140,25,35), C(245,245,245), C(0,0,0), "Ashenvale|Moorhaven|Kettle Lands|Northmere"),
        S("Quillon", C(120,120,120), -52, 60, 5, 12, CountryDifficulty.Hard,
          "Windblown sub-Antarctic rock and tundra crowded with penguin colonies.",
          "Isolated in the far southern Indian Ocean, near Antarctica.",
          "Flag: three vertical stripes of white, blue and white.",
          Flag.VStripes, C(245,245,245), C(30,80,170), C(245,245,245), "Quillane|Southmark|Cape Rusk|Penrith Deep"),
    };

    // continents for the crude placeholder Earth: lat, lon, rLat, rLon
    static readonly float[][] Continents =
    {
        new float[]{ 48,-100,22,35 }, new float[]{ -15,-60,32,18 }, new float[]{ 52,60,20,80 },
        new float[]{ 5,20,32,22 }, new float[]{ -25,134,12,20 }, new float[]{ 70,-40,10,18 },
    };

    // Periodic-in-u noise so textures wrap without a seam.
    static float Noise(float u, float v, float f, float seed)
    {
        float a = u * Mathf.PI * 2f;
        return Mathf.PerlinNoise(Mathf.Cos(a) * f + seed + 200f, Mathf.Sin(a) * f + v * f * 0.5f + seed + 200f);
    }
    static float Fbm(float u, float v, float f, float seed)
    {
        float s = 0, amp = 0.5f;
        for (int o = 0; o < 4; o++) { s += amp * Noise(u, v, f, seed); f *= 2; amp *= 0.5f; }
        return s / 0.9375f;
    }
    static float Ell(float lat, float lon, float la0, float lo0, float rLa, float rLo)
    {
        float a = (lat - la0) / rLa, b = Mathf.DeltaAngle(lon, lo0) / rLo; return a * a + b * b;
    }

    [MenuItem("Geoguess/Generate Sample Data")]
    public static void Generate()
    {
        var mapShader = Shader.Find("Geoguess/GlobeURP");
        if (mapShader == null) { Debug.LogError("Geoguess shaders not found. Make sure the Shaders folder is imported and compiled (URP project)."); return; }

        foreach (var sub in new[] { "Textures", "Flags", "Data", "Materials" })
            Directory.CreateDirectory($"{Root}/{sub}");

        var specs = Specs();
        var albedo = new Color32[W * H]; var idmap = new Color32[W * H]; var height = new float[W * H];

        try
        {
            for (int y = 0; y < H; y++)
            {
                if (y % 32 == 0) EditorUtility.DisplayProgressBar("Geoguess", "Painting placeholder Earth + ID map…", y / (float)H);
                float v = (y + 0.5f) / H, lat = (v - 0.5f) * 180f;
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, lon = (u - 0.5f) * 360f;
                    float wob = (Fbm(u, v, 5, 3f) - 0.5f) * 0.9f;

                    float cq = 99f; foreach (var c in Continents) cq = Mathf.Min(cq, Ell(lat, lon, c[0], c[1], c[2], c[3]));
                    bool land = cq + wob < 1f || lat < -72f + wob * 8f;

                    int country = -1; float best = 99f;
                    for (int i = 0; i < specs.Count; i++)
                    {
                        var s = specs[i]; float q = Ell(lat, lon, s.lat, s.lon, s.rLat, s.rLon);
                        if (q > 2f) continue;
                        q += wob * 0.8f;
                        if (q < 1f && q < best) { best = q; country = i; }
                    }
                    if (country >= 0) land = true;

                    int idx = y * W + x;
                    float n = Fbm(u, v, 12, 9f);
                    Color col;
                    if (!land) col = Color.Lerp(new Color(0.03f, 0.15f, 0.35f), new Color(0.08f, 0.32f, 0.55f), Fbm(u, v, 6, 1f));
                    else
                    {
                        col = Color.Lerp(new Color(0.20f, 0.45f, 0.18f), new Color(0.55f, 0.48f, 0.3f), n);
                        if (Mathf.Abs(lat) > 68f) col = Color.Lerp(col, Color.white, 0.7f);
                        if (country >= 0) col = Color.Lerp(col, (Color)specs[country].id, 0.4f);
                    }
                    albedo[idx] = col;
                    idmap[idx] = country >= 0 ? specs[country].id : new Color32(0, 0, 0, 255);
                    height[idx] = land ? 0.25f + 0.75f * Fbm(u, v, 10, 7f) * (country >= 0 ? 1.3f : 1f) : 0f;
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        // --- normal map from height (Sobel-ish, wraps horizontally) ---
        var normal = new Color32[W * H]; const float strength = 6f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int xl = (x - 1 + W) % W, xr = (x + 1) % W, yd = Mathf.Max(y - 1, 0), yu = Mathf.Min(y + 1, H - 1);
                float dx = (height[y * W + xr] - height[y * W + xl]) * strength, dy = (height[yu * W + x] - height[yd * W + x]) * strength;
                Vector3 n3 = new Vector3(-dx, -dy, 1f).normalized;
                normal[y * W + x] = new Color32((byte)((n3.x * 0.5f + 0.5f) * 255), (byte)((n3.y * 0.5f + 0.5f) * 255), (byte)((n3.z * 0.5f + 0.5f) * 255), 255);
            }

        // --- clouds ---
        int CW = 1024, CH = 512; var clouds = new Color32[CW * CH];
        for (int y = 0; y < CH; y++)
            for (int x = 0; x < CW; x++)
            {
                float a = Mathf.SmoothStep(0f, 1f, (Fbm((x + 0.5f) / CW, (y + 0.5f) / CH, 4, 11f) - 0.5f) * 4f);
                clouds[y * CW + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }

        var tAlbedo = WritePng($"{Root}/Textures/EarthAlbedo_Placeholder.png", albedo, W, H, i => { i.wrapModeU = TextureWrapMode.Repeat; i.wrapModeV = TextureWrapMode.Clamp; });
        var tNormal = WritePng($"{Root}/Textures/EarthNormal_Placeholder.png", normal, W, H, i => { i.textureType = TextureImporterType.NormalMap; i.wrapModeU = TextureWrapMode.Repeat; i.wrapModeV = TextureWrapMode.Clamp; });
        var tID = WritePng($"{Root}/Textures/CountryIDMap.png", idmap, W, H, i =>
        {
            i.isReadable = true; i.sRGBTexture = false; i.mipmapEnabled = false; i.filterMode = FilterMode.Point;
            i.textureCompression = TextureImporterCompression.Uncompressed; i.wrapMode = TextureWrapMode.Repeat; i.npotScale = TextureImporterNPOTScale.None;
        });
        var tClouds = WritePng($"{Root}/Textures/Clouds.png", clouds, CW, CH, i => { i.wrapModeU = TextureWrapMode.Repeat; i.wrapModeV = TextureWrapMode.Clamp; i.alphaIsTransparency = true; });

        // --- flags + CountryData assets ---
        var db = ScriptableObject.CreateInstance<CountryDatabase>();
        foreach (var s in specs)
        {
            string safe = s.name.Replace(" ", "");
            var sprite = WriteFlag($"{Root}/Flags/Flag_{safe}.png", s);
            var cd = ScriptableObject.CreateInstance<CountryData>();
            cd.countryName = s.name; cd.idColor = s.id; cd.flag = sprite; cd.difficulty = s.diff;
            cd.clueTerrain = s.c1; cd.clueLocation = s.c2; cd.clueCulture = s.c3;
            cd.decoys = new List<string>(s.decoys.Split('|'));
            AssetDatabase.CreateAsset(cd, $"{Root}/Data/{safe}.asset");
            db.countries.Add(cd);
        }
        AssetDatabase.CreateAsset(db, $"{Root}/Data/CountryDatabase.asset");

        // --- materials ---
        var globe = new Material(mapShader);
        globe.SetTexture("_BaseMap", tAlbedo); globe.SetTexture("_BumpMap", tNormal); globe.SetTexture("_IDMap", tID);
        globe.SetVector("_IDMapSize", new Vector4(W, H, 0, 0));
        AssetDatabase.CreateAsset(globe, $"{Root}/Materials/GlobeMat.mat");

        var cloudMat = new Material(Shader.Find("Geoguess/Clouds")); cloudMat.SetTexture("_MainTex", tClouds);
        AssetDatabase.CreateAsset(cloudMat, $"{Root}/Materials/CloudsMat.mat");
        AssetDatabase.CreateAsset(new Material(Shader.Find("Geoguess/Atmosphere")), $"{Root}/Materials/AtmosphereMat.mat");
        AssetDatabase.CreateAsset(new Material(Shader.Find("Geoguess/StarSkybox")), $"{Root}/Materials/StarSkyboxMat.mat");

        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Selection.activeObject = db;
        Debug.Log($"Geoguess: generated {specs.Count} countries, textures and materials in {Root}");
    }

    static Texture2D WritePng(string path, Color32[] px, int w, int h, System.Action<TextureImporter> setup)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels32(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        setup(imp); imp.maxTextureSize = 4096; imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Sprite WriteFlag(string path, Spec s)
    {
        const int fw = 192, fh = 128; var px = new Color32[fw * fh];
        for (int y = 0; y < fh; y++)
            for (int x = 0; x < fw; x++)
            {
                float u = x / (float)fw, v = y / (float)fh; Color32 c = s.f1;
                switch (s.flag)
                {
                    case Flag.HStripes: c = v < 0.333f ? s.f1 : (v < 0.666f ? s.f2 : s.f3); break;
                    case Flag.VStripes: c = u < 0.333f ? s.f1 : (u < 0.666f ? s.f2 : s.f3); break;
                    case Flag.NordicCross: if (Mathf.Abs(u - 0.35f) < 0.07f || Mathf.Abs(v - 0.5f) < 0.10f) c = s.f2; break;
                    case Flag.Disc: if (Vector2.Distance(new Vector2(x, y), new Vector2(fw / 2f, fh / 2f)) < fh * 0.3f) c = s.f2; break;
                    case Flag.Diagonal: if (Mathf.Abs(v - u) < 0.12f) c = s.f2; break;
                    case Flag.Crescent:
                    {
                        var p = new Vector2(x, y);
                        bool moon = Vector2.Distance(p, new Vector2(fw * 0.38f, fh * 0.5f)) < fh * 0.28f
                                 && Vector2.Distance(p, new Vector2(fw * 0.46f, fh * 0.5f)) >= fh * 0.24f;
                        bool star = false;
                        for (int k = 0; k < 3; k++) star |= Vector2.Distance(p, new Vector2(fw * 0.72f, fh * (0.25f + 0.25f * k))) < fh * 0.06f;
                        if (moon || star) c = s.f2; break;
                    }
                }
                px[y * fw + x] = c;
            }
        WritePng(path, px, fw, fh, i => { i.textureType = TextureImporterType.Sprite; i.mipmapEnabled = false; });
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
