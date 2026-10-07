using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

public static class VRShooterBuilder
{
    const string ScenePath = "Assets/Scenes/VRShooter.unity";
    const string SamplesRoot = "Assets/Samples/XR Interaction Toolkit/3.6.1";
    const string RigPrefab = SamplesRoot + "/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    const string SimulatorPrefab = SamplesRoot + "/XR Interaction Simulator/XR Interaction Simulator.prefab";
    const string InputActionsPath = SamplesRoot + "/Starter Assets/XRI Default Input Actions.inputactions";

    static Material matFloor, matWall, matGunBody, matGunAccent, matBoard, matLaser, matTracer, matParticle, matDebris;
    static Material[] targetMats;
    static Texture2D softDot;

    // ---------------- Entry points ----------------

    [MenuItem("VR Shooter/1. Build Project Content")]
    public static void BuildAll()
    {
        try
        {
            ConfigureXR(BuildTargetGroup.Android, true);
            ConfigureXR(BuildTargetGroup.Standalone, false);
            ConfigurePlayer();
            CreateMaterials();
            ParticleSystem explosion = CreateExplosionPrefab();
            Target targetPrefab = CreateTargetPrefab(explosion);
            BuildScene(targetPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[VRShooter] Project content built successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError("[VRShooter] Build content failed: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    [MenuItem("VR Shooter/2. Build Quest APK")]
    public static void BuildAndroid() => Build(BuildTarget.Android, "Builds/Android/VRCubeShooter.apk");

    [MenuItem("VR Shooter/3. Build Windows (PC VR)")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/VRCubeShooter.exe");

    static void Build(BuildTarget target, string relativePath)
    {
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = target,
            options = BuildOptions.None
        });
        BuildSummary s = report.summary;
        Debug.Log($"[VRShooter] {target} build {s.result}: {s.totalSize / (1024f * 1024f):F1} MB, {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalTime}");
        if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }

    [MenuItem("VR Shooter/Play Game")]
    public static void OpenAndPlay()
    {
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("VR Shooter/4. Save Preview Screenshot")]
    public static void Screenshot()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        Camera cam = GameObject.FindObjectsByType<Camera>().First(c => c.CompareTag("MainCamera"));
        cam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cam.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        Transform right = GameObject.Find("Right Controller").transform;
        right.localPosition = new Vector3(0.18f, 1.38f, 0.32f);
        right.localRotation = Quaternion.Euler(4f, -6f, 0f);
        GameObject message = GameObject.Find("Message");
        if (message) message.SetActive(false);


        RenderTexture rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.aspect = 16f / 9f;
        for (int i = 0; i < 4; i++) cam.Render();
        RenderTexture.active = rt;
        Texture2D img = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        img.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        img.Apply();
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Screenshots");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "preview.png"), img.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        EditorSceneManager.OpenScene(ScenePath);
        Debug.Log("[VRShooter] Screenshot saved to " + dir);
    }

    // ---------------- XR + player settings ----------------

    static void ConfigureXR(BuildTargetGroup group, bool quest)
    {
        if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
        {
            EnsureFolder("Assets/XR");
            perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        }
        if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
        if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

        XRGeneralSettings settings = perTarget.SettingsForBuildTarget(group);
        settings.InitManagerOnStart = true;
        XRPackageMetadataStore.AssignLoader(settings.AssignedSettings, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
        EditorUtility.SetDirty(settings);

        FeatureHelpers.RefreshFeatures(group);
        OpenXRSettings openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        foreach (var feature in openXR.GetFeatures())
        {
            string n = feature.GetType().Name;
            if (n == "OculusTouchControllerProfile" || n == "MetaQuestTouchPlusControllerProfile" || (quest && n == "MetaQuestFeature"))
            {
                feature.enabled = true;
                EditorUtility.SetDirty(feature);
            }
        }
        EditorUtility.SetDirty(openXR);
    }

    static void ConfigurePlayer()
    {
        PlayerSettings.productName = "VR Cube Shooter";
        PlayerSettings.companyName = "College Project";
        NamedBuildTarget android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, "com.college.vrcubeshooter");
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        PlayerSettings.colorSpace = ColorSpace.Linear;
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
    }

    // ---------------- Assets ----------------

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    static Material Mat(string name, string shader, Color color, Action<Material> extra = null)
    {
        string path = $"Assets/Materials/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        Material m = new Material(Shader.Find(shader)) { name = name };
        m.SetColor("_BaseColor", color);
        extra?.Invoke(m);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Texture2D SaveTexture(string name, int size, Func<int, int, Color> pixel, bool repeat)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, true);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                t.SetPixel(x, y, pixel(x, y));
        t.Apply();
        string path = $"Assets/Materials/{name}.png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.alphaIsTransparency = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void MakeTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.One);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    static void CreateMaterials()
    {
        EnsureFolder("Assets/Materials");
        Texture2D grid = SaveTexture("FloorGrid", 256, (x, y) =>
        {
            bool line = x < 4 || y < 4 || x > 251 || y > 251;
            float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.06f;
            return line ? new Color(0.22f, 0.5f, 0.85f) : new Color(0.13f + n, 0.15f + n, 0.22f + n);
        }, true);
        softDot = SaveTexture("SoftDot", 64, (x, y) =>
        {
            float d = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f);
            return new Color(1f, 1f, 1f, d * d);
        }, false);

        const string lit = "Universal Render Pipeline/Lit";
        const string unlit = "Universal Render Pipeline/Unlit";
        matFloor = Mat("Floor", lit, Color.white, m =>
        {
            m.SetTexture("_BaseMap", grid);
            m.SetTextureScale("_BaseMap", new Vector2(15f, 15f));
            m.SetFloat("_Smoothness", 0.35f);
        });
        matWall = Mat("Wall", lit, new Color(0.3f, 0.33f, 0.42f));
        matBoard = Mat("Scoreboard", lit, new Color(0.06f, 0.07f, 0.12f));
        matGunBody = Mat("GunBody", lit, new Color(0.18f, 0.19f, 0.22f), m => { m.SetFloat("_Metallic", 0.8f); m.SetFloat("_Smoothness", 0.6f); });
        matGunAccent = Mat("GunAccent", lit, new Color(1f, 0.5f, 0.1f), m =>
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.05f) * 1.5f);
        });
        matLaser = Mat("Laser", unlit, new Color(1f, 0.1f, 0.1f, 0.6f), MakeTransparent);
        matTracer = Mat("Tracer", unlit, new Color(1f, 0.9f, 0.3f, 1f), MakeTransparent);
        matParticle = Mat("ParticleGlow", "Universal Render Pipeline/Particles/Unlit", Color.white, m =>
        {
            m.SetTexture("_BaseMap", softDot);
            MakeTransparent(m);
        });
        matDebris = Mat("Debris", "Universal Render Pipeline/Particles/Lit", Color.white);

        Color[] colors =
        {
            new Color(0.95f, 0.25f, 0.3f), new Color(1f, 0.75f, 0.15f), new Color(0.25f, 0.85f, 0.4f),
            new Color(0.25f, 0.6f, 1f), new Color(0.75f, 0.35f, 1f)
        };
        targetMats = colors.Select((c, i) => Mat($"Target_{i}", lit, c, m =>
        {
            m.SetFloat("_Smoothness", 0.55f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.25f);
        })).ToArray();
    }

    static ParticleSystem MakeParticles(GameObject go, Material mat, Color color, int burst, Vector2 life,
        Vector2 speed, Vector2 size, float gravity, float coneAngle)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
        var shape = ps.shape;
        shape.shapeType = coneAngle > 0f ? ParticleSystemShapeType.Cone : ParticleSystemShapeType.Sphere;
        shape.angle = coneAngle;
        shape.radius = coneAngle > 0f ? 0.005f : 0.2f;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        return ps;
    }

    static ParticleSystem CreateExplosionPrefab()
    {
        EnsureFolder("Assets/Prefabs");
        GameObject root = new GameObject("CubeExplosion");
        ParticleSystem debris = MakeParticles(root, matDebris, Color.white, 28, new Vector2(0.8f, 1.4f),
            new Vector2(2f, 5f), new Vector2(0.05f, 0.14f), 1.2f, 0f);
        var main = debris.main;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var col = debris.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.World;
        col.bounce = 0.35f;
        col.dampen = 0.3f;
        ParticleSystemRenderer r = root.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

        GameObject flash = new GameObject("Flash");
        flash.transform.SetParent(root.transform, false);
        MakeParticles(flash, matParticle, new Color(1f, 0.85f, 0.4f), 18, new Vector2(0.2f, 0.4f),
            new Vector2(1f, 3f), new Vector2(0.2f, 0.5f), 0f, 0f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/CubeExplosion.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<ParticleSystem>();
    }

    static Target CreateTargetPrefab(ParticleSystem explosion)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "TargetCube";
        cube.transform.localScale = Vector3.one * 0.5f;
        cube.GetComponent<Renderer>().sharedMaterial = targetMats[0];
        Target t = cube.AddComponent<Target>();
        t.explosionPrefab = explosion;
        t.explodeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/explode.wav");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cube, "Assets/Prefabs/TargetCube.prefab");
        UnityEngine.Object.DestroyImmediate(cube);
        return prefab.GetComponent<Target>();
    }

    // ---------------- Scene ----------------

    static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static LineRenderer Line(string name, Transform parent, Material mat, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = mat;
        lr.widthMultiplier = width;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    static void BuildGun(Transform rightController)
    {
        GameObject gunGo = new GameObject("Gun");
        Transform gun = gunGo.transform;
        gun.SetParent(rightController, false);
        gun.localPosition = new Vector3(0f, -0.02f, 0.03f);

        Part("Body", PrimitiveType.Cube, gun, new Vector3(0f, 0.01f, 0.05f), new Vector3(0.045f, 0.06f, 0.18f), Vector3.zero, matGunBody);
        Part("Barrel", PrimitiveType.Cylinder, gun, new Vector3(0f, 0.02f, 0.2f), new Vector3(0.024f, 0.07f, 0.024f), new Vector3(90f, 0f, 0f), matGunBody);
        Part("Grip", PrimitiveType.Cube, gun, new Vector3(0f, -0.045f, -0.01f), new Vector3(0.035f, 0.09f, 0.04f), new Vector3(-15f, 0f, 0f), matGunBody);
        Part("Accent", PrimitiveType.Cube, gun, new Vector3(0f, 0.042f, 0.05f), new Vector3(0.047f, 0.01f, 0.12f), Vector3.zero, matGunAccent);
        Part("Sight", PrimitiveType.Cube, gun, new Vector3(0f, 0.05f, 0.11f), new Vector3(0.01f, 0.015f, 0.01f), Vector3.zero, matGunAccent);

        Transform muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(gun, false);
        muzzle.localPosition = new Vector3(0f, 0.02f, 0.28f);

        ParticleSystem flash = MakeParticles(new GameObject("MuzzleFlash"), matParticle, new Color(1f, 0.8f, 0.3f), 14,
            new Vector2(0.05f, 0.1f), new Vector2(1.5f, 3f), new Vector2(0.03f, 0.07f), 0f, 12f);
        flash.transform.SetParent(muzzle, false);
        var fm = flash.main;
        fm.simulationSpace = ParticleSystemSimulationSpace.Local;

        AudioSource audio = gunGo.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.6f;

        Gun g = gunGo.AddComponent<Gun>();
        g.muzzle = muzzle;
        g.muzzleFlash = flash;
        g.audioSource = audio;
        g.shotClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/shot.wav");
        g.laserSight = Line("LaserSight", gun, matLaser, 0.004f);
        g.tracer = Line("Tracer", gun, matTracer, 0.012f);

        InputActionReference trigger = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath).OfType<InputActionReference>()
            .First(r => r.action.actionMap.name == "XRI Right Interaction" && r.action.name == "Activate");
        g.fireAction = new InputActionProperty(trigger);
    }

    static TMP_Text Text(Transform parent, string name, string value, float size, Color color, Vector2 pos, Vector2 box, Material mat = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = value;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        if (mat) t.fontSharedMaterial = mat;
        return t;
    }

    static Canvas WorldCanvas(string name, Transform parent, Vector3 localPos, Vector2 size, float scale)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.WorldSpace;
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.localPosition = localPos;
        rt.localScale = Vector3.one * scale;
        return c;
    }

    static void BuildScene(Target targetPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        foreach (Camera c in UnityEngine.Object.FindObjectsByType<Camera>())
            UnityEngine.Object.DestroyImmediate(c.gameObject);
        Light sun = UnityEngine.Object.FindAnyObjectByType<Light>();
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.75f);
        RenderSettings.ambientEquatorColor = new Color(0.4f, 0.42f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.24f);

        GameObject env = new GameObject("Environment");
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.SetParent(env.transform);
        floor.transform.localScale = new Vector3(3f, 1f, 3f);
        floor.GetComponent<Renderer>().sharedMaterial = matFloor;
        foreach (var (pos, scale) in new[]
        {
            (new Vector3(0f, 0.5f, 15f), new Vector3(30f, 1f, 0.4f)), (new Vector3(0f, 0.5f, -15f), new Vector3(30f, 1f, 0.4f)),
            (new Vector3(15f, 0.5f, 0f), new Vector3(0.4f, 1f, 30f)), (new Vector3(-15f, 0.5f, 0f), new Vector3(0.4f, 1f, 30f))
        })
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.SetParent(env.transform);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = matWall;
        }

        GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab));
        rig.transform.position = Vector3.zero;
        Transform rightController = rig.GetComponentsInChildren<Transform>(true).First(t => t.name == "Right Controller");
        foreach (Transform t in rightController.GetComponentsInChildren<Transform>(true))
            if (t.name.Contains("Near-Far")) t.gameObject.SetActive(false);
        BuildGun(rightController);
        Camera head = rig.GetComponentInChildren<Camera>(true);

        GameObject sim = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefab));
        sim.tag = "EditorOnly";

        new GameObject("ScoreManager").AddComponent<ScoreManager>();

        targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TargetCube.prefab").GetComponent<Target>();
        targetMats = Enumerable.Range(0, targetMats.Length)
            .Select(i => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Target_{i}.mat")).ToArray();
        TargetSpawner spawner = new GameObject("Targets").AddComponent<TargetSpawner>();
        spawner.targetPrefab = targetPrefab;
        spawner.materials = targetMats;
        var layout = spawner.WaveLayout(42);
        for (int i = 0; i < layout.Count; i++)
        {
            GameObject cube = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefab.gameObject, spawner.transform);
            cube.transform.SetPositionAndRotation(layout[i].position, layout[i].rotation);
            cube.GetComponent<Renderer>().sharedMaterial = targetMats[i % targetMats.Length];
        }

        Material overlay = new Material(TMP_Settings.defaultFontAsset.material) { name = "TMP_Overlay" };
        overlay.shader = Shader.Find("TextMeshPro/Distance Field Overlay");
        overlay.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        overlay.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.8f));
        overlay.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
        overlay.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
        overlay.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
        AssetDatabase.DeleteAsset("Assets/Materials/TMP_Overlay.mat");
        AssetDatabase.CreateAsset(overlay, "Assets/Materials/TMP_Overlay.mat");

        Canvas hud = WorldCanvas("ScoreHUD", head.transform, new Vector3(-0.36f, 0.27f, 1.2f), new Vector2(560f, 220f), 0.001f);
        ScoreUI ui = hud.gameObject.AddComponent<ScoreUI>();
        ui.spawner = spawner;
        ui.scoreText = Text(hud.transform, "ScoreText", "Score: 0", 96, new Color(1f, 0.85f, 0.25f), new Vector2(0f, 40f), new Vector2(560f, 120f), overlay);
        ui.targetsText = Text(hud.transform, "CubesLeft", "Cubes left: 12 / 12", 44, Color.white, new Vector2(0f, -50f), new Vector2(560f, 60f), overlay);
        ui.scoreText.alignment = TextAlignmentOptions.Left;
        ui.targetsText.alignment = TextAlignmentOptions.Left;

        Canvas msg = WorldCanvas("MessageHUD", head.transform, new Vector3(0f, -0.05f, 1.2f), new Vector2(1200f, 300f), 0.001f);
        ui.messageText = Text(msg.transform, "Message", "WAVE CLEARED!", 120, new Color(0.4f, 1f, 0.5f), Vector2.zero, new Vector2(1200f, 300f), overlay);

        GameObject board = new GameObject("Scoreboard");
        board.transform.position = new Vector3(0f, 0f, 11f);
        Part("Panel", PrimitiveType.Cube, board.transform, new Vector3(0f, 3.2f, 0.06f), new Vector3(4f, 2.2f, 0.1f), Vector3.zero, matBoard);
        Part("Frame", PrimitiveType.Cube, board.transform, new Vector3(0f, 3.2f, 0.08f), new Vector3(4.2f, 2.4f, 0.06f), Vector3.zero, matGunAccent);
        Part("PostL", PrimitiveType.Cube, board.transform, new Vector3(-1.6f, 1f, 0.1f), new Vector3(0.15f, 2.2f, 0.15f), Vector3.zero, matWall);
        Part("PostR", PrimitiveType.Cube, board.transform, new Vector3(1.6f, 1f, 0.1f), new Vector3(0.15f, 2.2f, 0.15f), Vector3.zero, matWall);
        Canvas boardCanvas = WorldCanvas("BoardCanvas", board.transform, new Vector3(0f, 3.2f, 0f), new Vector2(400f, 220f), 0.01f);
        ui.boardScoreText = Text(boardCanvas.transform, "BoardScore", "SCORE\n<size=150%>0</size>", 48, Color.white, Vector2.zero, new Vector2(400f, 220f));

        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
