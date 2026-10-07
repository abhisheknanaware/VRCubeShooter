using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

public class Gun : MonoBehaviour
{
    [Tooltip("Controller trigger action (XRI Right Interaction/Activate).")]
    public InputActionProperty fireAction;
    public Transform muzzle;
    public float range = 60f;
    public LayerMask hitMask = ~0;

    [Header("Effects")]
    public LineRenderer laserSight;
    public LineRenderer tracer;
    public ParticleSystem muzzleFlash;
    public AudioSource audioSource;
    public AudioClip shotClip;
    public float recoilDistance = 0.04f;

    HapticImpulsePlayer haptics;
    Vector3 restPosition;
    Coroutine recoilRoutine;

    public int ShotsFired { get; private set; }

    void Awake()
    {
        haptics = GetComponentInParent<HapticImpulsePlayer>();
        restPosition = transform.localPosition;
        if (tracer) tracer.enabled = false;
    }

    public void CaptureRestPose()
    {
        if (recoilRoutine != null) StopCoroutine(recoilRoutine);
        restPosition = transform.localPosition;
    }

    void OnEnable()
    {
        if (fireAction.action != null) fireAction.action.Enable();
    }

    void Update()
    {
        if (fireAction.action != null && fireAction.action.WasPressedThisFrame()) Fire();
#if UNITY_EDITOR
        if (!DesktopMode.Active && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) Fire();
#endif
        UpdateLaser();
    }

    void UpdateLaser()
    {
        if (!laserSight) return;
        Vector3 end = Physics.Raycast(muzzle.position, muzzle.forward, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore)
            ? hit.point
            : muzzle.position + muzzle.forward * range;
        laserSight.SetPosition(0, muzzle.position);
        laserSight.SetPosition(1, end);
    }

    public Target Fire()
    {
        ShotsFired++;
        Target target = null;
        Vector3 end = muzzle.position + muzzle.forward * range;

        if (Physics.Raycast(muzzle.position, muzzle.forward, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            target = hit.collider.GetComponentInParent<Target>();
            if (target) target.Hit();
        }

        if (muzzleFlash) muzzleFlash.Play();
        if (audioSource && shotClip) audioSource.PlayOneShot(shotClip);
        if (haptics) haptics.SendHapticImpulse(0.6f, 0.08f);
        if (tracer) StartCoroutine(ShowTracer(end));
        if (recoilRoutine != null) StopCoroutine(recoilRoutine);
        recoilRoutine = StartCoroutine(Recoil());
        return target;
    }

    IEnumerator ShowTracer(Vector3 end)
    {
        tracer.SetPosition(0, muzzle.position);
        tracer.SetPosition(1, end);
        tracer.enabled = true;
        yield return new WaitForSeconds(0.05f);
        tracer.enabled = false;
    }

    IEnumerator Recoil()
    {
        Vector3 kicked = restPosition - Vector3.forward * recoilDistance;
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            transform.localPosition = Vector3.Lerp(kicked, restPosition, t / 0.12f);
            yield return null;
        }
        transform.localPosition = restPosition;
    }
}
