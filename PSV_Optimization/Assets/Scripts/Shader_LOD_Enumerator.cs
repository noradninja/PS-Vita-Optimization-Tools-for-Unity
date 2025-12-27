using UnityEngine;
using UnityEngine.Rendering;

public class Shader_LOD_Enumerator : MonoBehaviour
{
    // Define the possible states (Full = Near, Reduced = Medium, VertexOnly = Far)
    public enum LODState
    {
        Full,
        Reduced,
        VertexOnly
    }

    [Header("Distanze al Quadrato (Metri * Metri)")]
    // Example: 10m and 30m become 100 and 900
    public float[] LOD_DistanceSqr = new float[2] { 100f, 900f }; 

    [Header("Riferimenti")]
    public GameObject player; 
    public bool enableShaderLOD = true;
    public bool isFoliage;
    
    // Material Slots: Managed INDIVIDUALLY for each item
    public Material replacementMaterial; // Light Material (Vertex Lit)
    private Material originalMaterial;   // Original heavy material
    
    private Renderer thisRenderer;
    public LODState shaderLOD;
    private bool shadowCaster;

    private void Awake()
    {
        // 1. Get the Renderer of the object
        thisRenderer = GetComponent<Renderer>();
        
        // 2. We save the individual original material
        originalMaterial = thisRenderer.sharedMaterial;
        
        // 3. Save if the object should cast shadows (On/Off)
        shadowCaster = thisRenderer.shadowCastingMode == ShadowCastingMode.On;

        // 4. Ci registriamo al Manager per ricevere i calcoli della distanza
        if (LODManager.Instance != null)
            LODManager.Instance.Register(this);
    }

    void Start()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer == null) return;

        _originalMaterial = _renderer.material;
        _originalTexture = _originalMaterial.mainTexture;

        if (player == null) player = GameObject.FindGameObjectWithTag("Player");

        // EDIT: Use the Manager's refShader instead of searching for it manually
        if (LODManager.Instance != null && LODManager.Instance.refShader != null)
        {
            // We create a UNIQUE instance to hold the individual PVRTC texture
            _lodMaterial = new Material(LODManager.Instance.refShader);
            _lodMaterial.mainTexture = _originalTexture;
            _lodMaterial.name = "LOD_" + gameObject.name;
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
        else
            newState = LODState.VertexOnly;

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
        }
    }

    private void OnDestroy()
    {
        // When the item is removed, it is deleted from the Manager's list.
        if (LODManager.Instance != null)
            LODManager.Instance.Unregister(this);
    }
}
