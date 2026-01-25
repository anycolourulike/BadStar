using UnityEngine;

public class MaterialBlender : MonoBehaviour
{
    public Material material1; // Reference to the first material
    public Material material2; // Reference to the second material
    public float blendSpeed = 0.5f; // Speed at which to blend between the materials
    private float blendFactor = 0f; // Current blending factor between the materials

    private Renderer renderer1;

    private void Start()
    {
        renderer1 = GetComponent<Renderer>();
        renderer1.material = material1; // Start with material1
    }

    private void Update()
    {
        // Blend between materials gradually
        blendFactor = Mathf.Clamp01(blendFactor + blendSpeed * Time.deltaTime);

        // Set the blend factor in the shader
        renderer1.material.SetFloat("_BlendFactor", blendFactor);

        // Optionally, you can reverse the blending direction by uncommenting the following line:
        // blendFactor = Mathf.Clamp01(blendFactor - blendSpeed * Time.deltaTime);

        // If the blending is complete, swap to the second material
        if (blendFactor >= 1f && renderer1.material == material1)
        {
            renderer1.material = material2;
            blendFactor = 0f; // Reset blend factor for the next cycle
        }
    }
}
