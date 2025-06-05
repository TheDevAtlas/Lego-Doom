using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegoFireController : MonoBehaviour
{
    [Header("Fire Pieces")]
    [SerializeField] private List<GameObject> firePieces = new List<GameObject>();
    
    [Header("Animation Settings")]
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobHeight = 0.1f;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0f, 0f, 90f); // degrees per second for each axis
    [SerializeField] private float maxOffsetRange = 2f; // maximum random offset for timing
    
    // Store original positions and individual offsets
    private List<Vector3> originalPositions = new List<Vector3>();
    private List<float> timeOffsets = new List<float>();
    
    void Start()
    {
        InitializeFirePieces();
    }
    
    void InitializeFirePieces()
    {
        originalPositions.Clear();
        timeOffsets.Clear();
        
        for (int i = 0; i < firePieces.Count; i++)
        {
            if (firePieces[i] != null)
            {
                // Store original position
                originalPositions.Add(firePieces[i].transform.position);
                
                // Generate random time offset for each piece
                timeOffsets.Add(Random.Range(0f, maxOffsetRange));
            }
            else
            {
                originalPositions.Add(Vector3.zero);
                timeOffsets.Add(0f);
            }
        }
    }
    
    void Update()
    {
        AnimateFirePieces();
    }
    
    void AnimateFirePieces()
    {
        for (int i = 0; i < firePieces.Count; i++)
        {
            if (firePieces[i] != null)
            {
                // Calculate time with offset
                float offsetTime = Time.time + timeOffsets[i];
                
                // Calculate vertical bobbing motion
                float yOffset = Mathf.Sin(offsetTime * bobSpeed) * bobHeight;
                Vector3 newPosition = originalPositions[i] + Vector3.up * yOffset;
                firePieces[i].transform.position = newPosition;
                
                // Calculate rotation (oscillating between -180 and 180 on each axis)
                float rotationX = Mathf.Sin(offsetTime * (rotationSpeed.x * Mathf.Deg2Rad)) * 180f;
                float rotationY = Mathf.Sin(offsetTime * (rotationSpeed.y * Mathf.Deg2Rad)) * 180f;
                float rotationZ = Mathf.Sin(offsetTime * (rotationSpeed.z * Mathf.Deg2Rad)) * 180f;
                firePieces[i].transform.rotation = Quaternion.Euler(rotationX, rotationY, rotationZ);
            }
        }
    }
    
    // Public method to add fire pieces at runtime if needed
    public void AddFirePiece(GameObject firePiece)
    {
        if (firePiece != null && !firePieces.Contains(firePiece))
        {
            firePieces.Add(firePiece);
            originalPositions.Add(firePiece.transform.position);
            timeOffsets.Add(Random.Range(0f, maxOffsetRange));
        }
    }
    
    // Public method to remove fire pieces at runtime if needed
    public void RemoveFirePiece(GameObject firePiece)
    {
        int index = firePieces.IndexOf(firePiece);
        if (index >= 0)
        {
            firePieces.RemoveAt(index);
            originalPositions.RemoveAt(index);
            timeOffsets.RemoveAt(index);
        }
    }
    
    // Reset all fire pieces to their original positions (useful for level restart)
    public void ResetFirePieces()
    {
        InitializeFirePieces();
        
        for (int i = 0; i < firePieces.Count; i++)
        {
            if (firePieces[i] != null)
            {
                firePieces[i].transform.position = originalPositions[i];
                firePieces[i].transform.rotation = Quaternion.identity;
            }
        }
    }
    
    // Enable/disable the fire effect
    public void SetFireEffectActive(bool active)
    {
        this.enabled = active;
        
        if (!active)
        {
            // Reset to original positions when disabled
            for (int i = 0; i < firePieces.Count; i++)
            {
                if (firePieces[i] != null)
                {
                    firePieces[i].transform.position = originalPositions[i];
                    firePieces[i].transform.rotation = Quaternion.identity;
                }
            }
        }
    }
}