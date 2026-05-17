using UnityEngine;
using UnityEngine.VFX;
using Unity.Mathematics;
using System.Runtime.InteropServices;
using Random = UnityEngine.Random;

public class FluidBridge : MonoBehaviour
{
    [SerializeField] private VisualEffect vfxGraph;
    [SerializeField] private int particleCount = 1000;

    private GraphicsBuffer particleBuffer;
    private FluidParticle[] rawParticles;

    void Start()
    {
        if (vfxGraph == null)
        {
            throw new System.NullReferenceException($"[FluidBridge] VisualEffect reference is missing on {gameObject.name}! Did you forget to drag and drop it in the Inspector?");
        }
        rawParticles = new FluidParticle[particleCount];
        for (int i = 0; i < particleCount; i++) {
            rawParticles[i].position = new float2(Random.Range(-5f, 5f), Random.Range(-5f, 5f));
            rawParticles[i].velocity = new float2(0f, 0f);
            rawParticles[i].type = (i % 2 == 0) ? 0.0f : 1.0f;
        }
        int stride = Marshal.SizeOf(typeof(FluidParticle));
        particleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, stride); // equivalent to glGenBuffers() and glBindBuffer() in OpenGL
        particleBuffer.SetData(rawParticles); // pushes data to VRAM
        vfxGraph.SetGraphicsBuffer("ParticleBuffer", particleBuffer);
        vfxGraph.SetInt("ParticleCount", particleCount);
    }

    // Update will be handled by the GPU
    
    void OnDestroy() {
        if (particleBuffer != null) {
            particleBuffer.Release();
            particleBuffer = null;
        }
    }
}
