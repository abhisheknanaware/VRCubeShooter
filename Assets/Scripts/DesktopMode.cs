using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

/// <summary>
/// Makes the game playable on a normal PC when no VR headset is running:
/// mouse look, WASD movement, click to shoot.
/// </summary>
public class DesktopMode : MonoBehaviour
{
    public float eyeHeight = 1.6f;
    public float moveSpeed = 3f;
    public float lookSensitivity = 0.12f;
    public float arenaHalfSize = 14f;
    public Vector3 gunOffset = new Vector3(0.18f, -0.17f, 0.42f);

    public static bool Active { get; private set; }

    Camera cam;
    Gun gun;
    Transform rig;
    float yaw;
    float pitch;
    TMP_Text help;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Active = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (XRSettings.isDeviceActive) return;
        if (FindAnyObjectByType<DesktopMode>() != null) return;
        if (FindAnyObjectByType<Gun>(FindObjectsInactive.Include) == null) return;
        new GameObject("Desktop Mode").AddComponent<DesktopMode>();
    }

    void OnDestroy()
    {
        Active = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        Active = true;
        cam = Camera.main;
        gun = FindAnyObjectByType<Gun>(FindObjectsInactive.Include);

        GameObject simulator = GameObject.Find("XR Interaction Simulator");
        if (simulator) simulator.SetActive(false);

        TrackedPoseDriver pose = cam.GetComponent<TrackedPoseDriver>();
        if (pose) pose.enabled = false;

        rig = cam.transform.root;
        cam.transform.localRotation = Quaternion.identity;
        Vector3 p = cam.transform.position;
        cam.transform.position = new Vector3(p.x, rig.position.y + eyeHeight, p.z);

        gun.gameObject.SetActive(false);
        gun.transform.SetParent(cam.transform, false);
        gun.transform.localPosition = gunOffset;
        gun.transform.localRotation = Quaternion.identity;
        gun.gameObject.SetActive(true);
        gun.CaptureRestPose();

        BuildOverlay();
    }

    void BuildOverlay()
    {
        Canvas canvas = new GameObject("Desktop Overlay").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        TMP_Text crosshair = CreateText(canvas.transform, "+", 22, Vector2.zero, new Vector2(0.5f, 0.5f));
        crosshair.color = new Color(1f, 1f, 1f, 0.9f);

        help = CreateText(canvas.transform,
            "Click to play  |  Mouse: aim  |  Left click / Space: shoot  |  WASD: move  |  Esc: release mouse",
            22, new Vector2(0f, 30f), new Vector2(0.5f, 0f));
    }

    static TMP_Text CreateText(Transform parent, string value, float size, Vector2 offset, Vector2 anchor)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(1400f, 80f);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = value;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        bool locked = Cursor.lockState == CursorLockMode.Locked;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Shoot();
        }
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame)) Shoot();
        if (help) help.alpha = locked ? 0.5f : 1f;

        if (locked && mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f);
        }
        cam.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);

        if (keyboard != null)
        {
            Vector2 input = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            Vector3 pos = rig.position + (forward * input.y + right * input.x) * moveSpeed * Time.deltaTime;
            pos.x = Mathf.Clamp(pos.x, -arenaHalfSize, arenaHalfSize);
            pos.z = Mathf.Clamp(pos.z, -arenaHalfSize, arenaHalfSize);
            rig.position = pos;
        }

        AimGunAtCrosshair();
    }

    void AimGunAtCrosshair()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 aimPoint = Physics.Raycast(ray, out RaycastHit hit, gun.range) ? hit.point : ray.GetPoint(gun.range);
        // The muzzle is offset from the gun's pivot, so refine a few times until it points exactly at the crosshair.
        for (int i = 0; i < 4; i++)
        {
            Quaternion fix = Quaternion.FromToRotation(gun.muzzle.forward, aimPoint - gun.muzzle.position);
            gun.transform.rotation = fix * gun.transform.rotation;
        }
    }

    public Target Shoot()
    {
        AimGunAtCrosshair();
        return gun.Fire();
    }
}
