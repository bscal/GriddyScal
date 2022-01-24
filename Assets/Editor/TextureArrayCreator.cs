using UnityEditor;
using UnityEngine;

namespace FluidGame
{
    public class TextureArrayCreator : ScriptableWizard
    {

        public Texture2D[] Textures;

        [MenuItem("Assets/Create/Texture Array")]
        static void CreateWizard()
        {
            ScriptableWizard.DisplayWizard<TextureArrayCreator>("Create Texture Array", "Create");
        }

        private void OnWizardCreate()
        {
            if (Textures.Length == 0) return;

            string path = EditorUtility.SaveFilePanelInProject("Save Texture Array", "Texture Array", "asset", "Save Texture Array");
            if (path.Length == 0) return;

            Texture2D tex = Textures[0];
            Texture2DArray tex2DArray = new Texture2DArray(tex.width, tex.height, Textures.Length, tex.format, tex.mipmapCount > 1);
            tex2DArray.anisoLevel = tex.anisoLevel;
            tex2DArray.filterMode = tex.filterMode;
            tex2DArray.wrapMode = tex.wrapMode;

            for (int i = 0; i < Textures.Length; i++)
            {
                for (int m = 0; m < tex.mipmapCount; m++)
                {
                    Graphics.CopyTexture(Textures[i], 0, m, tex2DArray, i, m);
                }
            }

            AssetDatabase.CreateAsset(tex2DArray, path);
            Debug.Log(path);
        }
    }

    public class TextureArrayCreatorTiled : ScriptableWizard
    {
        public Vector2Int TileSize;
        public Texture2D Textures;

        [MenuItem("Assets/Create/Texture Array Tiled")]
        static void CreateWizard()
        {
            ScriptableWizard.DisplayWizard<TextureArrayCreatorTiled>("Create Texture Tiled Array", "Create");
        }

        private void OnWizardCreate()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Texture Tiled Array", "Texture Tiled Array", "asset", "Save Texture Tiled Array");
            if (path.Length == 0) return;

            Texture2D tex = Textures;
            int xTiles = tex.width / TileSize.x;
            int yTiles = tex.height / TileSize.y;
            int totalTiles = xTiles * yTiles;

            Texture2DArray tex2DArray = new Texture2DArray(TileSize.x, TileSize.y, totalTiles, tex.format, tex.mipmapCount, false);
            tex2DArray.anisoLevel = tex.anisoLevel;
            tex2DArray.filterMode = tex.filterMode;
            tex2DArray.wrapMode = tex.wrapMode;

            int index = 0;
            for (int y = 0; y < tex.height; y += 16)
            {
                for (int x = 0; x < tex.width; x += 16)
                {
                    for (int m = 0; m < tex.mipmapCount; m++)
                    {
                        var pixels = tex.GetPixels(x, y, TileSize.x, TileSize.y);
                        tex2DArray.SetPixels(pixels, index, m);
                    }
                    index++;
                }
            }
            tex2DArray.Apply();
            AssetDatabase.CreateAsset(tex2DArray, path);
        }
    }
}