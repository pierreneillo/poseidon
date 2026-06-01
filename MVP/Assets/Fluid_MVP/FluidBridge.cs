using UnityEngine;
using UnityEngine.VFX;
using Unity.Mathematics;
using System.Runtime.InteropServices;
using Random = UnityEngine.Random;

public class FluidBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ComputeShader pbfShader;
    [SerializeField] private VisualEffect vfxGraph;

    [Header("Simulation Settings")]
    [SerializeField] private int particleCount = 1000;
    [SerializeField] private float cellSize = 1.0f;
    [SerializeField] private float smoothingRadius = 1.0f;
    [SerializeField] private Vector2 gravity = new Vector2(0f, -9.81f);

    // Kernels ID
    private int kernelPredict;

    // VRAM
    private GraphicsBuffer particleBuffer;
    private GraphicsBuffer predictedPositionsBuffer;
    private GraphicsBuffer hashesBuffer;
    private GraphicsBuffer neighborBuffer;

    private struct GPUHash { public uint hash; public uint pid; }
    private struct GPUNeighborData { public int count; public unsafe fixed int indices[64]; }

    private float fixedDeltaTime = 0.016f; // 60Hz
    private float accumulator = 0f;

    private FluidParticle[] rawParticles;

    void Start()
    {
        if (vfxGraph == null || pbfShader == null)
        {
            throw new System.NullReferenceException($"[FluidBridge] VisualEffect or PBFShader reference is missing on {gameObject.name}! Did you forget to drag and drop it in the Inspector?");
        }
        kernelPredict = pbfShader.FindKernel("predictPositions");
        rawParticles = new FluidParticle[particleCount];
        for (int i = 0; i < particleCount; i++) {
            rawParticles[i].position = new Vector2(Random.Range(-5f, 5f), Random.Range(2f, 7f));
            rawParticles[i].velocity = new Vector2(0f, 0f);
        }
        int stride = Marshal.SizeOf(typeof(FluidParticle));
        particleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, stride); // equivalent to glGenBuffers() and glBindBuffer() in OpenGL
        particleBuffer.SetData(rawParticles); // pushes data to VRAM
        predictedPositionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, Marshal.SizeOf(typeof(Vector2)));
        hashesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, Marshal.SizeOf(typeof(GPUHash)));
        neighborBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, Marshal.SizeOf(typeof(GPUNeighborData)));

        vfxGraph.SetGraphicsBuffer("ParticleBuffer", particleBuffer);
        vfxGraph.SetInt("ParticleCount", particleCount);
    }

    void Update() {
        accumulator += Time.deltaTime;
        int threadGroupsX = Mathf.CeilToInt((float)particleCount / 64f);
        while (accumulator >= fixedDeltaTime) {
            pbfShader.SetInt("ParticleCount", particleCount);
            pbfShader.SetInt("GridCellCount", 2048);
            pbfShader.SetFloat("cellSize", cellSize);
            pbfShader.SetFloat("dt", fixedDeltaTime);
            pbfShader.SetFloat("smoothingRadius", smoothingRadius);
            pbfShader.SetVector("gravity", gravity);

            pbfShader.SetBuffer(kernelPredict, "Particles", particleBuffer);
            pbfShader.SetBuffer(kernelPredict, "PredictedPositionsBuffer", predictedPositionsBuffer);
            
            pbfShader.Dispatch(kernelPredict, threadGroupsX, 1, 1);

            accumulator -= fixedDeltaTime;
        }
    }
    
    void OnDestroy() {
        if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
        if (predictedPositionsBuffer != null) { predictedPositionsBuffer.Release(); predictedPositionsBuffer = null; }
        if (hashesBuffer != null) { hashesBuffer.Release(); hashesBuffer = null; }
        if (neighborBuffer != null) { neighborBuffer.Release(); neighborBuffer = null; }
    }
}
