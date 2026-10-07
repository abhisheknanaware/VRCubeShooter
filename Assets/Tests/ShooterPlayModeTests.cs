using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ShooterPlayModeTests
{
    Gun gun;
    TargetSpawner spawner;
    ScoreUI ui;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        LogAssert.ignoreFailingMessages = true;
        yield return SceneManager.LoadSceneAsync("VRShooter");
        yield return null;
        yield return null;
        gun = Object.FindAnyObjectByType<Gun>(FindObjectsInactive.Include);
        spawner = Object.FindAnyObjectByType<TargetSpawner>();
        ui = Object.FindAnyObjectByType<ScoreUI>();

        // No headset in the test runner, so the XR modality manager leaves the controller disabled.
        for (Transform t = gun.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
        yield return null;
    }

    Target AimAtNextTarget()
    {
        Target target = spawner.GetComponentsInChildren<Target>().FirstOrDefault();
        if (target) gun.muzzle.rotation = Quaternion.LookRotation(target.transform.position - gun.muzzle.position);
        return target;
    }

    [UnityTest]
    public IEnumerator SceneStartsWithTwelveCubesAndZeroScore()
    {
        Assert.IsNotNull(gun, "Gun missing");
        Assert.IsNotNull(spawner, "Spawner missing");
        Assert.AreEqual(12, spawner.Remaining);
        Assert.AreEqual(0, ScoreManager.Instance.Score);
        Assert.AreEqual("Score: 0", ui.scoreText.text);
        Assert.AreEqual("Cubes left: 12 / 12", ui.targetsText.text);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TriggerShotDestroysCubeAndAddsPoint()
    {
        Target target = AimAtNextTarget();
        Target hit = gun.Fire();
        Assert.AreSame(target, hit, "Shot should hit the aimed cube");
        yield return null;

        Assert.IsTrue(target == null, "Cube should be destroyed");
        Assert.AreEqual(1, ScoreManager.Instance.Score);
        Assert.AreEqual(11, spawner.Remaining);
        Assert.AreEqual("Score: 1", ui.scoreText.text);
        Assert.AreEqual("Cubes left: 11 / 12", ui.targetsText.text);
    }

    [UnityTest]
    public IEnumerator MissDoesNotScore()
    {
        gun.muzzle.rotation = Quaternion.LookRotation(Vector3.up);
        Assert.IsNull(gun.Fire());
        yield return null;
        Assert.AreEqual(0, ScoreManager.Instance.Score);
        Assert.AreEqual(12, spawner.Remaining);
    }

    [UnityTest]
    public IEnumerator ClearingAllCubesSpawnsNewWave()
    {
        for (int i = 0; i < 12; i++)
        {
            Assert.IsNotNull(AimAtNextTarget(), $"Target {i} missing");
            Assert.IsNotNull(gun.Fire(), $"Shot {i} missed");
            yield return null;
        }
        Assert.AreEqual(12, ScoreManager.Instance.Score);
        Assert.AreEqual(0, spawner.Remaining);
        Assert.IsTrue(ui.messageText.gameObject.activeSelf, "Wave cleared message should show");

        yield return new WaitForSeconds(spawner.respawnDelay + 0.5f);
        Assert.AreEqual(12, spawner.Remaining, "New wave should spawn");
        Assert.AreEqual(2, spawner.Wave);
        Assert.AreEqual("Score: 12", ui.scoreText.text);
    }
}
