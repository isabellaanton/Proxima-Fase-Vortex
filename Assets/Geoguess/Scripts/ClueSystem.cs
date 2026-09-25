using System;
using UnityEngine;

/// <summary>Progressive clue reveal for the current round (max 3). Scoring is done by GameManager.</summary>
public class ClueSystem : MonoBehaviour
{
    public const int MaxClues = 3;
    public static readonly string[] Labels = { "Terrain & Climate", "Location", "Culture & Flag" };

    public int Revealed { get; private set; }
    public bool CanReveal => country != null && Revealed < MaxClues;
    public event Action<int, string> ClueRevealed;   // (index, text)

    CountryData country;

    public void Begin(CountryData c) { country = c; Revealed = 0; }

    /// <summary>Reveals the next clue. Returns false if none are left.</summary>
    public bool RevealNext(out int index, out string text)
    {
        index = Revealed; text = "";
        if (!CanReveal) return false;
        text = $"{Labels[index]}: {country.GetClue(index)}";
        Revealed++;
        ClueRevealed?.Invoke(index, text);
        return true;
    }
}
