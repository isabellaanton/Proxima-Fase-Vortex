using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pure view layer: owns all Canvas references, exposes events for button presses and simple
/// methods for GameManager to call. Contains no game rules.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject menuPanel;
    public GameObject hudPanel;
    public GameObject resultsPanel;
    public GameObject feedbackPanel;
    public GameObject answerPanel;
    public GameObject typedPanel;
    public GameObject findItPanel;

    [Header("Main menu")]
    public Button playButton;
    public Button modeButton;        // cycles Guess / Find-It
    public TMP_Text modeLabel;
    public Button answerModeButton;  // cycles Easy (multiple choice) / Hard (typed)
    public TMP_Text answerModeLabel;
    public Button quitButton;

    [Header("HUD")]
    public TMP_Text roundText;
    public TMP_Text scoreText;
    public TMP_Text timerText;
    public TMP_Text maxScoreText;

    [Header("Clues")]
    public TMP_Text[] clueTexts = new TMP_Text[3];
    public Button clueButton;
    public TMP_Text clueButtonLabel;

    [Header("Answers (Easy)")]
    public Button[] answerButtons = new Button[4];

    [Header("Typed answer (Hard)")]
    public TMP_InputField typedInput;
    public Button typedSubmitButton;

    [Header("Find-It prompt")]
    public TMP_Text findItName;
    public Image findItFlag;

    [Header("Feedback")]
    public TMP_Text feedbackTitle;
    public TMP_Text feedbackDetail;
    public Image feedbackFlag;
    public Button nextButton;

    [Header("Results")]
    public TMP_Text resultsScore;
    public TMP_Text resultsAccuracy;
    public TMP_Text resultsDetail;
    public Button playAgainButton;
    public Button menuButton;

    [Header("Colours")]
    public Color goodColor = new Color(0.55f, 0.95f, 0.6f);
    public Color badColor = new Color(0.95f, 0.5f, 0.5f);

    public event Action PlayClicked, ModeClicked, AnswerModeClicked, QuitClicked, ClueClicked, NextClicked, PlayAgainClicked, MenuClicked;
    public event Action<int> AnswerClicked;
    public event Action<string> TypedSubmitted;

    void Awake()
    {
        playButton.onClick.AddListener(() => PlayClicked?.Invoke());
        modeButton.onClick.AddListener(() => ModeClicked?.Invoke());
        answerModeButton.onClick.AddListener(() => AnswerModeClicked?.Invoke());
        quitButton.onClick.AddListener(() => QuitClicked?.Invoke());
        clueButton.onClick.AddListener(() => ClueClicked?.Invoke());
        nextButton.onClick.AddListener(() => NextClicked?.Invoke());
        playAgainButton.onClick.AddListener(() => PlayAgainClicked?.Invoke());
        menuButton.onClick.AddListener(() => MenuClicked?.Invoke());
        typedSubmitButton.onClick.AddListener(() => TypedSubmitted?.Invoke(typedInput.text));
        typedInput.onSubmit.AddListener(s => TypedSubmitted?.Invoke(s));
        for (int i = 0; i < answerButtons.Length; i++) { int idx = i; answerButtons[i].onClick.AddListener(() => AnswerClicked?.Invoke(idx)); }
    }

    // ---------------------------------------------------------------- screens
    public void HideFeedback() { feedbackPanel.SetActive(false); }
    public void ShowMenu() { menuPanel.SetActive(true); hudPanel.SetActive(false); resultsPanel.SetActive(false); feedbackPanel.SetActive(false); }
    public void ShowGame() { menuPanel.SetActive(false); hudPanel.SetActive(true); resultsPanel.SetActive(false); feedbackPanel.SetActive(false); }

    public void SetMenuLabels(string mode, string answer, bool answerToggleVisible)
    {
        modeLabel.text = mode; answerModeLabel.text = answer;
        answerModeButton.gameObject.SetActive(answerToggleVisible);
    }

    public void ShowResults(int score, int correct, int total, float avgClues)
    {
        hudPanel.SetActive(false); feedbackPanel.SetActive(false); resultsPanel.SetActive(true);
        resultsScore.text = $"Total score: {score}";
        resultsAccuracy.text = $"Accuracy: {Mathf.RoundToInt(100f * correct / Mathf.Max(1, total))}%  ({correct}/{total})";
        resultsDetail.text = $"Average clues used: {avgClues:0.0}";
    }

    // ---------------------------------------------------------------- HUD
    public void SetHUD(int round, int total, int score) { roundText.text = $"Round {round}/{total}"; scoreText.text = $"Score: {score}"; }

    public void SetTimer(float seconds)
    {
        seconds = Mathf.Max(0f, seconds);
        timerText.text = Mathf.CeilToInt(seconds).ToString();
        timerText.color = seconds <= 5f ? badColor : Color.white;
    }

    public void SetMaxScore(int max) { maxScoreText.text = $"Max points now: {max}"; }

    // ---------------------------------------------------------------- clues
    public void ResetClues() { foreach (var t in clueTexts) t.text = "— hidden —"; }
    public void ShowClue(int index, string text) { if (index >= 0 && index < clueTexts.Length) clueTexts[index].text = text; }
    public void SetClueButton(bool interactable, string label) { clueButton.interactable = interactable; clueButtonLabel.text = label; }

    // ---------------------------------------------------------------- answers
    public void SetupMultipleChoice(string[] options)
    {
        answerPanel.SetActive(true); typedPanel.SetActive(false);
        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool used = i < options.Length;
            answerButtons[i].gameObject.SetActive(used);
            if (!used) continue;
            answerButtons[i].GetComponentInChildren<TMP_Text>().text = options[i];
            answerButtons[i].image.color = Color.white;
        }
    }

    public void SetupTyped()
    {
        answerPanel.SetActive(false); typedPanel.SetActive(true);
        typedInput.text = "";
    }

    public void HideAnswers() { answerPanel.SetActive(false); typedPanel.SetActive(false); }

    public void SetAnswersInteractable(bool value)
    {
        foreach (var b in answerButtons) b.interactable = value;
        typedInput.interactable = value; typedSubmitButton.interactable = value;
        if (value && typedPanel.activeSelf) typedInput.ActivateInputField();
    }

    public void MarkAnswers(int chosen, int correct)
    {
        for (int i = 0; i < answerButtons.Length; i++)
            if (answerButtons[i].gameObject.activeSelf) answerButtons[i].image.color = i == correct ? goodColor : (i == chosen ? badColor : Color.white);
    }

    // ---------------------------------------------------------------- find-it
    public void SetupFindIt(string countryName, Sprite flag)
    {
        findItPanel.SetActive(true);
        findItName.text = $"Find: {countryName}";
        findItFlag.sprite = flag; findItFlag.enabled = flag != null;
    }
    public void HideFindIt() { findItPanel.SetActive(false); }

    // ---------------------------------------------------------------- feedback
    public void ShowFeedback(bool correct, bool timedOut, CountryData c, int points)
    {
        feedbackPanel.SetActive(true);
        feedbackTitle.text = correct ? "Correct!" : (timedOut ? "Time's up!" : "Not quite!");
        feedbackTitle.color = correct ? goodColor : badColor;
        feedbackDetail.text = correct ? $"{c.countryName}\n+{points} points" : $"It was {c.countryName}\n+0 points";
        feedbackFlag.sprite = c.flag; feedbackFlag.enabled = c.flag != null;
    }
}
