using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class EnvironmentMeshCombiner : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private Vector3 gridSize = new Vector3(5f, 5f, 5f);
    
    [Header("Combined Meshes")]
    [SerializeField] private GameObject combinedMeshesParent;
    [SerializeField] private List<GameObject> originalObjects = new List<GameObject>();
    [SerializeField] private List<GameObject> combinedObjects = new List<GameObject>();
    
    [Header("Status")]
    [SerializeField] private bool hasCombinedMeshes = false;
    [SerializeField] private int totalMeshesFound = 0;

#if UNITY_EDITOR
    [System.Serializable]
    private class GridCell
    {
        public Vector3Int gridPosition;
        public List<CombineInstance> combineInstances = new List<CombineInstance>();
        public Material material;
        public Vector3 centerPosition;
    }

    public void CombineMeshes()
    {
        Debug.Log("Starting mesh combination process...");
        
        // Clear previous combined meshes
        ClearCombinedMeshes();

        // Find ALL mesh renderers in this object and ALL children recursively
        List<MeshRenderer> allMeshRenderers = new List<MeshRenderer>();
        FindAllMeshRenderersRecursive(transform, allMeshRenderers);
        
        totalMeshesFound = allMeshRenderers.Count;
        Debug.Log($"Found {totalMeshesFound} mesh renderers in Environment and all children");
        
        if (allMeshRenderers.Count == 0)
        {
            Debug.LogWarning("No mesh renderers found in Environment object or its children!");
            return;
        }

        // Store original objects for later reference
        originalObjects.Clear();
        foreach (MeshRenderer renderer in allMeshRenderers)
        {
            if (!originalObjects.Contains(renderer.gameObject))
                originalObjects.Add(renderer.gameObject);
        }

        Debug.Log($"Stored {originalObjects.Count} original objects");

        // Group meshes by material and grid position
        Dictionary<string, GridCell> gridCells = new Dictionary<string, GridCell>();

        int validMeshCount = 0;
        foreach (MeshRenderer meshRenderer in allMeshRenderers)
        {
            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) 
            {
                Debug.LogWarning($"Skipping {meshRenderer.gameObject.name} - no valid mesh");
                continue;
            }

            validMeshCount++;

            // Calculate grid position based on object's world position
            Vector3 worldPos = meshRenderer.transform.position;
            Vector3Int gridPos = new Vector3Int(
                Mathf.FloorToInt(worldPos.x / gridSize.x),
                Mathf.FloorToInt(worldPos.y / gridSize.y),
                Mathf.FloorToInt(worldPos.z / gridSize.z)
            );

            // Get the main material (first material)
            Material material = meshRenderer.sharedMaterial;
            if (material == null)
            {
                Debug.LogWarning($"Object {meshRenderer.gameObject.name} has no material, using default");
                material = GetDefaultMaterial();
            }

            // Create unique key for grid position + material
            string key = $"{gridPos.x}_{gridPos.y}_{gridPos.z}_{material.name}";

            // Add to grid cell
            if (!gridCells.ContainsKey(key))
            {
                gridCells[key] = new GridCell 
                { 
                    gridPosition = gridPos,
                    material = material,
                    centerPosition = new Vector3(
                        gridPos.x * gridSize.x + gridSize.x * 0.5f,
                        gridPos.y * gridSize.y + gridSize.y * 0.5f,
                        gridPos.z * gridSize.z + gridSize.z * 0.5f
                    )
                };
            }

            // Create combine instance
            CombineInstance combine = new CombineInstance();
            combine.mesh = meshFilter.sharedMesh;
            combine.transform = meshRenderer.transform.localToWorldMatrix;
            combine.subMeshIndex = 0;

            gridCells[key].combineInstances.Add(combine);
        }

        Debug.Log($"Processing {validMeshCount} valid meshes into {gridCells.Count} grid cells");

        // Create parent for combined meshes if it doesn't exist  
        if (combinedMeshesParent == null)
        {
            GameObject parent = new GameObject("CombinedMeshes");
            parent.transform.SetParent(transform);
            parent.transform.localPosition = Vector3.zero;
            combinedMeshesParent = parent;
        }

        combinedObjects.Clear();

        // Combine meshes for each grid cell
        int cellIndex = 0;
        foreach (var kvp in gridCells)
        {
            GridCell cell = kvp.Value;
            if (cell.combineInstances.Count == 0) continue;

            GameObject combinedObject = CreateCombinedMesh(cell, cellIndex);
            if (combinedObject != null)
            {
                combinedObjects.Add(combinedObject);
                Debug.Log($"Created combined object: {combinedObject.name} with {cell.combineInstances.Count} meshes");
            }
            cellIndex++;
        }

        hasCombinedMeshes = true;
        Debug.Log($"Successfully combined {validMeshCount} objects into {combinedObjects.Count} combined meshes.");

        // Mark scene as dirty so changes are saved
        EditorUtility.SetDirty(gameObject);
    }

    private void FindAllMeshRenderersRecursive(Transform parent, List<MeshRenderer> meshRenderers)
    {
        // Check current transform
        MeshRenderer renderer = parent.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            meshRenderers.Add(renderer);
        }

        // Recursively check all children
        for (int i = 0; i < parent.childCount; i++)
        {
            FindAllMeshRenderersRecursive(parent.GetChild(i), meshRenderers);
        }
    }

    private Material GetDefaultMaterial()
    {
        // Try to find a default material, or create one
        Material defaultMat = Resources.GetBuiltinResource<Material>("Default-Material.mat");
        if (defaultMat == null)
        {
            defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.name = "Default Material";
        }
        return defaultMat;
    }

    private GameObject CreateCombinedMesh(GridCell cell, int index)
    {
        if (cell.combineInstances.Count == 0) return null;

        // Create combined object
        GameObject combinedObj = new GameObject($"Combined_Grid_{cell.gridPosition.x}_{cell.gridPosition.y}_{cell.gridPosition.z}_{index}");
        combinedObj.transform.SetParent(combinedMeshesParent.transform);
        combinedObj.transform.position = Vector3.zero; // Keep at origin for world space combining

        // Add components
        MeshFilter meshFilter = combinedObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = combinedObj.AddComponent<MeshRenderer>();

        try
        {
            // Create and combine mesh
            Mesh combinedMesh = new Mesh();
            combinedMesh.name = combinedObj.name + "_Mesh";
            
            // Ensure we can handle large meshes
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            
            // Combine the meshes
            combinedMesh.CombineMeshes(cell.combineInstances.ToArray(), true, true);
            
            // Recalculate bounds and normals
            combinedMesh.RecalculateBounds();
            combinedMesh.RecalculateNormals();
            
            // Assign mesh and material
            meshFilter.sharedMesh = combinedMesh;
            meshRenderer.sharedMaterial = cell.material;

            Debug.Log($"Successfully created combined mesh with {cell.combineInstances.Count} sub-meshes");
            return combinedObj;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to combine meshes for grid cell {cell.gridPosition}: {e.Message}");
            DestroyImmediate(combinedObj);
            return null;
        }
    }

    public void ToggleOriginalObjects()
    {
        bool newState = false;
        if (originalObjects.Count > 0 && originalObjects[0] != null)
        {
            newState = !originalObjects[0].activeSelf;
        }

        int toggledCount = 0;
        foreach (GameObject obj in originalObjects)
        {
            if (obj != null)
            {
                obj.SetActive(newState);
                toggledCount++;
            }
        }
        Debug.Log($"Toggled {toggledCount} original objects to {(newState ? "visible" : "hidden")}");
    }

    public void ToggleCombinedObjects()
    {
        if (combinedMeshesParent != null)
        {
            bool newState = !combinedMeshesParent.activeSelf;
            combinedMeshesParent.SetActive(newState);
            Debug.Log($"Combined objects {(newState ? "shown" : "hidden")}");
        }
    }

    public void ClearCombinedMeshes()
    {
        if (combinedMeshesParent != null)
        {
            DestroyImmediate(combinedMeshesParent);
            combinedMeshesParent = null;
        }
        
        combinedObjects.Clear();
        hasCombinedMeshes = false;
        Debug.Log("Cleared all combined meshes");
    }

    // Method to show original objects (useful for debugging)
    public void ShowOriginalObjects()
    {
        foreach (GameObject obj in originalObjects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
            }
        }
    }

    // Method to hide original objects
    public void HideOriginalObjects()
    {
        foreach (GameObject obj in originalObjects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(EnvironmentMeshCombiner))]
public class EnvironmentMeshCombinerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EnvironmentMeshCombiner combiner = (EnvironmentMeshCombiner)target;
        
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Mesh Combination Tools", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Combine Meshes by Grid", GUILayout.Height(35)))
        {
            combiner.CombineMeshes();
        }
        
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Visibility Controls", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Show Original"))
        {
            combiner.ShowOriginalObjects();
        }
        
        if (GUILayout.Button("Hide Original"))
        {
            combiner.HideOriginalObjects();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Toggle Combined"))
        {
            combiner.ToggleCombinedObjects();
        }
        
        if (GUILayout.Button("Toggle Original"))
        {
            combiner.ToggleOriginalObjects();
        }
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Clear Combined Meshes", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Clear Combined Meshes", 
                "Are you sure you want to clear all combined meshes?", "Yes", "No"))
            {
                combiner.ClearCombinedMeshes();
            }
        }
        
        // Show some stats
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
        
        SerializedProperty totalMeshes = serializedObject.FindProperty("totalMeshesFound");
        SerializedProperty originalObjects = serializedObject.FindProperty("originalObjects");
        SerializedProperty combinedObjects = serializedObject.FindProperty("combinedObjects");
        SerializedProperty hasCombined = serializedObject.FindProperty("hasCombinedMeshes");
        
        EditorGUILayout.LabelField($"Total Meshes Found: {totalMeshes.intValue}");
        EditorGUILayout.LabelField($"Original Objects: {originalObjects.arraySize}");
        EditorGUILayout.LabelField($"Combined Objects: {combinedObjects.arraySize}");
        EditorGUILayout.LabelField($"Has Combined Meshes: {hasCombined.boolValue}");
        
        if (hasCombined.boolValue)
        {
            EditorGUILayout.HelpBox("Meshes have been combined. Use visibility controls to show/hide original or combined geometry.", MessageType.Info);
        }
    }
}
#endif