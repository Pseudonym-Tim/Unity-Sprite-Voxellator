#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class VoxellatorEditor : EditorWindow
{
    public static readonly string VOXELLATOR_NAMETAG = "[Voxellator]";
    private const float DEFAULT_SCALE = 1f;
    private const float DEFAULT_EXTRUDE_FACTOR = 1;

    [MenuItem("Tools/Voxellator")]
    public static void ShowWindow()
    {
        GetWindow<VoxellatorEditor>("Voxellator");
    }

    private Texture2D spriteTexture;
    private Sprite sprite;
    private Texture2D optionExtrusionMapTexture;
    private Sprite extrusionMapSprite;
    private string spriteName;
    private float optionVoxelScale = DEFAULT_SCALE;
    private float optionExtrudeFactor = DEFAULT_EXTRUDE_FACTOR;
    private bool optimizeMesh = true;
    public Material material;
    private bool saveMesh;
    private bool saveTexture;
    private bool optionApplyColorPerVertex;
    private bool createMeshGameObject = true;

    private void OnGUI()
    {
        string debugText = null;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Sprite");
        sprite = (Sprite)EditorGUILayout.ObjectField(sprite, typeof(Sprite), true);
        EditorGUILayout.EndHorizontal();

        if(sprite)
        {
            if(!HasCorrectImportSettings(sprite.texture)) 
            { 
                SetTextureImportSettings(sprite.texture); 
                debugText = "Undesirable import settings detected for this sprite! Automatically corrected!"; 
            }
            else
            {
                // Get and store the texture from the sprite, store sprite name as well...
                spriteTexture = Voxellator.GetTextureFromSprite(sprite); 
                spriteName = sprite.name; 
            }

            // Extrusion map...
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Extrusion Map Sprite");
            extrusionMapSprite = (Sprite)EditorGUILayout.ObjectField(extrusionMapSprite, typeof(Sprite), true);
            EditorGUILayout.EndHorizontal();
           
            // Material
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Material");
            material = (Material)EditorGUILayout.ObjectField(material, typeof(Material), true);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Make sure that we at least have a generic standard material selected...
            if(!material)
            {
                material = new Material(Shader.Find("Standard"));
            }
        }

        if(extrusionMapSprite)
        {
            if(!HasCorrectImportSettings(extrusionMapSprite.texture))
            {
                SetTextureImportSettings(extrusionMapSprite.texture);
                debugText = "Undesirable import settings detected for this extrusion map! Automatically corrected!";
            }
            else
            {
                // Get and store the texture from the sprite...
                optionExtrusionMapTexture = Voxellator.GetTextureFromSprite(extrusionMapSprite); 
            } 
        }
        else
        { 
            optionExtrusionMapTexture = null; 
        }

        if(!sprite)
        {
            GUI.enabled = false;
            debugText = "Select a sprite to voxellate!";
        }
        else
        {
            optionExtrudeFactor = (float)EditorGUILayout.FloatField("Extrusion Factor", optionExtrudeFactor);
        }

        optionVoxelScale = (float)EditorGUILayout.FloatField("Scale", optionVoxelScale);
        optimizeMesh = EditorGUILayout.Toggle("Optimize Mesh", optimizeMesh);
        saveMesh = EditorGUILayout.Toggle("Save Mesh", saveMesh);
        saveTexture = EditorGUILayout.Toggle("Save Texture", saveTexture);
        optionApplyColorPerVertex = EditorGUILayout.Toggle("Per-vertex Colors", optionApplyColorPerVertex);
        createMeshGameObject = EditorGUILayout.Toggle("Add To Scene", createMeshGameObject);

        EditorGUILayout.Space();

        if(!createMeshGameObject && !saveMesh) { GUI.enabled = false; }

        if(GUILayout.Button("Voxellate!")) { VoxellateSprite(); }

        GUI.enabled = true;

        if(debugText != null)
        {
            EditorGUILayout.HelpBox(debugText, MessageType.Warning);
        }
    }

    private void VoxellateSprite()
    {
        // Get the readable texture...
        Texture2D readableTexture = spriteTexture.isReadable ? spriteTexture : Voxellator.ReadTexture(spriteTexture);

        // Construct the info...
        Voxellator.VoxelInfo voxelateInfo = new Voxellator.VoxelInfo()
        {
            applyColorPerVertex = optionApplyColorPerVertex,
            extrusionMapTexture = optionExtrusionMapTexture,
            scale = optionVoxelScale,
            texture = readableTexture,
            extrusionFactor = optionExtrudeFactor,
        };

        // Voxelate us a mesh using this info!
        Mesh voxelatedMesh = Voxellator.Voxellate(voxelateInfo);

        // Generate a texture map for this new mesh!
        Texture2D texture = Voxellator.GenerateTextureMap(ref voxelatedMesh, readableTexture);

        GameObject voxelObjCreated = null;

        // Create the object, optimize the mesh, save the mesh/texture, whatever...
        if(optimizeMesh) { voxelatedMesh.Optimize(); }
        if(createMeshGameObject) { voxelObjCreated = Voxellator.CreateVoxelObject(voxelatedMesh, texture, spriteName, material); }
        if(saveMesh) { SaveMeshToFile(voxelatedMesh); }
        if(saveTexture) { SaveTextureToFile(texture, voxelObjCreated); }
    }

    private void SaveMeshToFile(Mesh mesh)
    {
        string path = EditorUtility.SaveFilePanel("Save mesh to folder...", "Assets/", mesh.name + ".asset", "asset");

        path = FileUtil.GetProjectRelativePath(path);

        if(!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.Log(VOXELLATOR_NAMETAG + " Mesh export failed! Invalid path or user cancellation!");
        }
    }

    private void SaveTextureToFile(Texture2D texture, GameObject voxelObj)
    {
        texture.name = spriteName + Voxellator.VOXEL_NAME_POSTFIX;
        string path = EditorUtility.SaveFilePanel("Save texture to folder...", "Assets/", texture.name + ".png", "png");

        path = FileUtil.GetProjectRelativePath(path);

        if(!string.IsNullOrEmpty(path))
        {
            byte[] _bytes = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, _bytes);

            // We wrote the file! Refresh so we can find and grab it!
            AssetDatabase.Refresh();

            // Grab the texture at this path...
            Texture2D savedText = (Texture2D)AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D));

            // Set it's import settings and refresh again!
            SetTextureImportSettings(savedText);

            // If we selected to add it to the scene, we'll make this texture asset be the texture for the material for convenience...
            if(createMeshGameObject)
            {
                MeshRenderer meshRenderer = voxelObj.GetComponent<MeshRenderer>();
                meshRenderer.sharedMaterial.mainTexture = savedText;
            }
        }
        else
        {
            Debug.Log(VOXELLATOR_NAMETAG + " Texture export failed! Invalid path or user cancellation!");
        }
    }

    private void SetTextureImportSettings(Texture2D texture)
    {
        #if UNITY_EDITOR

        if(!texture) { return; }

        string assetPath = AssetDatabase.GetAssetPath(texture);
        TextureImporter texImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if(texImporter != null)
        {
            // Force texture import settings to be RGBA32 through this garbage for best results...
            TextureImporterPlatformSettings texset = texImporter.GetDefaultPlatformTextureSettings();
            texset.format = TextureImporterFormat.RGBA32;
            texImporter.SetPlatformTextureSettings(texset);

            // Force no compression, NPOT scale to none, point filtering, and clamp wrap mode...
            texImporter.textureCompression = TextureImporterCompression.Uncompressed;
            texImporter.npotScale = TextureImporterNPOTScale.None;
            texImporter.filterMode = FilterMode.Point;
            texImporter.wrapMode = TextureWrapMode.Clamp;

            // Make sure it's readable...
            texImporter.isReadable = true;

            // Cool, import and refresh...
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.Refresh();
        }

        #endif
    }

    private bool HasCorrectImportSettings(Texture2D texture)
    {
        string assetPath = AssetDatabase.GetAssetPath(texture);
        TextureImporter texImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if(texImporter != null)
        {
            bool correctFormat = texture.format == TextureFormat.RGBA32;
            bool setReadable = texture.isReadable;

            bool passedImportCheck = correctFormat && setReadable;

            if(passedImportCheck) { return true; }
        }

        return false;
    }
}
#endif
