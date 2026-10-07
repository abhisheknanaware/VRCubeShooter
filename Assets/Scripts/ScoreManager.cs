using System;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public int Score { get; private set; }

    public event Action<int> ScoreChanged;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddPoint(int amount = 1)
    {
        Score += amount;
        ScoreChanged?.Invoke(Score);
    }
}
