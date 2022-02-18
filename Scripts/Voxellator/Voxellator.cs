using System.Collections.Generic;
using UnityEngine;
using System;

public static class Voxellator
{
    public static readonly string VOXEL_NAME_POSTFIX = "_voxelmesh";
    private const int CUBE_INDICE_COUNT = 24;

    /// <summary>
    /// Create and grab the voxellated mesh
    /// </summary>
    public static Mesh Voxellate(VoxelInfo voxelateInfo)
    {
        voxelateInfo.texture.filterMode = FilterMode.Point;

        if(voxelateInfo.texture.format != TextureFormat.RGBA32)
        {
            Debug.LogWarning("Wrong sprite format detected! Would be better to set it to RGBA32 from Import Settings!");
        }

        Mesh newMesh = new Mesh();
        Color32[] colorBuffer = voxelateInfo.texture.GetPixels32();

        int height = voxelateInfo.texture.height;
        int width = voxelateInfo.texture.width;
        
        GenerateVertices(ref newMesh, colorBuffer, height, width, voxelateInfo);
        GenerateNormals(ref newMesh);

        // Circumvent max vertex count issues in some cases...
        if(newMesh.vertexCount >= Int16.MaxValue)
        {
            newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        GenerateTriangles(ref newMesh, colorBuffer);

        if(voxelateInfo.applyColorPerVertex)
        {
            GenerateVertexColors(ref newMesh, colorBuffer);
        }

        return newMesh;
    }

    /// <summary>
    /// Generate a 24 vertices cube for every pixel in the texture...
    /// </summary>
    private static void GenerateVertices(ref Mesh mesh, IList<Color32> colorBuffer, int height, int width, VoxelInfo voxelateInfo)
    {
        if(!mesh || colorBuffer == null) { return; }

        // Reduce the scale by a tenth...
        float scale = voxelateInfo.scale / 10f;

        List<Vector3> meshVertices = new List<Vector3>(CUBE_INDICE_COUNT * height * width);

        float startPosX = -(width * scale / 2f);
        float startPosY = -(height * scale / 2f);

        for(int h = 0; h < height; h++)
        {
            float y = startPosY + (h * scale);

            for(int w = 0; w < width; w++)
            {
                // Skip alpha!
                if(colorBuffer[h * width + w].a == 0) { continue; }

                float x = startPosX + (w * scale);

                // Get the extrusion amount for this pixel...
                float extrusion = GetPixelExtrusionAmount(voxelateInfo, scale, w, h);

                Vector3[] cubeVerts = new Vector3[8];

                // Bottom...
                cubeVerts[0] = new Vector3(x, y, extrusion);
                cubeVerts[1] = new Vector3(x + scale, y, extrusion);
                cubeVerts[2] = new Vector3(x + scale, y, -extrusion);
                cubeVerts[3] = new Vector3(x, y, -extrusion);

                // Top...
                cubeVerts[4] = new Vector3(x, y + scale, extrusion);
                cubeVerts[5] = new Vector3(x + scale, y + scale, extrusion);
                cubeVerts[6] = new Vector3(x + scale, y + scale, -extrusion);
                cubeVerts[7] = new Vector3(x, y + scale, -extrusion);

                meshVertices.AddRange(new List<Vector3>
                {
                    cubeVerts[0], cubeVerts[1], cubeVerts[2], cubeVerts[3], // Bottom
                    cubeVerts[7], cubeVerts[4], cubeVerts[0], cubeVerts[3], // Left
                    cubeVerts[4], cubeVerts[5], cubeVerts[1], cubeVerts[0], // Front
                    cubeVerts[6], cubeVerts[7], cubeVerts[3], cubeVerts[2], // Back
                    cubeVerts[5], cubeVerts[6], cubeVerts[2], cubeVerts[1], // Right
                    cubeVerts[7], cubeVerts[6], cubeVerts[5], cubeVerts[4]  // Top
                });
            }
        }

        mesh.SetVertices(meshVertices);
    }

    /// <summary>
    /// Create and grab the voxel object...
    /// </summary>
    public static GameObject CreateVoxelObject(Mesh mesh, Texture2D texture, string spriteName, Material mat)
    {
        // Name the mesh...
        string meshName = spriteName + VOXEL_NAME_POSTFIX;
        mesh.name = meshName;

        GameObject voxelObject = new GameObject(meshName);

        MeshFilter meshFilter = voxelObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = voxelObject.AddComponent<MeshRenderer>();

        if(texture != null)
        {
            // Dupe the selected material (We don't want to change the texture of universal standard shaders!)
            Material newMat = new Material(mat);

            newMat.SetTexture("_MainTex", texture);
            meshRenderer.sharedMaterial = newMat;
        }

        return voxelObject;
    }

    /// <summary>
    /// Read texture independent of Read/Write enabled on the sprite
    /// </summary>
    public static Texture2D ReadTexture(Texture2D texture)
    {
        Texture2D newTexture = new Texture2D(texture.width, texture.height, texture.format, false);
        newTexture.LoadRawTextureData(texture.GetRawTextureData());
        newTexture.Apply();

        return newTexture;
    }

    /// <summary>
    /// Get the amount of extrusion for this pixel
    /// </summary>
    public static float GetPixelExtrusionAmount(VoxelInfo voxelateInfo, float scale, int w, int h)
    {
        float extrudeFactor = voxelateInfo.extrusionFactor * scale;

        // If we got an extrusion map, use that AND scale by extrusion factor...
        if(voxelateInfo.extrusionMapTexture != null)
        {
            float greyscaleValue = voxelateInfo.extrusionMapTexture.GetPixel(w, h).grayscale;
            greyscaleValue = 1.0f - greyscaleValue; // (Inverse the value so darker bits are more extruded instead of lighter colors...)

            extrudeFactor = greyscaleValue * voxelateInfo.extrusionFactor * scale;
        }

        return extrudeFactor;
    }

    public static Texture2D GetTextureFromSprite(Sprite sprite)
    {
        if(sprite.rect.width != sprite.texture.width)
        {
            int texRectX = (int)sprite.textureRect.x;
            int texRectY = (int)sprite.textureRect.y;
            int texRectWidth = (int)sprite.textureRect.width;
            int texRectHeight = (int)sprite.textureRect.height;

            Texture2D newText = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);

            Color[] newColors = sprite.texture.GetPixels(texRectX, texRectY, texRectWidth, texRectHeight);

            newText.SetPixels(newColors);
            newText.Apply();

            return newText;
        }
        else
        {
            return sprite.texture;
        }
    }

