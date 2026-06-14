using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

[System.Serializable] // will be useful for debug
[StructLayout(LayoutKind.Sequential)]
public struct GPUObstacleAABB
{
    public Vector2 minPos;
    public Vector2 maxPos;
}

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

    [Header("Interactions, feedback and gameplay")]
    private GraphicsBuffer obstaclesBuffer;
    private GPUObstacleAABB[] obstacleData;
    private GraphicsBuffer collisionFeedbackBuffer;
    private uint[] feedbackData; // number of particles in collision with obstacles (index 0 for player, 1 for first enemy...)

    private bool isWaitingForFeedback = false; // to avoid overloading gpu with requests
    private const int max_obstacles = 32; // 1 player + 31 enemies

    private static Enemy[] activeObstacles = new Enemy[max_obstacles];
    private static int currentCount = 1;

    [Header("Static walls & Signed Distance Field")]
    [SerializeField] private Vector2 sdfSize = new Vector2(20f, 20f);
    [SerializeField] private int sdfResolution = 1;

    private GraphicsBuffer sdfValuesBuffer;
    private GraphicsBuffer sdfGradientBuffer;
    private float[] sdfValues;
    private Vector2[] sdfGradientValues;

    public static int RegisterObstacle(Enemy enemy) {
        for (int i = 1; i < max_obstacles; i++) {
            if (activeObstacles[i] == null) {
                activeObstacles[i] = enemy;
                currentCount++;
                return i;
            }
        }
        return -1;
    }

    public static void UnregisterObstacle(int id) {
        if (id > 0 && id < max_obstacles) activeObstacles[id] = null;
    }



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

        int aabbStride = Marshal.SizeOf(typeof(GPUObstacleAABB));
        obstaclesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, max_obstacles, aabbStride);
        obstacleData = new GPUObstacleAABB[max_obstacles];

        collisionFeedbackBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, max_obstacles, sizeof(uint));
        feedbackData = new uint[max_obstacles];
        collisionFeedbackBuffer.SetData(feedbackData);

        vfxGraph.SetGraphicsBuffer("ParticleBuffer", particleBuffer);
        vfxGraph.SetInt("ParticleCount", particleCount);
        vfxGraph.SetGraphicsBuffer("ColorBuffer", debugColorBuffer);


        // Create the Signed Distance Field
        AxisAlignedRectWall[] walls = Object.FindObjectsByType<AxisAlignedRectWall>(FindObjectsSortMode.None);
        sdfValues = new float[sdfResolution*sdfResolution];
        sdfGradientValues = new Vector2[sdfResolution*sdfResolution];
        
        
        int sdfHalfResolution = sdfResolution / 2;
        Vector2 pixelStep = new Vector2((sdfSize.x * 2f) / sdfResolution, (sdfSize.y * 2f) / sdfResolution);

        for (int i = 0; i < sdfResolution; i++)
        {
            for(int j = 0; j < sdfResolution; j++) {
                Vector2 position = new Vector2((i - sdfHalfResolution) * pixelStep.x, (j - sdfHalfResolution) * pixelStep.y);

                float minSdfValue = float.MaxValue;
                Vector2 bestGradient = Vector2.zero;
                bool wallFound = false;

                foreach (AxisAlignedRectWall wall in walls)
                {
                    Collider2D col = wall.GetComponent<Collider2D>();
                    if (col != null)
                    {
                        wallFound = true;
                        Vector2 center = col.bounds.center;
                        Vector2 halfExtents = col.bounds.extents;
                        Vector2 localP = position - center;

                        Vector2 v = new Vector2(Mathf.Abs(localP.x) - halfExtents.x, Mathf.Abs(localP.y) - halfExtents.y);

                        // Compute the SDF value for THIS wall
                        float extDist = Vector2.Max(v, Vector2.zero).magnitude;
                        float intDist = Mathf.Min(Mathf.Max(v.x, v.y), 0.0f);
                        float currentSdf = extDist + intDist;

                        // UNION LOGIC (Opération CSG) : On ne garde que le mur le plus proche du pixel
                        if (currentSdf < minSdfValue)
                        {
                            minSdfValue = currentSdf;

                            // Compute the Gradient for THIS wall
                            Vector2 signP = new Vector2(Mathf.Sign(localP.x), Mathf.Sign(localP.y));
                            if (v.x > 0.0f || v.y > 0.0f)
                            {
                                bestGradient = Vector2.Max(v, Vector2.zero).normalized * signP;
                            }
                            else
                            {
                                if (v.x > v.y) bestGradient = new Vector2(signP.x, 0.0f);
                                else bestGradient = new Vector2(0.0f, signP.y);
                            }
                        }
                    }
                }

                // Index 1D
                int pixelIdx = i + (sdfResolution * j);

                if (wallFound)
                {
                    sdfValues[pixelIdx] = minSdfValue;
                    sdfGradientValues[pixelIdx] = bestGradient.normalized;
                }
                else
                {
                    sdfValues[pixelIdx] = 99999f;
                    sdfGradientValues[pixelIdx] = Vector2.zero;
                }
            }
        }

        sdfValuesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sdfResolution * sdfResolution, sizeof(float));
        sdfValuesBuffer.SetData(sdfValues);
        sdfGradientBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, sdfResolution * sdfResolution, Marshal.SizeOf(typeof(Vector2)));
        sdfGradientBuffer.SetData(sdfGradientValues);

        pbfShader.SetBuffer(kernelPredict, "SdfValuesBuffer", sdfValuesBuffer);
        pbfShader.SetBuffer(kernelPredict, "SdfGradientBuffer", sdfGradientBuffer);

        pbfShader.SetInt("sdfResolution", sdfResolution);
        pbfShader.SetVector("sdfSize", sdfSize);
    }

    void Update()
    {
        accumulator += Time.deltaTime;

        System.Array.Clear(obstacleData, 0, obstacleData.Length);

        PlayerScript player = Object.FindFirstObjectByType<PlayerScript>();
        if (player != null) {
            Collider2D col = player.GetComponent<Collider2D>();
            if (col != null) {
                obstacleData[0].minPos = col.bounds.min;
                obstacleData[0].maxPos = col.bounds.max;
            }
        }

        for (int i = 1; i < max_obstacles; i++) {
            if (activeObstacles[i] != null) {
                Collider2D col = activeObstacles[i].GetComponent<Collider2D>();
                if (col != null) {
                    obstacleData[i].minPos = col.bounds.min;
                    obstacleData[i].maxPos = col.bounds.max;
                }
            }
        }

        obstaclesBuffer.SetData(obstacleData);

        // TODO ? : rename these
        int threadGroupsParticles = Mathf.CeilToInt(particleCount / 64f);
        int threadGroupsGrid = Mathf.CeilToInt(gridCellCount / 64f);

        int maxStepsPerFrame = 2;
        int currentSteps = 0;

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
            pbfShader.SetInt("ObstacleCount", currentCount);
            

            timer.Restart();

            // Predict positions
            pbfShader.SetBuffer(kernelPredict, "Particles", particleBuffer);
            pbfShader.SetBuffer(kernelPredict, "PredictedPositionsBuffer", predictedPositionsBuffer);
            pbfShader.SetBuffer(kernelPredict, "Colors", debugColorBuffer);
            pbfShader.SetBuffer(kernelPredict, "ObstaclesBuffer", obstaclesBuffer);
            pbfShader.Dispatch(kernelPredict, threadGroupsParticles, 1, 1);

            // Optimization : we now build the grid outside of the solver
            pbfShader.SetBuffer(kernelClear, "NInCell", nInCellBuffer);
            pbfShader.Dispatch(kernelClear, threadGroupsGrid, 1, 1);

            pbfShader.SetBuffer(kernelBuild, "PredictedPositionsBuffer", predictedPositionsBuffer);
            pbfShader.SetBuffer(kernelBuild, "ParticlesInCell", particlesInCellBuffer);
            pbfShader.SetBuffer(kernelBuild, "NInCell", nInCellBuffer);
            pbfShader.Dispatch(kernelBuild, threadGroupsParticles, 1, 1);

            for (int iter = 0; iter < solverIterations; iter++)
            {
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
                pbfShader.SetBuffer(kernelApplyConstraints, "ObstaclesBuffer", obstaclesBuffer);
                pbfShader.SetBuffer(kernelApplyConstraints, "CollisionFeedbackBuffer", collisionFeedbackBuffer);
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

        if (!isWaitingForFeedback) {
            isWaitingForFeedback = true;
            AsyncGPUReadback.Request(collisionFeedbackBuffer, (request) => {
                if (request.hasError || !Application.isPlaying) { isWaitingForFeedback = false; return; }

                var nativeArray = request.GetData<uint>();

                // TODO: change what happens when enemies get hit
                for (int i = 1; i < max_obstacles; i++) {
                    if (nativeArray[i] > 40 && activeObstacles[i] != null) {
                        UnityEngine.Debug.Log($"[Poseidon] Destruction pf {activeObstacles[i].name}.");
                        Destroy(activeObstacles[i].gameObject);
                        activeObstacles[i] = null;
                    }
                }

                System.Array.Clear(feedbackData, 0, feedbackData.Length);
                collisionFeedbackBuffer.SetData(feedbackData);
                isWaitingForFeedback = false;
            });
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
        if (sdfValuesBuffer != null) { sdfValuesBuffer.Release(); sdfValuesBuffer = null;}
        if (sdfGradientBuffer != null) {  sdfGradientBuffer.Release(); sdfGradientBuffer = null; }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = UnityEngine.Color.blue;
        Vector2 tl = new Vector2(-sdfSize.x, sdfSize.y);
        Vector2 br = new Vector2(sdfSize.x, -sdfSize.y);
        Gizmos.DrawLine(sdfSize,tl);
        Gizmos.DrawLine(sdfSize, br);
        Gizmos.DrawLine(-sdfSize, tl);
        Gizmos.DrawLine(-sdfSize, br);
    }
}
