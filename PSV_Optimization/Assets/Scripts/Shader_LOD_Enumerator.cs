using UnityEngine;
using UnityEngine.Rendering;

public class Shader_LOD_Enumerator : MonoBehaviour
{
    // Define the possible states (Full = Near, Reduced = Medium, VertexOnly = Far)
    public enum LODState
    {
        Full,
        Reduced,
        VertexOnly,
        Disabled
    }

    [Header("Square Distance (m * m)")]
    // Example: 10m, 20m and 30m become 100, 400 and 900
    public float[] LOD_DistanceSqr = new float[3] { 100f, 400f, 900f }; 

    [Header("References")]
    public GameObject player; 
    public bool enableShaderLOD = true;
    public bool isFoliage;
    
    // Material Slots: Managed INDIVIDUALLY for each item
    public LODState shaderLOD;
    private Material replacementMaterial; // Light Material (Vertex Lit)
    private Material originalMaterial;   // Original heavy material
    private Texture originalTexture;
    private Renderer thisRenderer;
    private bool shadowCaster;
    
    //EDIT: Registering and assignment of these variables needs to happen in Awake, because we want all these
    //vars to be in place before any Start() method is called, so it's all done before the first frame presents
    void Awake()
    {
        thisRenderer = GetComponent<Renderer>();
        
        if (thisRenderer == null) return;

        originalMaterial = thisRenderer.material;
        originalTexture = originalMaterial.mainTexture;
        shadowCaster = thisRenderer.shadowCastingMode == ShadowCastingMode.On;
        
        if (player == null) player = GameObject.FindGameObjectWithTag("Player");

        // EDIT: Use the Manager's refShader instead of searching for it manually
        if (LODManager.Instance != null && LODManager.Instance.refShader != null)
        {
            // We create a UNIQUE instance to hold the individual PVRTC texture
            replacementMaterial = new Material(LODManager.Instance.refShader);
            replacementMaterial.mainTexture = originalTexture;
            replacementMaterial.name = "LOD_" + gameObject.name;
        }

        LODManager.Instance.Register(this);
    }

    // This function is called from the General (LODManager)
    public void UpdateLOD(float currentDistSqr, float farClipSqr)
    {
        if (!enableShaderLOD) return;

        LODState newState;

        // Threshold calculation (without square roots, very fast)
        if (currentDistSqr <= LOD_DistanceSqr[0])
            newState = LODState.Full;
        else if (currentDistSqr <= LOD_DistanceSqr[1])
            newState = LODState.Reduced;
        else if (currentDistSqr <= LOD_DistanceSqr[2])
            newState = LODState.VertexOnly;
        else
            newState = LODState.Disabled;

        // We change materials ONLY if the state is different from the current one
        if (newState != shaderLOD)
        {
            shaderLOD = newState;
            ApplySettings();
        }
    }

    private void ApplySettings()
    {
        switch (shaderLOD)
        {
            case LODState.Full:
                // We put back the original individual material
                thisRenderer.sharedMaterial = originalMaterial;
                thisRenderer.sharedMaterial.EnableKeyword("_NORMALMAP");
                if (shadowCaster) thisRenderer.shadowCastingMode = ShadowCastingMode.On;
                break;

            case LODState.Reduced:
                // We keep the original but turn off the Normal Maps to lighten the GPU
                thisRenderer.sharedMaterial = originalMaterial;
                thisRenderer.sharedMaterial.DisableKeyword("_NORMALMAP");
                if (shadowCaster) thisRenderer.shadowCastingMode = ShadowCastingMode.Off;
                break;

            case LODState.VertexOnly:
                // We put the light material created especially for this object
                thisRenderer.sharedMaterial = replacementMaterial;
                if (shadowCaster) thisRenderer.shadowCastingMode = ShadowCastingMode.Off;
                break;
            
            case LODState.Disabled:
                // We disable the renderer to stop issuing GC's for this object, CPU/GPU cost will drop to near-zero
                thisRenderer.enabled = false;
                if (shadowCaster) thisRenderer.shadowCastingMode = ShadowCastingMode.Off;
                break;
        }
    }

    private void OnDestroy()
    {
        // When the item is removed, it is deleted from the Manager's list.
        if (LODManager.Instance != null)
            LODManager.Instance.Unregister(this);
    }
}
