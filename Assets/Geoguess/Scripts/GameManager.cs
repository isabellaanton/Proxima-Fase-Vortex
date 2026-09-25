using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum GameState { Menu, Focusing, Playing, Feedback, Results }
public enum GameMode { GuessCountry, FindIt }
public enum AnswerMode { MultipleChoice, Typed }   // Easy / Hard

/// <summary>
/// Owns the round loop, state machine and scoring. Talks to the rest of the game only through
/// events and small public methods, so each system stays replaceable.
///
/// GUESS mode : country is highlighted + camera flies there -> clues -> pick / type the name.
/// FIND-IT mode: name + flag are shown -> player clicks the country on the globe.
/// Score per round = (maxPoints - pointsLostPerClue * cluesUsed) * lerp(minTimeFactor, 1, timeLeft/roundTime)
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("References")]
    public CountryDatabase database;
    public UIManager ui;
    public ClueSystem clues;
    public CountryHighlighter highlighter;
    public CameraFocus cameraFocus;
    public CountryPicker picker;
    public GlobeController globe;

    [Header("Rules")]
    public int roundsPerGame = 10;
    public float roundTime = 30f;
    public int maxPointsPerRound = 1000;
    public int pointsLostPerClue = 200;
    [Range(0f, 1f)] public float minTimeFactor = 0.5f;
    [Tooltip("Seconds before feedback auto-advances (Next button always works). 0 = manual only.")]
    public float feedbackAutoAdvance = 4f;
    public float findItOverviewDistance = 15f;
    public bool hoverOutlineInFindIt = true;

    [Header("Feedback colours (HDR)")]
    [ColorUsage(true, true)] public Color correctColor = new Color(0.3f, 1.6f, 0.5f);
    [ColorUsage(true, true)] public Color revealColor = new Color(1.8f, 1.2f, 0.2f);

    public GameState State { get; private set; } = GameState.Menu;
    public GameMode Mode = GameMode.GuessCountry;
    public AnswerMode Answer = AnswerMode.MultipleChoice;

    List<CountryData> rounds;
    CountryData current;
    int roundIndex, score, correctCount, totalClues, correctOption;
    float timeLeft, feedbackTimer;

    // ------------------------------------------------------------------ setup
    void Start()
    {
        ui.PlayClicked += StartGame;
        ui.ModeClicked += () => { Mode = Mode == GameMode.GuessCountry ? GameMode.FindIt : GameMode.GuessCountry; RefreshMenu(); };
        ui.AnswerModeClicked += () => { Answer = Answer == AnswerMode.MultipleChoice ? AnswerMode.Typed : AnswerMode.MultipleChoice; RefreshMenu(); };
        ui.QuitClicked += Quit;
        ui.ClueClicked += OnClueRequested;
        ui.AnswerClicked += OnAnswerChosen;
        ui.TypedSubmitted += OnTypedSubmitted;
        ui.NextClicked += NextRound;
        ui.PlayAgainClicked += StartGame;
        ui.MenuClicked += GoToMenu;
        picker.CountryClicked += OnGlobeClicked;
        picker.HoverChanged += c => { if (State == GameState.Playing && Mode == GameMode.FindIt && hoverOutlineInFindIt) highlighter.SetHover(c); else highlighter.ClearHover(); };
        clues.ClueRevealed += (i, t) => ui.ShowClue(i, t);
        GoToMenu();
    }

    void RefreshMenu() => ui.SetMenuLabels(
        Mode == GameMode.GuessCountry ? "Mode: Guess the Country" : "Mode: Find It on the Globe",
        Answer == AnswerMode.MultipleChoice ? "Answers: Multiple Choice (Easy)" : "Answers: Type It (Hard)",
        Mode == GameMode.GuessCountry);

    void GoToMenu()
    {
        State = GameState.Menu;
        highlighter.Clear();
        picker.PickingEnabled = false;
        globe.autoRotate = true; globe.InputLocked = false;
        ui.ShowMenu(); RefreshMenu();
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------ rounds
    void StartGame()
    {
        rounds = BuildRoundList();
        roundIndex = score = correctCount = totalClues = 0;
        globe.autoRotate = false;
        ui.ShowGame();
        BeginRound();
    }

    List<CountryData> BuildRoundList()
    {
        var bag = new List<CountryData>(database.countries);
        bag.RemoveAll(c => c == null);
        var result = new List<CountryData>();
        // If there are fewer countries than rounds, reshuffle and continue (repeats happen only after all were used).
        while (result.Count < roundsPerGame)
        {
            Shuffle(bag);
            foreach (var c in bag) { if (result.Count >= roundsPerGame) break; result.Add(c); }
        }
        return result;
    }

    void BeginRound()
    {
        current = rounds[roundIndex];
        clues.Begin(current);
        highlighter.Clear();
        State = GameState.Focusing;
        picker.PickingEnabled = false;
        timeLeft = roundTime;

        ui.HideFeedback();
        ui.SetHUD(roundIndex + 1, rounds.Count, score);
        ui.SetTimer(timeLeft);
        ui.ResetClues();
        RefreshClueUI();

        if (Mode == GameMode.GuessCountry)
        {
            highlighter.Highlight(current);                       // glowing outline + pulse
            ui.HideFindIt();
            if (Answer == AnswerMode.MultipleChoice) { var opts = BuildOptions(current); ui.SetupMultipleChoice(opts); }
            else ui.SetupTyped();
            ui.SetAnswersInteractable(false);                     // locked until the camera arrives
            cameraFocus.FocusOn(current, () =>
            {
                if (State != GameState.Focusing) return;
                State = GameState.Playing; ui.SetAnswersInteractable(true);
            });
        }
        else
        {
            ui.HideAnswers();
            ui.SetupFindIt(current.countryName, current.flag);
            cameraFocus.FocusOnLatLon(globe.Lat, globe.Lon, findItOverviewDistance, () =>
            {
                if (State != GameState.Focusing) return;
                State = GameState.Playing; picker.PickingEnabled = true;
            });
        }
    }

    void Update()
    {
        if (State == GameState.Playing)
        {
            timeLeft -= Time.deltaTime;
            ui.SetTimer(timeLeft);
            if (timeLeft <= 0f) EndRound(false, true);
        }
        else if (State == GameState.Feedback && feedbackAutoAdvance > 0f)
        {
            feedbackTimer -= Time.deltaTime;
            if (feedbackTimer <= 0f) NextRound();
        }
    }

    // ------------------------------------------------------------------ player actions
    void OnClueRequested()
    {
        if (State != GameState.Playing) return;
        if (clues.RevealNext(out _, out _)) RefreshClueUI();
    }

    void OnAnswerChosen(int index)
    {
        if (State != GameState.Playing || Mode != GameMode.GuessCountry || Answer != AnswerMode.MultipleChoice) return;
        ui.MarkAnswers(index, correctOption);
        EndRound(index == correctOption, false);
    }

    void OnTypedSubmitted(string text)
    {
        if (State != GameState.Playing || Mode != GameMode.GuessCountry || Answer != AnswerMode.Typed) return;
        EndRound(Matches(text, current.countryName), false);
    }

    // Find-It: click on ocean / real land is ignored (treated as a miss-click); clicking a fictional country is final.
    void OnGlobeClicked(CountryData c)
    {
        if (State != GameState.Playing || Mode != GameMode.FindIt || c == null) return;
        EndRound(c == current, false);
    }

    // ------------------------------------------------------------------ round end / scoring
    int CurrentMaxScore => Mathf.Max(0, maxPointsPerRound - pointsLostPerClue * clues.Revealed);

    int ComputeScore()
    {
        float timeFrac = Mathf.Clamp01(timeLeft / roundTime);
        return Mathf.RoundToInt(CurrentMaxScore * Mathf.Lerp(minTimeFactor, 1f, timeFrac));
    }

    void EndRound(bool correct, bool timedOut)
    {
        if (State != GameState.Playing) return;
        State = GameState.Feedback;
        picker.PickingEnabled = false;
        ui.SetAnswersInteractable(false);

        int points = correct ? ComputeScore() : 0;
        score += points; totalClues += clues.Revealed;
        if (correct) correctCount++;

        highlighter.ClearHover();
        highlighter.Highlight(current, correct ? correctColor : revealColor);
        cameraFocus.FocusOn(current);                              // in Find-It mode this reveals the answer
        ui.SetHUD(roundIndex + 1, rounds.Count, score);
        ui.ShowFeedback(correct, timedOut, current, points);
        feedbackTimer = feedbackAutoAdvance;
    }

    void NextRound()
    {
        if (State != GameState.Feedback) return;
        roundIndex++;
        if (roundIndex >= rounds.Count)
        {
            State = GameState.Results;
            highlighter.Clear();
            ui.ShowResults(score, correctCount, rounds.Count, totalClues / (float)rounds.Count);
        }
        else BeginRound();
    }

    void RefreshClueUI()
    {
        ui.SetClueButton(clues.CanReveal, clues.CanReveal ? $"Reveal clue {clues.Revealed + 1}  (-{pointsLostPerClue} pts)" : "No more clues");
        ui.SetMaxScore(CurrentMaxScore);
    }

    // ------------------------------------------------------------------ helpers
    string[] BuildOptions(CountryData c)
    {
        var wrong = new List<string>();
        foreach (var d in c.decoys) if (!string.IsNullOrWhiteSpace(d) && d != c.countryName && !wrong.Contains(d)) wrong.Add(d);
        Shuffle(wrong);
        if (wrong.Count > 3) wrong.RemoveRange(3, wrong.Count - 3);

        if (wrong.Count < 3)   // top up with other fictional countries' names
        {
            var others = new List<string>();
            foreach (var o in database.countries) if (o != null && o != c && !wrong.Contains(o.countryName)) others.Add(o.countryName);
            Shuffle(others);
            for (int i = 0; i < others.Count && wrong.Count < 3; i++) wrong.Add(others[i]);
        }
        wrong.Add(c.countryName);
        Shuffle(wrong);
        correctOption = wrong.IndexOf(c.countryName);
        return wrong.ToArray();
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }

    // Typed answers: case/punctuation-insensitive, with a 1-typo allowance for names of 7+ letters.
    static string Normalize(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in (s ?? "").ToLowerInvariant()) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    static bool Matches(string guess, string answer)
    {
        string g = Normalize(guess), a = Normalize(answer);
        if (g.Length == 0) return false;
        if (g == a) return true;
        return a.Length >= 7 && Levenshtein(g, a) <= 1;
    }

    static int Levenshtein(string s, string t)
    {
        var prev = new int[t.Length + 1]; var cur = new int[t.Length + 1];
        for (int j = 0; j <= t.Length; j++) prev[j] = j;
        for (int i = 1; i <= s.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= t.Length; j++)
                cur[j] = Mathf.Min(Mathf.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (s[i - 1] == t[j - 1] ? 0 : 1));
            (prev, cur) = (cur, prev);
        }
        return prev[t.Length];
    }
}
