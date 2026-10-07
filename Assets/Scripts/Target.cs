using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Target : MonoBehaviour
{
    public int points = 1;
    public ParticleSystem explosionPrefab;
    public AudioClip explodeClip;

    public event Action<Target> Destroyed;

    bool hit;

    void OnEnable()
    {
        if (Application.isPlaying) StartCoroutine(PopIn());
    }

    IEnumerator PopIn()
    {
        Vector3 full = transform.localScale;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            float k = t / 0.25f;
            transform.localScale = full * (1f + 0.2f * Mathf.Sin(k * Mathf.PI)) * k;
            yield return null;
        }
        transform.localScale = full;
    }

    public void Hit()
    {
        if (hit) return;
        hit = true;

        if (explosionPrefab)
        {
            ParticleSystem fx = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            var main = fx.main;
            Renderer r = GetComponent<Renderer>();
            if (r) main.startColor = r.sharedMaterial.color;
            fx.Play();
            Destroy(fx.gameObject, 2f);
        }
        if (explodeClip) AudioSource.PlayClipAtPoint(explodeClip, transform.position, 0.8f);
        if (ScoreManager.Instance) ScoreManager.Instance.AddPoint(points);

        Destroyed?.Invoke(this);
        Destroy(gameObject);
    }
}
