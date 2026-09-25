using UnityEngine;

/// <summary>
/// Drives the highlight parameters of the Globe material (Geoguess/GlobeURP shader).
/// The shader does the actual work per-pixel from the ID map: pulsing fill + glowing outline.
/// Two slots: "highlight" (round target / answer) and "hover" (mouse-over in Find-It mode).
/// </summary>
public class CountryHighlighter : MonoBehaviour
{
    public Renderer globeRenderer;
    [Tooltip("Same ID map as the picker (only used to tell the shader its pixel size).")]
    public Texture2D idMap;

    [Header("Colours (HDR values >1 glow with Bloom)")]
    [ColorUsage(true, true)] public Color selectColor = new Color(0.3f, 1.2f, 1.7f, 1f);
    [ColorUsage(true, true)] public Color hoverColor = new Color(1.6f, 1.4f, 0.4f, 1f);
    public float pulseSpeed = 4f;

    static readonly int IdMapSize = Shader.PropertyToID("_IDMapSize");
    static readonly int HighlightID = Shader.PropertyToID("_HighlightID");
    static readonly int HighlightColor = Shader.PropertyToID("_HighlightColor");
    static readonly int HoverID = Shader.PropertyToID("_HoverID");
    static readonly int HoverColor = Shader.PropertyToID("_HoverColor");
    static readonly int PulseSpeed = Shader.PropertyToID("_PulseSpeed");

    Material mat;

    void Awake()
    {
        mat = globeRenderer.material;   // instance, so we never modify the asset
        if (idMap != null) mat.SetVector(IdMapSize, new Vector4(idMap.width, idMap.height, 0, 0));
        mat.SetFloat(PulseSpeed, pulseSpeed);
        mat.SetColor(HoverColor, hoverColor);
        Clear();
    }

    public void Highlight(CountryData c) => Highlight(c, selectColor);

    public void Highlight(CountryData c, Color color)
    {
        if (c == null) { ClearHighlight(); return; }
        mat.SetVector(HighlightID, ToVec(c.idColor));
        mat.SetColor(HighlightColor, color);
    }

    public void SetHover(CountryData c)
    {
        if (c == null) ClearHover(); else mat.SetVector(HoverID, ToVec(c.idColor));
    }

    public void ClearHighlight() => mat.SetVector(HighlightID, Vector4.zero);
    public void ClearHover() => mat.SetVector(HoverID, Vector4.zero);
    public void Clear() { ClearHighlight(); ClearHover(); }

    // w = 1 means "slot active". Raw 0-1 channel values are compared against the point-sampled ID map.
    static Vector4 ToVec(Color32 c) => new Vector4(c.r / 255f, c.g / 255f, c.b / 255f, 1f);
}
