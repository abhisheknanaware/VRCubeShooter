using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class DesktopModeTests
{
    [UnityTest]
    public IEnumerator WithoutHeadsetDesktopModeLetsYouAimAndShoot()
    {
        LogAssert.ignoreFailingMessages = true;
        yield return SceneManager.LoadSceneAsync("VRShooter");
        yield return null;
        yield return null;

        DesktopMode desktop = Object.FindAnyObjectByType<DesktopMode>();
        Assert.IsNotNull(desktop, "Desktop mode should start when no headset is active");
        Assert.IsTrue(DesktopMode.Active);

        Gun gun = Object.FindAnyObjectByType<Gun>();
        Assert.IsNotNull(gun, "Gun should be active and visible");
        Camera cam = Camera.main;
        Assert.AreSame(cam.transform, gun.transform.parent, "Gun should be held by the camera");
        Assert.AreEqual(1.6f, cam.transform.position.y, 0.05f, "Camera should be at eye height");

        Target target = Object.FindAnyObjectByType<TargetSpawner>().GetComponentsInChildren<Target>()[0];
        cam.transform.LookAt(target.transform.position);
        Vector3 e = cam.transform.localEulerAngles;
        typeof(DesktopMode).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(desktop, e.y);
        typeof(DesktopMode).GetField("pitch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(desktop, e.x > 180f ? e.x - 360f : e.x);
        yield return null;

        Assert.AreSame(target, desktop.Shoot(), "Shooting at the crosshair should hit the cube");
        yield return null;
        Assert.AreEqual(1, ScoreManager.Instance.Score);

        for (int i = 0; i < 5; i++)
        {
            desktop.Shoot();
            yield return new WaitForSeconds(0.05f);
        }
        yield return new WaitForSeconds(0.3f);
        Assert.Less(Vector3.Distance(gun.transform.localPosition, desktop.gunOffset), 0.01f,
            "After recoil the gun must return to its hand position, not the camera");
    }
}