    private static void GenerateNormals(ref Mesh mesh)
    {
        if(!mesh || mesh.vertexCount <= 0) { return; }

        List<Vector3> normals = new List<Vector3>(mesh.vertexCount);

        Vector3 up = Vector3.up;
        Vector3 down = Vector3.down;
        Vector3 forward = Vector3.forward;
        Vector3 back = Vector3.back;
        Vector3 left = Vector3.left;
        Vector3 right = Vector3.right;

        for(int j = 0; j < mesh.vertexCount; j += CUBE_INDICE_COUNT)
        {
            normals.AddRange(new List<Vector3>
            {
                down, down, down, down,             // Bottom
                left, left, left, left,             // Left
                forward, forward, forward, forward,	// Front
                back, back, back, back,             // Back
                right, right, right, right,         // Right
                up, up, up, up	                    // Top
            });
        }

        mesh.SetNormals(normals);
    }

    private static void GenerateTriangles(ref Mesh mesh, IList<Color32> colorBuffer)
    {
        if(!mesh || colorBuffer == null) return;

        // Triangle values are vertices array indices!
        List<int> triangles = new List<int>(mesh.vertexCount);

        int i = 0;

        // Pixels are laid out left to right, and bottom to top...
        for(int j = 0; j < CUBE_INDICE_COUNT * colorBuffer.Count; j += CUBE_INDICE_COUNT)
        {
            // If it's not transparent...
            if(colorBuffer[j / CUBE_INDICE_COUNT].a != 0)
            {
                triangles.AddRange(new int[]
                {
                    // Bottom
                    i + 3, i + 1, i,
                    i + 3, i + 2, i + 1,

                    // Left     	
                    i + 7, i + 5, i + 4,
                    i + 7, i + 6, i + 5,

                    // Front
                    i + 11, i + 9, i + 8,
                    i + 11, i + 10, i + 9,

                    // Back
                    i + 15, i + 13, i + 12,
                    i + 15, i + 14, i + 13,

                    // Right
                    i + 19, i + 17, i + 16,
                    i + 19, i + 18, i + 17,

                    // Top
                    i + 23, i + 21, i + 20,
                    i + 23, i + 22, i + 21,
                });

                i += CUBE_INDICE_COUNT;
            }
        }

        mesh.SetTriangles(triangles, 0);
    }

