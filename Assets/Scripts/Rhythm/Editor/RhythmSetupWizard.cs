#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace RhythmGame.Editor
{
    /// <summary>
    /// Unity Editor Menüsü üzerinden tek tıkla 2.5D piksel yolunu, yol üstü basılmamış/basılmış butonları,
    /// yassı 2.5D düşen notaları, içi boş/parıldayan Hold barlarını ve ana menüyü kuran sihirbaz.
    /// </summary>
    public static class RhythmSetupWizard
    {
        [InitializeOnLoadMethod]
        private static void AutoRefreshSceneOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (SessionState.GetBool("PixelBeat_V3_AutoRefreshed", false)) return;
                if (Object.FindFirstObjectByType<LaneController>() != null)
                {
                    SessionState.SetBool("PixelBeat_V3_AutoRefreshed", true);
                    SetupPhase1Scene();
                }
            };
        }

        [MenuItem("Rhythm Game/⚡ Setup Rhythm Scene (Sahnede Tüm Sistemleri Kur)", false, 10)]
        public static void SetupPhase1Scene()
        {
            // 1. Sliced Sprite'ları Point Filter & Uncompressed olarak yapılandır
            PixelAssetImporter.SetupAllSlicedSprites();

            string prefabDir = "Assets/Prefabs/Rhythm";
            if (!Directory.Exists(prefabDir))
            {
                Directory.CreateDirectory(prefabDir);
                AssetDatabase.Refresh();
            }

            // 2. Nota Prefabını oluştur/yenile
            string prefabPath = $"{prefabDir}/NotePrefab.prefab";
            NoteObject notePrefab = CreateNotePrefab(prefabPath);

            // 3. Conductor oluştur
            Conductor conductor = Object.FindFirstObjectByType<Conductor>();
            if (conductor == null)
            {
                GameObject conductorObj = new GameObject("[Conductor]");
                conductor = conductorObj.AddComponent<Conductor>();
                Undo.RegisterCreatedObjectUndo(conductorObj, "Create Conductor");
            }

            AudioClip saturnClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/rings-of-saturn.wav");
            AudioSource condAudio = conductor.GetComponent<AudioSource>();
            if (condAudio != null)
            {
                condAudio.playOnAwake = false;
                if (saturnClip != null)
                {
                    condAudio.clip = saturnClip;
                    condAudio.loop = true;
                    conductor.SetTrack(saturnClip, 120f);
                }
            }

            // 4. Rhythm Field ve tüm yöneticileri oluştur
            LaneController laneController = Object.FindFirstObjectByType<LaneController>();
            GameObject fieldObj;
            if (laneController == null)
            {
                fieldObj = new GameObject("[RhythmField]");
                laneController = fieldObj.AddComponent<LaneController>();
                Undo.RegisterCreatedObjectUndo(fieldObj, "Create RhythmField");
            }
            else
            {
                fieldObj = laneController.gameObject;
            }

            HighwayVisuals highway = fieldObj.GetComponent<HighwayVisuals>() ?? fieldObj.AddComponent<HighwayVisuals>();
            InputManager inputMgr = fieldObj.GetComponent<InputManager>() ?? fieldObj.AddComponent<InputManager>();
            HitDetector hitDetector = fieldObj.GetComponent<HitDetector>() ?? fieldObj.AddComponent<HitDetector>();
            ScoreManager scoreMgr = fieldObj.GetComponent<ScoreManager>() ?? fieldObj.AddComponent<ScoreManager>();
            GameUIManager uiMgr = fieldObj.GetComponent<GameUIManager>() ?? fieldObj.AddComponent<GameUIManager>();
            EffectManager effectMgr = fieldObj.GetComponent<EffectManager>() ?? fieldObj.AddComponent<EffectManager>();
            AudioVisualizer visualizer = fieldObj.GetComponent<AudioVisualizer>() ?? fieldObj.AddComponent<AudioVisualizer>();
            PixelArtSetup pixelSetup = fieldObj.GetComponent<PixelArtSetup>() ?? fieldObj.AddComponent<PixelArtSetup>();
            SongLoader songLoader = fieldObj.GetComponent<SongLoader>() ?? fieldObj.AddComponent<SongLoader>();
            BeatDetector beatDetector = fieldObj.GetComponent<BeatDetector>() ?? fieldObj.AddComponent<BeatDetector>();
            MainMenuManager menuMgr = fieldObj.GetComponent<MainMenuManager>() ?? fieldObj.AddComponent<MainMenuManager>();

            PixelArtSetup.OptimizeAllSpritesForPixelArt();

            highway.SetupHighway();

            SerializedObject serializedLaneController = new SerializedObject(laneController);
            serializedLaneController.FindProperty("notePrefab").objectReferenceValue = notePrefab;

            // 5. 4 Şeridin 2.5D Perspektifli Yol Koordinatlarını Kur
            Transform lanesParent = fieldObj.transform.Find("Lanes");
            if (lanesParent == null)
            {
                GameObject lanesObj = new GameObject("Lanes");
                lanesParent = lanesObj.transform;
                lanesParent.SetParent(fieldObj.transform);
            }

            SerializedProperty lanesProp = serializedLaneController.FindProperty("lanes");
            lanesProp.arraySize = 4;

            for (int i = 0; i < 4; i++)
            {
                Transform laneTransform = lanesParent.Find($"Lane_{i}");
                if (laneTransform == null)
                {
                    GameObject laneObj = new GameObject($"Lane_{i}");
                    laneTransform = laneObj.transform;
                    laneTransform.SetParent(lanesParent);
                }

                Transform spawnPoint = laneTransform.Find("SpawnPoint");
                if (spawnPoint == null)
                {
                    GameObject spObj = new GameObject("SpawnPoint");
                    spawnPoint = spObj.transform;
                    spawnPoint.SetParent(laneTransform);
                }
                spawnPoint.position = new Vector3(HighwayVisuals.TopLaneX[i], HighwayVisuals.TopLaneY, 0f);

                Transform hitPoint = laneTransform.Find("HitPoint");
                if (hitPoint == null)
                {
                    GameObject hpObj = new GameObject("HitPoint");
                    hitPoint = hpObj.transform;
                    hitPoint.SetParent(laneTransform);
                }
                hitPoint.position = new Vector3(HighwayVisuals.BottomLaneX[i], HighwayVisuals.BottomLaneY, 0f);

                SerializedProperty laneElement = lanesProp.GetArrayElementAtIndex(i);
                laneElement.FindPropertyRelative("laneIndex").intValue = i;
                laneElement.FindPropertyRelative("spawnPoint").objectReferenceValue = spawnPoint;
                laneElement.FindPropertyRelative("hitPoint").objectReferenceValue = hitPoint;
            }

            serializedLaneController.ApplyModifiedProperties();

            // 6. Kamerayı 2.5D Piksel Yol Görseline Tam Hizala
            HighwayVisuals.ConfigureCameraFor25DPixelRoad();

            Selection.activeGameObject = fieldObj;
            EditorUtility.SetDirty(fieldObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("<color=#00ffff><b>[Rhythm Game]</b> 2.5D Perspektif Notalar, İçi Boş/Parıldayan Hold Barları ve Tam Beat Senkronu Kuruldu ve Sahne Kaydedildi!</color>");
        }

        private static NoteObject CreateNotePrefab(string path)
        {
            GameObject noteRoot = new GameObject("NotePrefab");
            NoteObject noteComp = noteRoot.AddComponent<NoteObject>();

            Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                                 ?? Shader.Find("Sprites/Default");
            Material unlitMat = new Material(unlitShader);

            Sprite[] notes = new Sprite[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/Note_0_Square.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/Note_1_Triangle.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/Note_2_Cross.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/Note_3_Circle.png")
            };
            Sprite[] hollows = new Sprite[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldBody_Hollow_0.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldBody_Hollow_1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldBody_Hollow_2.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldBody_Hollow_3.png")
            };
            Sprite[] glowings = new Sprite[]
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldBody_Glowing_0.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldBody_Glowing_1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldBody_Glowing_2.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldBody_Glowing_3.png")
            };
            Sprite holdRing = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Sliced/Gameplay/HoldRing_Glow.png");

            // 1. Kafa (Head) Görseli
            GameObject headObj = new GameObject("Head");
            headObj.transform.SetParent(noteRoot.transform);
            headObj.transform.localPosition = Vector3.zero;
            SpriteRenderer headSr = headObj.AddComponent<SpriteRenderer>();
            headSr.sprite = notes[0];
            headSr.material = unlitMat;
            headSr.sortingOrder = 10;

            // 2. Kuyruk (Tail) Görseli
            GameObject tailObj = new GameObject("Tail");
            tailObj.transform.SetParent(noteRoot.transform);
            tailObj.transform.localPosition = Vector3.zero;
            SpriteRenderer tailSr = tailObj.AddComponent<SpriteRenderer>();
            tailSr.sprite = notes[0];
            tailSr.material = unlitMat;
            tailSr.sortingOrder = 10;

            // 3. Hold Gövde Sprite'ı (İçi boş gelir, basılı tutunca parıldar)
            GameObject bodyObj = new GameObject("HoldBodySprite");
            bodyObj.transform.SetParent(noteRoot.transform);
            bodyObj.transform.localPosition = Vector3.zero;
            SpriteRenderer bodySr = bodyObj.AddComponent<SpriteRenderer>();
            bodySr.sprite = hollows[0];
            bodySr.material = unlitMat;
            bodySr.sortingOrder = 8;
            bodySr.enabled = false;

            // 4. Hold Parlama Halkası (Basılı tutarken alt tuşun etrafında döner)
            GameObject ringObj = new GameObject("HoldGlowRing");
            ringObj.transform.SetParent(noteRoot.transform);
            ringObj.transform.localPosition = Vector3.zero;
            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = holdRing;
            ringSr.material = unlitMat;
            ringSr.sortingOrder = 12;
            ringSr.enabled = false;

            SerializedObject so = new SerializedObject(noteComp);
            so.FindProperty("headRenderer").objectReferenceValue = headSr;
            so.FindProperty("tailRenderer").objectReferenceValue = tailSr;
            so.FindProperty("holdBodySpriteRenderer").objectReferenceValue = bodySr;
            so.FindProperty("holdRingRenderer").objectReferenceValue = ringSr;
            so.FindProperty("holdRingSprite").objectReferenceValue = holdRing;

            SerializedProperty laneSpritesProp = so.FindProperty("laneSprites");
            SerializedProperty hollowProp = so.FindProperty("holdHollowSprites");
            SerializedProperty glowingProp = so.FindProperty("holdGlowingSprites");

            laneSpritesProp.arraySize = 4;
            hollowProp.arraySize = 4;
            glowingProp.arraySize = 4;

            for (int i = 0; i < 4; i++)
            {
                laneSpritesProp.GetArrayElementAtIndex(i).objectReferenceValue = notes[i];
                hollowProp.GetArrayElementAtIndex(i).objectReferenceValue = hollows[i];
                glowingProp.GetArrayElementAtIndex(i).objectReferenceValue = glowings[i];
            }

            so.ApplyModifiedProperties();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(noteRoot, path);
            Object.DestroyImmediate(noteRoot);

            AssetDatabase.SaveAssets();
            return prefab.GetComponent<NoteObject>();
        }
    }
}
#endif
