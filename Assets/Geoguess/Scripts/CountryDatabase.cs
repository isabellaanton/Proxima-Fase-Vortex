using System.Collections.Generic;
using UnityEngine;

/// <summary>List of all playable countries. Drag new CountryData assets in here.</summary>
[CreateAssetMenu(fileName = "CountryDatabase", menuName = "Geoguess/Country Database")]
public class CountryDatabase : ScriptableObject
{
    public List<CountryData> countries = new List<CountryData>();

    /// <summary>Finds the country whose idColor is within 'tolerance' (per channel) of c.</summary>
    public CountryData FindByColor(Color32 c, int tolerance = 2)
    {
        CountryData best = null; int bestD = int.MaxValue;
        foreach (var k in countries)
        {
            if (k == null) continue;
            int d = Mathf.Max(Mathf.Abs(k.idColor.r - c.r), Mathf.Abs(k.idColor.g - c.g), Mathf.Abs(k.idColor.b - c.b));
            if (d <= tolerance && d < bestD) { best = k; bestD = d; }
        }
        return best;
    }
}
