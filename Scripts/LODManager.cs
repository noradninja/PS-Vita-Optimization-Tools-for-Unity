using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

public class LODManager : MonoBehaviour
{
    public static LODManager Instance { get; private set; }

    [Header("Global LOD Settings")]
    public int tickInterval = 10;
    public int batchSize = 50; // Number of items to process per frame

    private List<Shader_LOD_Enumerator> enumerators = new List<Shader_LOD_Enumerator>();
    private Transform playerTransform;
    private Camera mainCam;
    private float farClipSqr;

    // NativeArrays to store only necessary data
    private NativeArray<Vector2> enumeratorPositions;
    private NativeArray<float> distSqrArray;

    private int currentBatchIndex = 0;

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
        if (enumerators.Count > 0)
            playerTransform = enumerators[0].player.transform;

        farClipSqr = (mainCam.farClipPlane + 2f) * (mainCam.farClipPlane + 2f);
    }

    private int unloadCycleCount = 0; // Count how many frames have passed

    private void Update()
    {
        if (Time.frameCount % tickInterval != 0) return;
        if (playerTransform == null) return;

        // Resize NativeArrays to match the current enumerators list if needed
        if (enumeratorPositions.Length != enumerators.Count)
        {
            if (enumeratorPositions.IsCreated)
                enumeratorPositions.Dispose();

            enumeratorPositions = new NativeArray<Vector2>(enumerators.Count, Allocator.Persistent);

            if (distSqrArray.IsCreated)
                distSqrArray.Dispose();

            distSqrArray = new NativeArray<float>(enumerators.Count, Allocator.Persistent);

            // Populate the NativeArrays with the relevant data
            for (int i = 0; i < enumerators.Count; i++)
            {
                enumeratorPositions[i] = new Vector2(enumerators[i].transform.position.x, enumerators[i].transform.position.z);
            }
        }

        Vector2 playerPos2D = new Vector2(
            playerTransform.position.x,
            playerTransform.position.z
        );

        // Calculate the range of enumerators to process in this frame
        int startIndex = currentBatchIndex * batchSize;
        int endIndex = Mathf.Min((currentBatchIndex + 1) * batchSize, enumerators.Count);

        if (startIndex >= enumerators.Count)
            return;  // No more enumerators to process, we are done

        // Create NativeSlice for positions and distSqrArray for this batch
        NativeSlice<Vector2> batchPositions = enumeratorPositions.Slice(startIndex, endIndex - startIndex);
        NativeSlice<float> batchDistSqrArray = distSqrArray.Slice(startIndex, endIndex - startIndex);

        // Create and schedule job for distance and LOD update for this batch
        LODJob lodJob = new LODJob
        {
            playerPos = playerPos2D,
            farClipSqr = farClipSqr,
            positions = batchPositions,
            distSqrArray = batchDistSqrArray
        };

        JobHandle jobHandle = lodJob.Schedule(batchPositions.Length, 1);
        jobHandle.Complete();

        // After the job completes, update the actual Shader_LOD_Enumerator objects for this batch
        for (int i = startIndex; i < endIndex; i++)
        {
            enumerators[i].UpdateLOD(distSqrArray[i], farClipSqr);
        }

        // Increment the batch index for the next frame
        currentBatchIndex++;

        // Reset batch index if we've processed all enumerators
        if (currentBatchIndex * batchSize >= enumerators.Count)
        {
            currentBatchIndex = 0; // Start over or adjust based on logic

            // Trigger unloading only for objects with disabled renderers every 5 cycles
            unloadCycleCount++;
            if (unloadCycleCount >= 5)
            {
                StartCoroutine(UnloadUnusedAssetsCoroutine());
                unloadCycleCount = 0; // Reset cycle count after unloading
            }
        }
    }

    private IEnumerator UnloadUnusedAssetsCoroutine()
    {
        // Optionally, you can add a small delay to ensure all frame work is done before unloading.
        yield return null; // This simply waits for the next frame.

        // Unload unused assets only for objects with disabled renderers
        foreach (var enumerator in enumerators)
        {
            if (!enumerator.GetComponent<Renderer>().enabled)
            {
                Resources.UnloadUnusedAssets();
                Debug.Log("Unused assets unloaded for disabled renderers.");
                break; // Exit after unloading unused assets for the first disabled renderer
            }
        }
    }

    public void Register(Shader_LOD_Enumerator e)
    {
        if (!enumerators.Contains(e))
            enumerators.Add(e);
    }

    public void Unregister(Shader_LOD_Enumerator e)
    {
        enumerators.Remove(e);
    }

    private void OnDestroy()
    {
        if (enumeratorPositions.IsCreated)
            enumeratorPositions.Dispose();

        if (distSqrArray.IsCreated)
            distSqrArray.Dispose();
    }

    // Define the job struct to handle LOD updates
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
            distSqrArray[index] = distSqr;  // Store the calculated distance in the NativeArray
        }
    }
}