    /// <summary>
    /// Assigns colors for each vertex
    /// </summary>
    private static void GenerateVertexColors(ref Mesh mesh, IList<Color32> colorBuffer)
    {
        if(!mesh || colorBuffer == null) { return; }

        List<Color32> vertexColors = new List<Color32>(CUBE_INDICE_COUNT * colorBuffer.Count);

        for(int i = 0; i < colorBuffer.Count; i++)
        {
            Color32 color = colorBuffer[i];

            if(color.a == 0) { continue; }

            for(int j = 0; j < CUBE_INDICE_COUNT; j++)
            {
                vertexColors.Add(color);
            }
        }

        mesh.SetColors(vertexColors);
    }

    /// <summary>
    /// Generates a texture map and assigns the mesh UVs...
    /// </summary>
    public static Texture2D GenerateTextureMap(ref Mesh mesh, Texture2D inputTexture)
    {
        if(!mesh || inputTexture == null) { return null; }

        Color32[] colorBuffer = inputTexture.GetPixels32();
        Dictionary<Color32, int> colorMap = new Dictionary<Color32, int>();

        for(int i = 0; i < colorBuffer.Length; i++)
        {
            Color32 color = colorBuffer[i];

            if(color.a != byte.MinValue && !colorMap.ContainsKey(color))
            {
                colorMap.Add(color, colorMap.Count);
            }
        }

        Texture2D textureMap = new Texture2D(1, colorMap.Count);

        if(colorMap.Count == 0) { return textureMap; }

        Color32[] colors = new Color32[colorMap.Count];

        foreach(KeyValuePair<Color32, int> color in colorMap)
        {
            colors[color.Value] = color.Key;
        }

        textureMap.SetPixels32(colors);

        List<Vector2> uvs = new List<Vector2>(mesh.vertexCount);
        float offset = 1f / (2f * colorMap.Count);

        for(int i = 0; i < colorBuffer.Length; i++)
        {
            Color32 color = colorBuffer[i];

            if(color.a == byte.MinValue || !colorMap.ContainsKey(color)) continue;

            int index = colorMap[color];
            float v = (float)index / (float)colorMap.Count;

            for(int j = 0; j < CUBE_INDICE_COUNT; j++)
            {
                uvs.Add(new Vector2(0, v + offset));
            }
        }

        mesh.SetUVs(0, uvs);

        textureMap.filterMode = FilterMode.Point;
        textureMap.Apply();

        return textureMap;
    }

    [System.Serializable]
    public class VoxelInfo
    {
        public Texture2D texture = null;
        public Texture2D extrusionMapTexture = null;
        public bool applyColorPerVertex = false;
        public float scale = 1.0f;
        public float extrusionFactor = 1.0f;
    }
}
