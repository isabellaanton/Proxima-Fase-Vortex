using System.Collections.Generic;
using UnityEngine;

public enum CountryDifficulty { Easy, Medium, Hard }

/// <summary>
/// One fictional country. Create via Assets > Create > Geoguess > Country Data.
/// idColor MUST match the flat colour painted for this country in the ID map texture.
/// </summary>
[CreateAssetMenu(fileName = "NewCountry", menuName = "Geoguess/Country Data")]
public class CountryData : ScriptableObject
{
    public string countryName = "New Country";

    [Tooltip("Exact flat colour used in the ID map. Never pure black (black = 'no country').")]
    public Color32 idColor = new Color32(255, 0, 0, 255);

    public Sprite flag;

    [Header("Clues (progressively revealed)")]
    [TextArea(2, 4)] public string clueTerrain;   // Clue 1: climate & terrain
    [TextArea(2, 4)] public string clueLocation;  // Clue 2: neighbours / location
    [TextArea(2, 4)] public string clueCulture;   // Clue 3: culture / flag

    public CountryDifficulty difficulty = CountryDifficulty.Medium;

    [Tooltip("Wrong answers for multiple choice. 3+ recommended; missing ones are filled from other countries.")]
    public List<string> decoys = new List<string>();

    public int ColorKey => (idColor.r << 16) | (idColor.g << 8) | idColor.b;

    public string GetClue(int index)
    {
        switch (index) { case 0: return clueTerrain; case 1: return clueLocation; case 2: return clueCulture; default: return ""; }
    }
}
