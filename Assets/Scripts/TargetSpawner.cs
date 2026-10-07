using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    public Target targetPrefab;
    public Material[] materials;
    public float[] rowDistances = { 4f, 6f, 8f };
    public int perRow = 4;
    public float arcDegrees = 90f;
    public float respawnDelay = 3f;

    readonly List<Target> alive = new List<Target>();

    public int Remaining => alive.Count;
    public int Total => rowDistances.Length * perRow;
    public int Wave { get; private set; }

    public event Action<int, int> TargetsChanged;
    public event Action<int> WaveCleared;

    void Start()
    {
        Target[] placed = GetComponentsInChildren<Target>();
        if (placed.Length == 0)
        {
            SpawnWave();
            return;
        }
        Wave = 1;
        foreach (Target t in placed) Track(t);
        TargetsChanged?.Invoke(Remaining, Total);
    }

    public List<Pose> WaveLayout(int seed)
    {
        System.Random rng = new System.Random(seed);
        List<Pose> poses = new List<Pose>();
        float halfHeight = targetPrefab.transform.localScale.y * 0.5f;
        foreach (float dist in rowDistances)
        {
            for (int n = 0; n < perRow; n++)
            {
                float t = perRow == 1 ? 0.5f : (float)n / (perRow - 1);
                float jitter = (float)(rng.NextDouble() * 12.0 - 6.0);
                float angle = (Mathf.Lerp(-arcDegrees / 2f, arcDegrees / 2f, t) + jitter) * Mathf.Deg2Rad;
                Vector3 pos = transform.position + new Vector3(Mathf.Sin(angle) * dist, halfHeight, Mathf.Cos(angle) * dist);
                poses.Add(new Pose(pos, Quaternion.Euler(0f, (float)(rng.NextDouble() * 90.0), 0f)));
            }
        }
        return poses;
    }

    public Target Place(Pose pose, int index)
    {
        Target target = Instantiate(targetPrefab, pose.position, pose.rotation, transform);
        if (materials != null && materials.Length > 0)
            target.GetComponent<Renderer>().sharedMaterial = materials[index % materials.Length];
        return target;
    }

    public void SpawnWave()
    {
        Wave++;
        List<Pose> layout = WaveLayout(Environment.TickCount + Wave);
        for (int i = 0; i < layout.Count; i++) Track(Place(layout[i], i));
        TargetsChanged?.Invoke(Remaining, Total);
    }

    void Track(Target target)
    {
        target.Destroyed += OnTargetDestroyed;
        alive.Add(target);
    }

    void OnTargetDestroyed(Target target)
    {
        alive.Remove(target);
        TargetsChanged?.Invoke(Remaining, Total);
        if (alive.Count == 0)
        {
            WaveCleared?.Invoke(Wave);
            StartCoroutine(RespawnLater());
        }
    }

    IEnumerator RespawnLater()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnWave();
    }
}
