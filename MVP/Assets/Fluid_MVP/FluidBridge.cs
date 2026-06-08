using UnityEngine;
using UnityEngine.VFX;
using Unity.Mathematics;
using System.Runtime.InteropServices;
using Random = UnityEngine.Random;
using Unity.VisualScripting;
using System.Diagnostics;

public class FluidBridge : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ComputeShader pbfShader;
    [SerializeField] private VisualEffect vfxGraph;

    [Header("Simulation Settings")]
    [SerializeField] private int particleCount = 1000;
    [SerializeField] private int gridCellCount = 2048;
    [SerializeField] private int maxParticlesPerCell = 128;
    [SerializeField] private float cellSize = 1.0f;
    [SerializeField] private float smoothingRadius = 1.0f;
    [SerializeField] private Vector2 gravity = new Vector2(0f, -9.81f);
    [SerializeField] private float vorticity_epsilon = 0.01f;
    [SerializeField] private float viscosity_c = 0.01f;
    [SerializeField] private float collision_damping = 0.1f;

    [SerializeField] private uint solverIterations = 3;
    [SerializeField] private Vector2 spawnMinPos = new Vector2(-5f, 2f);
    [SerializeField] private Vector2 spawnMaxPos = new Vector2(5f, 7f);
    [SerializeField] private float rho_0 = 1000.0f;


    // Kernels ID
    private int kernelClear;
    private int kernelBuild;
    private int kernelPredict;
    private int kernelDensity;
    private int kernelApplyConstraints;
    private int kernelVorticity;
    private int kernelUpdateParticles;

    // VRAM
    private GraphicsBuffer particleBuffer;
    private GraphicsBuffer predictedPositionsBuffer;
    private GraphicsBuffer lambdaBuffer;
    private GraphicsBuffer vorticityBuffer;

    private GraphicsBuffer particlesInCellBuffer;
    private GraphicsBuffer nInCellBuffer;

    private GraphicsBuffer debugColorBuffer;

    private float fixedDeltaTime = 0.016f; // 60Hz
    private float accumulator = 0f;

    private FluidParticle[] rawParticles;

    private Stopwatch timer = new Stopwatch();
    private float timeAccumulator = 0f;
    private int frameCount = 0;

    void Start()
    {
        if (vfxGraph == null || pbfShader == null)
        {
            throw new System.NullReferenceException($"[FluidBridge] VisualEffect or PBFShader reference is missing on {gameObject.name}! Did you forget to drag and drop it in the Inspector?");
        }

        kernelClear = pbfShader.FindKernel("clearNeighbourGrid");
        kernelBuild = pbfShader.FindKernel("buildNeighbourGrid");
        kernelPredict = pbfShader.FindKernel("predictPositions");
        kernelDensity = pbfShader.FindKernel("calculateDensityAndLambda");
        kernelApplyConstraints = pbfShader.FindKernel("applyDensityConstraints");
        kernelVorticity = pbfShader.FindKernel("calculateVorticity");
        kernelUpdateParticles = pbfShader.FindKernel("updateParticles");

        rawParticles = new FluidParticle[particleCount];
        // Ideally, particles must appear at a distance of 0.5 * smoothingRadius from each other
        // We calculate density of the spawn zone
        float2 spawnSize = spawnMaxPos - spawnMinPos;
        float density = particleCount / spawnSize.x * spawnSize.y;
        UnityEngine.Debug.Log($"The spawn density is {density}");
        float4[] colors = new float4[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            rawParticles[i].position = new Vector2(Random.Range(spawnMinPos.x, spawnMaxPos.x), Random.Range(spawnMinPos.y, spawnMaxPos.y)) + (Random.insideUnitCircle * 0.01f);
            rawParticles[i].velocity = new Vector2(0f, 0f);
            colors[i] = new float4(0, 0, 1, 1);
        }

        int stride = Marshal.SizeOf(typeof(FluidParticle));
        particleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, stride); // equivalent to glGenBuffers() and glBindBuffer() in OpenGL
        particleBuffer.SetData(rawParticles); // pushes data to VRAM

        predictedPositionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, Marshal.SizeOf(typeof(Vector2)));
        lambdaBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, Marshal.SizeOf(typeof(float)));
        vorticityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, Marshal.SizeOf(typeof(float)));

        int totalGridEntries = gridCellCount * maxParticlesPerCell;
        particlesInCellBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalGridEntries, Marshal.SizeOf(typeof(uint)));

        nInCellBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gridCellCount, Marshal.SizeOf(typeof(uint)));

        debugColorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, particleCount, Marshal.SizeOf(typeof(float)) * 4);
        debugColorBuffer.SetData(colors);

        vfxGraph.SetGraphicsBuffer("ParticleBuffer", particleBuffer);
        vfxGraph.SetInt("ParticleCount", particleCount);
        vfxGraph.SetGraphicsBuffer("ColorBuffer", debugColorBuffer);
    }

    void Update()
    {
        accumulator += Time.deltaTime;

        // TODO ? : rename these
        int threadGroupsParticles = Mathf.CeilToInt(particleCount / 64f);
        int threadGroupsGrid = Mathf.CeilToInt(gridCellCount / 64f);

        int maxStepsPerFrame = 2;
        int currentSteps = 0;

        // FIXME : This condition can lead to bigger and bigger time steps of the calculation time exceeds deltaTime, and eventually to the simulation completely crashing
        while (accumulator >= fixedDeltaTime && currentSteps < maxStepsPerFrame)
        {
            currentSteps++;

            // send constant parameters to the cbuffer
            pbfShader.SetInt("ParticleCount", particleCount);
            pbfShader.SetInt("GridCellCount", gridCellCount);
            pbfShader.SetInt("MaxParticlesPerCell", maxParticlesPerCell);
            pbfShader.SetFloat("cellSize", cellSize);
            pbfShader.SetFloat("dt", fixedDeltaTime);
            pbfShader.SetFloat("smoothingRadius", smoothingRadius);
            pbfShader.SetVector("gravity", gravity);
            pbfShader.SetFloat("vorticity_epsilon", vorticity_epsilon);
            pbfShader.SetFloat("viscosity_c", viscosity_c);
            pbfShader.SetFloat("collision_damping", collision_damping);
            pbfShader.SetFloat("rho_0", rho_0);

            timer.Restart();

            // Predict positions
            pbfShader.SetBuffer(kernelPredict, "Particles", particleBuffer);
            pbfShader.SetBuffer(kernelPredict, "PredictedPositionsBuffer", predictedPositionsBuffer);
            pbfShader.SetBuffer(kernelPredict, "Colors", debugColorBuffer);
            pbfShader.Dispatch(kernelPredict, threadGroupsParticles, 1, 1);

            for (int iter = 0; iter < solverIterations; iter++)
            {

                // Reset of grid counters (NInCell)
                pbfShader.SetBuffer(kernelClear, "NInCell", nInCellBuffer);
                pbfShader.Dispatch(kernelClear, threadGroupsGrid, 1, 1);

                // Fill the spatial grid (InterlockedAdd)
                pbfShader.SetBuffer(kernelBuild, "PredictedPositionsBuffer", predictedPositionsBuffer);
                pbfShader.SetBuffer(kernelBuild, "ParticlesInCell", particlesInCellBuffer);
                pbfShader.SetBuffer(kernelBuild, "NInCell", nInCellBuffer);
                pbfShader.Dispatch(kernelBuild, threadGroupsParticles, 1, 1);

                // Density and Lambda calculation
                pbfShader.SetBuffer(kernelDensity, "PredictedPositionsBuffer", predictedPositionsBuffer);
                pbfShader.SetBuffer(kernelDensity, "ParticlesInCell", particlesInCellBuffer);
                pbfShader.SetBuffer(kernelDensity, "NInCell", nInCellBuffer);
                pbfShader.SetBuffer(kernelDensity, "LambdaBuffer", lambdaBuffer);
                pbfShader.Dispatch(kernelDensity, threadGroupsParticles, 1, 1);

                pbfShader.SetBuffer(kernelApplyConstraints, "PredictedPositionsBuffer", predictedPositionsBuffer);
                pbfShader.SetBuffer(kernelApplyConstraints, "LambdaBuffer", lambdaBuffer);
                pbfShader.SetBuffer(kernelApplyConstraints, "ParticlesInCell", particlesInCellBuffer);
                pbfShader.SetBuffer(kernelApplyConstraints, "NInCell", nInCellBuffer);
                pbfShader.Dispatch(kernelApplyConstraints, threadGroupsParticles, 1, 1);
            }

            pbfShader.SetBuffer(kernelVorticity, "Vorticity", vorticityBuffer);
            pbfShader.SetBuffer(kernelVorticity, "Particles", particleBuffer);
            pbfShader.SetBuffer(kernelVorticity, "PredictedPositionsBuffer", predictedPositionsBuffer);
            pbfShader.SetBuffer(kernelVorticity, "ParticlesInCell", particlesInCellBuffer);
            pbfShader.SetBuffer(kernelVorticity, "NInCell", nInCellBuffer);
            pbfShader.Dispatch(kernelVorticity, threadGroupsParticles, 1, 1);

            pbfShader.SetBuffer(kernelUpdateParticles, "Vorticity", vorticityBuffer);
            pbfShader.SetBuffer(kernelUpdateParticles, "Particles", particleBuffer);
            pbfShader.SetBuffer(kernelUpdateParticles, "PredictedPositionsBuffer", predictedPositionsBuffer);
            pbfShader.SetBuffer(kernelUpdateParticles, "ParticlesInCell", particlesInCellBuffer);
            pbfShader.SetBuffer(kernelUpdateParticles, "NInCell", nInCellBuffer);
            pbfShader.Dispatch(kernelUpdateParticles, threadGroupsParticles, 1, 1);

            UnityEngine.Rendering.AsyncGPUReadback.Request(lambdaBuffer).WaitForCompletion();

            timer.Stop();
            // UnityEngine.Debug.Log($"[PBF Profiler] Instant time: {timer.Elapsed.TotalMilliseconds:F3} ms");
            accumulator -= fixedDeltaTime;

            timeAccumulator += (float)timer.Elapsed.TotalMilliseconds;
            frameCount++;

            if (frameCount >= 100)
            {
                float averageTime = timeAccumulator / frameCount;
                UnityEngine.Debug.Log($"[PBF Benchmark] Mean time over 100 frames: {averageTime:F3} ms");

                // Reset
                timeAccumulator = 0f;
                frameCount = 0;
            }
        }
    }

    void OnDestroy()
    {
        if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
        if (predictedPositionsBuffer != null) { predictedPositionsBuffer.Release(); predictedPositionsBuffer = null; }
        if (lambdaBuffer != null) { lambdaBuffer.Release(); lambdaBuffer = null; }
        if (vorticityBuffer != null) { vorticityBuffer.Release(); vorticityBuffer = null; }
        if (particlesInCellBuffer != null) { particlesInCellBuffer.Release(); particlesInCellBuffer = null; }
        if (nInCellBuffer != null) { nInCellBuffer.Release(); nInCellBuffer = null; }
        if (debugColorBuffer != null) { debugColorBuffer.Release(); debugColorBuffer = null; }
    }
}
