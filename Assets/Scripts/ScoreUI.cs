using System.Collections;
using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    public TMP_Text scoreText;
    public TMP_Text targetsText;
    public TMP_Text messageText;
    public TMP_Text boardScoreText;
    public TargetSpawner spawner;

    void Start()
    {
        if (messageText) messageText.gameObject.SetActive(false);
        if (ScoreManager.Instance) ScoreManager.Instance.ScoreChanged += OnScoreChanged;
        if (spawner)
        {
            spawner.TargetsChanged += OnTargetsChanged;
            spawner.WaveCleared += OnWaveCleared;
            OnTargetsChanged(spawner.Remaining, spawner.Total);
        }
        OnScoreChanged(ScoreManager.Instance ? ScoreManager.Instance.Score : 0);
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance) ScoreManager.Instance.ScoreChanged -= OnScoreChanged;
        if (spawner)
        {
            spawner.TargetsChanged -= OnTargetsChanged;
            spawner.WaveCleared -= OnWaveCleared;
        }
    }

    void OnScoreChanged(int score)
    {
        if (scoreText)
        {
            scoreText.text = $"Score: {score}";
            StartCoroutine(Pop(scoreText.transform));
        }
        if (boardScoreText) boardScoreText.text = $"SCORE\n<size=150%>{score}</size>";
    }

    void OnTargetsChanged(int remaining, int total)
    {
        if (targetsText) targetsText.text = $"Cubes left: {remaining} / {total}";
    }

    void OnWaveCleared(int wave)
    {
        if (messageText) StartCoroutine(ShowMessage($"WAVE {wave} CLEARED!\n<size=60%>New cubes incoming...</size>"));
    }

    IEnumerator ShowMessage(string text)
    {
        messageText.text = text;
        messageText.gameObject.SetActive(true);
        yield return Pop(messageText.transform);
        yield return new WaitForSeconds(2.5f);
        messageText.gameObject.SetActive(false);
    }

    static IEnumerator Pop(Transform t)
    {
        for (float x = 0f; x < 0.2f; x += Time.deltaTime)
        {
            t.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(x / 0.2f * Mathf.PI));
            yield return null;
        }
        t.localScale = Vector3.one;
    }
}
