using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

public class LODManager : MonoBehaviour
{
    public static LODManager Instance { get; private set; }

    [Header("Global LOD Settings")]
    public int batchSize = 50; 
    public int unloadEveryXCycles = 5; // How often should all objects complete cycles be cleared from RAM?

    private List<Shader_LOD_Enumerator> enumerators = new List<Shader_LOD_Enumerator>();
    private Transform playerTransform;
    private Camera mainCam;
    private float farClipSqr;

    private NativeArray<Vector2> enumeratorPositions;
    private NativeArray<float> distSqrArray;
    
    private JobHandle lodJobHandle;
    private bool isJobScheduled = false;

    private int currentBatchIndex = 0;
    private int cycleCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        mainCam = Camera.main;
    }

    private void Start()
    {
        if (enumerators.Count > 0 && enumerators[0] != null)
            playerTransform = enumerators[0].player.transform;

        farClipSqr = (mainCam.farClipPlane + 2f) * (mainCam.farClipPlane + 2f);
    }

    private void Update()
    {
        if (playerTransform == null) return;

        // 1. RESULTS RECOVERY 
        if (isJobScheduled)
        {
            lodJobHandle.Complete();
            ApplyLODResults();
            isJobScheduled = false;
            
            currentBatchIndex++;

            // Check if we have completed a complete tour of all the objects
            if (currentBatchIndex * batchSize >= enumerators.Count)
            {
                currentBatchIndex = 0;
                cycleCount++;

                // Trigger RAM clean every X complete cycles
                if (cycleCount >= unloadEveryXCycles)
                {
                    cycleCount = 0;
                    Resources.UnloadUnusedAssets();
                    // Debug.Log("PS Vita RAM: Cleanup of unused assets completed.");
                }
            }
        }

        // 2. PREPARING A NEW BATCH
        if (!enumeratorPositions.IsCreated || enumeratorPositions.Length != enumerators.Count)
        {
            ResizeNativeArrays();
        }

        int startIndex = currentBatchIndex * batchSize;
        int endIndex = Mathf.Min((currentBatchIndex + 1) * batchSize, enumerators.Count);

        if (startIndex >= enumerators.Count) return;

        for (int i = startIndex; i < endIndex; i++)
        {
            if (enumerators[i] != null)
            {
                enumeratorPositions[i] = new Vector2(enumerators[i].transform.position.x, enumerators[i].transform.position.z);
            }
        }

        Vector2 playerPos2D = new Vector2(playerTransform.position.x, playerTransform.position.z);

        NativeSlice<Vector2> batchPositions = enumeratorPositions.Slice(startIndex, endIndex - startIndex);
        NativeSlice<float> batchDistSqrArray = distSqrArray.Slice(startIndex, endIndex - startIndex);

        // 3. JOB SCHEDULING
        LODJob lodJob = new LODJob
        {
            playerPos = playerPos2D,
            farClipSqr = farClipSqr,
            positions = batchPositions,
            distSqrArray = batchDistSqrArray
        };

        lodJobHandle = lodJob.Schedule(batchPositions.Length, 1);
        isJobScheduled = true;
    }

    private void ApplyLODResults()
    {
        int startIndex = currentBatchIndex * batchSize;
        int endIndex = Mathf.Min((currentBatchIndex + 1) * batchSize, enumerators.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            if (enumerators[i] != null)
            {
                enumerators[i].UpdateLOD(distSqrArray[i], farClipSqr);
            }
        }
    }

    private void ResizeNativeArrays()
    {
        if (isJobScheduled) lodJobHandle.Complete();
        if (enumeratorPositions.IsCreated) enumeratorPositions.Dispose();
        if (distSqrArray.IsCreated) distSqrArray.Dispose();

        enumeratorPositions = new NativeArray<Vector2>(enumerators.Count, Allocator.Persistent);
        distSqrArray = new NativeArray<float>(enumerators.Count, Allocator.Persistent);
    }

    public void Register(Shader_LOD_Enumerator e)
    {
        if (!enumerators.Contains(e))
        {
            if (isJobScheduled) { lodJobHandle.Complete(); isJobScheduled = false; }
            enumerators.Add(e);
            ResizeNativeArrays();
        }
    }

    public void Unregister(Shader_LOD_Enumerator e)
    {
        if (enumerators.Contains(e))
        {
            if (isJobScheduled) { lodJobHandle.Complete(); isJobScheduled = false; }
            enumerators.Remove(e);
            ResizeNativeArrays();
        }
    }

    private void OnDestroy()
    {
        if (isJobScheduled) lodJobHandle.Complete();
        if (enumeratorPositions.IsCreated) enumeratorPositions.Dispose();
        if (distSqrArray.IsCreated) distSqrArray.Dispose();
    }

    struct LODJob : IJobParallelFor
    {
        public Vector2 playerPos;
        public float farClipSqr;
        public NativeSlice<Vector2> positions;
        public NativeSlice<float> distSqrArray;

        public void Execute(int index)
        {
            Vector2 thisPos2D = positions[index];
            float distSqr = (playerPos - thisPos2D).sqrMagnitude;
            distSqrArray[index] = distSqr; 
        }
    }
}