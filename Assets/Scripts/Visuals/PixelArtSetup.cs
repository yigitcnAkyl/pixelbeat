using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RhythmGame
{
    /// <summary>
    /// Cyberpunk piksel grafiklerinin bulanıklaşmasını önleyen, Filter Mode: Point (no filter)
    /// ve Compression: None ayarlarını otomatik ve toplu olarak uygulayan piksel ayar yöneticisi.
    /// </summary>
    public class PixelArtSetup : MonoBehaviour
    {
        [Header("Piksel Render Ayarları")]
        [Tooltip("Piksel bozulmalarını önlemek için kamera tam sayı piksellerine kenetlensin mi")]
        [SerializeField] private bool snapToPixelGrid = true;

        [Header("Aktif Sprite Referansları")]
        public Sprite tapNoteSprite;
        public Sprite holdHeadSprite;
        public Sprite holdTailSprite;
        public Sprite hitPadSprite;

        private void Start()
        {
            ApplyPixelCameraSettings();
        }

        private void ApplyPixelCameraSettings()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                // Piksel sanatında antialiasing piksel netliğini bozabileceğinden kapatılır
                QualitySettings.antiAliasing = 0;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Assets/Sprites klasöründeki tüm görselleri tek tıkla piksel sanatı için kusursuz hale getirir.
        /// </summary>
        [MenuItem("Rhythm Game/🎨 Optimize All Sprites for Crisp Pixel Art (Point Filter & No Compression)", false, 20)]
        public static void OptimizeAllSpritesForPixelArt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new string[] { "Assets/Sprites" });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

                if (importer != null)
                {
                    bool modified = false;

                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        modified = true;
                    }

                    if (importer.filterMode != FilterMode.Point)
                    {
                        importer.filterMode = FilterMode.Point;
                        modified = true;
                    }

                    if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        modified = true;
                    }

                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        modified = true;
                    }

                    if (importer.alphaIsTransparency != true)
                    {
                        importer.alphaIsTransparency = true;
                        modified = true;
                    }

                    if (modified)
                    {
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }

            Debug.Log($"<color=#00ffff><b>[PixelArtSetup]</b> {count} adet piksel görseli Point Filter & No Compression olarak optimize edildi!</color>");
        }
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Assets/Sprites içerisine yeni bir resim atıldığında otomatik olarak piksel ayarlarına çeken postprocessor.
    /// </summary>
    public class PixelSpritePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Sprites"))
            {
                TextureImporter importer = (TextureImporter)assetImporter;
                importer.textureType = TextureImporterType.Sprite;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
            }
        }
    }
#endif
}
