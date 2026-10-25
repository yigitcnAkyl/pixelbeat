#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmGame.Editor
{
    /// <summary>
    /// Unity sahnesinde tek tıkla 4 şeridi, 2.5D derinlik noktalarını,
    /// BoxCollider2D içeren Hit Zone objelerini ve tüm Cyberpunk sprite referanslarını
    /// otomatik inşa eden Editor aracı.
    /// </summary>
    public static class HighwayAutoBuilder
    {
        [MenuItem("PixelBeat/Build Highway", false, 1)]
        [MenuItem("Rhythm Game/⚡ Build Highway (Piano Tiles)", false, 2)]
        public static void BuildHighway()
        {
            Debug.Log("<color=#00e5ff><b>[HighwayAutoBuilder]</b> Highway ve 4 şeritli Piano Tiles sistemi inşa ediliyor...</color>");

            // 1. Sprite ayarlarını Point Filter & Uncompressed olarak doğrula
            PixelAssetImporter.SetupAllSlicedSprites();

            // 2. Sahnede eski kalıntıları temizle
            HighwayVisuals.CleanLeftoverSceneObjects();

            // 3. Conductor oluştur veya bul
            Conductor conductor = Object.FindFirstObjectByType<Conductor>();
            if (conductor == null)
            {
                GameObject condObj = new GameObject("[Conductor]");
                conductor = condObj.AddComponent<Conductor>();
                Undo.RegisterCreatedObjectUndo(condObj, "Create Conductor");
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/rings-of-saturn.wav");
            AudioSource audio = conductor.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.playOnAwake = false;
                if (clip != null)
                {
                    audio.clip = clip;
                    audio.loop = true;
                    conductor.SetTrack(clip, 120f);
                }
            }

            // 4. RhythmField ana objesini hazırla
            LaneController laneController = Object.FindFirstObjectByType<LaneController>();
            GameObject fieldObj = laneController != null ? laneController.gameObject : null;
            if (fieldObj == null)
            {
                fieldObj = new GameObject("[RhythmField]");
                laneController = fieldObj.AddComponent<LaneController>();
                Undo.RegisterCreatedObjectUndo(fieldObj, "Create RhythmField");
            }

            // Temel bileşenleri bağla
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

            // 5. 2.5D Yol Zeminini Kur
            highway.SetupHighway();

            // 6. 4 Şeridi (-1.5, -0.5, 0.5, 1.5) ve 2.5D derinlik noktalarını inşa et
            Transform lanesParent = fieldObj.transform.Find("Lanes");
            if (lanesParent != null)
            {
                Object.DestroyImmediate(lanesParent.gameObject);
            }
            GameObject newLanesObj = new GameObject("Lanes");
            lanesParent = newLanesObj.transform;
            lanesParent.SetParent(fieldObj.transform);

            // Alt vuruş koordinatları ve üst doğma koordinatları
            float[] bottomX = new float[] { -2.70f, -0.91f, 0.92f, 2.70f };
            float[] topX = new float[] { -1.41f, -0.47f, 0.47f, 1.41f };
            float hitY = -2.50f;
            float spawnY = 3.50f;

            SerializedObject serializedLaneController = new SerializedObject(laneController);
            SerializedProperty lanesProp = serializedLaneController.FindProperty("lanes");
            lanesProp.arraySize = 4;

            string[] unpressedPaths = new string[]
            {
                "Assets/Sprites/Sliced/Gameplay/HitPad_0_Square.png",
                "Assets/Sprites/Sliced/Gameplay/HitPad_1_Triangle.png",
                "Assets/Sprites/Sliced/Gameplay/HitPad_2_Cross.png",
                "Assets/Sprites/Sliced/Gameplay/HitPad_3_Circle.png"
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject laneObj = new GameObject($"Lane_{i}");
                laneObj.transform.SetParent(lanesParent);

                // Spawn Point (Yukarıda)
                GameObject spObj = new GameObject("SpawnPoint");
                spObj.transform.SetParent(laneObj.transform);
                spObj.transform.position = new Vector3(topX[i], spawnY, 0f);

                // Hit Point (Aşağıda vuruş çizgisinde)
                GameObject hpObj = new GameObject("HitPoint");
                hpObj.transform.SetParent(laneObj.transform);
                hpObj.transform.position = new Vector3(bottomX[i], hitY, 0f);

                // Hit Zone objesi (BoxCollider2D ile vuruş alanı)
                GameObject hitZoneObj = new GameObject($"HitZone_{i}");
                hitZoneObj.transform.SetParent(hpObj.transform);
                hitZoneObj.transform.localPosition = Vector3.zero;

                BoxCollider2D collider = hitZoneObj.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(1.2f, 0.8f);

                // Serialized Properties eşitle
                SerializedProperty laneElement = lanesProp.GetArrayElementAtIndex(i);
                laneElement.FindPropertyRelative("laneIndex").intValue = i;
                laneElement.FindPropertyRelative("spawnPoint").objectReferenceValue = spObj.transform;
                laneElement.FindPropertyRelative("hitPoint").objectReferenceValue = hpObj.transform;
            }

            // 7. NotePrefab oluştur ve bağla
            string prefabDir = "Assets/Prefabs/Rhythm";
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);
            string prefabPath = $"{prefabDir}/NotePrefab.prefab";
            NoteObject notePrefab = CreateOrUpdateNotePrefab(prefabPath);
            serializedLaneController.FindProperty("notePrefab").objectReferenceValue = notePrefab;

            serializedLaneController.ApplyModifiedProperties();

            // 8. Kamerayı 2.5D Ortografik açıya ayarla
            HighwayVisuals.ConfigureCameraFor25DPixelRoad();

            // 9. Sahneyi kaydet
            EditorUtility.SetDirty(fieldObj);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=#00ff88><b>[HighwayAutoBuilder]</b> Highway, 4 Şerit, Hit Zone Collider'ları ve UI bağlantıları başarıyla kuruldu ve sahne kaydedildi!</color>");
        }

        private static NoteObject CreateOrUpdateNotePrefab(string path)
        {
            GameObject noteRoot = new GameObject("NotePrefab");
            NoteObject noteComp = noteRoot.AddComponent<NoteObject>();

            Material unlitMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteUnlit.mat");
            if (unlitMat == null)
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                                     ?? Shader.Find("Sprites/Default");
                unlitMat = new Material(unlitShader);
            }

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

            // Kafa (Head)
            GameObject headObj = new GameObject("Head");
            headObj.transform.SetParent(noteRoot.transform);
            headObj.transform.localPosition = Vector3.zero;
            SpriteRenderer headSr = headObj.AddComponent<SpriteRenderer>();
            headSr.sprite = notes[0];
            headSr.material = unlitMat;
            headSr.sortingOrder = 10;
            headObj.transform.localScale = Vector3.one * 1.2f; // Belirgin ve net boyut

            // Kuyruk (Tail)
            GameObject tailObj = new GameObject("Tail");
            tailObj.transform.SetParent(noteRoot.transform);
            tailObj.transform.localPosition = Vector3.zero;
            SpriteRenderer tailSr = tailObj.AddComponent<SpriteRenderer>();
            tailSr.sprite = notes[0];
            tailSr.material = unlitMat;
            tailSr.sortingOrder = 10;
            tailObj.transform.localScale = Vector3.one * 1.1f;

            // Hold Gövde
            GameObject bodyObj = new GameObject("HoldBodySprite");
            bodyObj.transform.SetParent(noteRoot.transform);
            bodyObj.transform.localPosition = Vector3.zero;
            SpriteRenderer bodySr = bodyObj.AddComponent<SpriteRenderer>();
            bodySr.sprite = hollows[0];
            bodySr.material = unlitMat;
            bodySr.sortingOrder = 8;
            bodySr.enabled = false;

            // Hold Halka
            GameObject ringObj = new GameObject("HoldGlowRing");
            ringObj.transform.SetParent(noteRoot.transform);
            ringObj.transform.localPosition = Vector3.zero;
            SpriteRenderer ringSr = ringObj.AddComponent<SpriteRenderer>();
            ringSr.sprite = holdRing;
            ringSr.material = unlitMat;
            ringSr.sortingOrder = 12;
            ringSr.enabled = false;

            // BoxCollider2D (Vuruş tespiti için opsiyonel fizik alanı)
            BoxCollider2D boxCol = noteRoot.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(1.1f, 0.6f);

            SerializedObject so = new SerializedObject(noteComp);
            so.FindProperty("headRenderer").objectReferenceValue = headSr;
            so.FindProperty("tailRenderer").objectReferenceValue = tailSr;
            so.FindProperty("holdBodySpriteRenderer").objectReferenceValue = bodySr;
            so.FindProperty("holdRingRenderer").objectReferenceValue = ringSr;
            so.FindProperty("holdRingSprite").objectReferenceValue = holdRing;
            so.FindProperty("defaultUnlitMaterial").objectReferenceValue = unlitMat;

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
