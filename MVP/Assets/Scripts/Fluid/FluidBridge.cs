using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;
using Random = UnityEngine.Random;
using UnityEngine.InputSystem;
using System.Numerics;
using System.Collections.Generic;
using Vector2 = UnityEngine.Vector2;
using Unity.VisualScripting;

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
    [SerializeField] private InputActionReference throwWaterAction;

    [Header("Water Throwing")]
    [SerializeField] private uint particlesPerFrame = 5;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnRadius = .5f;
    [SerializeField] private float initialVelocity = 15f;
    [SerializeField] private float sprayAngle = 0.0f;

    [Header("Simulation Settings")]
    // Capacity of buffers
    [SerializeField] private uint maxTemporaryParticleCount = 2000;
    private uint maxParticleCount = 0;
    private static uint everlastingParticleCount = 0;
    // Actual number of particles
    private uint particleCount;
    private uint particleIdx;
    [SerializeField] private int gridCellCount = 2048;
    [SerializeField] private int maxParticlesPerCell = 128;
    [SerializeField] private float cellSize = 1.0f;
    [SerializeField] private float smoothingRadius = 1.0f;
    [SerializeField] private Vector2 gravity = new Vector2(0f, -9.81f);
    [SerializeField] private float vorticity_epsilon = 0.01f;
    [SerializeField] private float viscosity_c = 0.01f;

    [SerializeField] private uint solverIterations = 3;
    [SerializeField] private float rho_0 = 1000.0f;

    [SerializeField] private float surfaceTension_c = 0.05f;
    [SerializeField] private float tensionBreakingThreshold = 0.75f;
    [SerializeField] private float particleTimeout = 10f;

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
    private GraphicsBuffer creationTimeBuffer;
    private GraphicsBuffer hitBuffer;
    private GraphicsBuffer predictedPositionsBuffer;
    private GraphicsBuffer lambdaBuffer;
    private GraphicsBuffer vorticityBuffer;

    private GraphicsBuffer particlesInCellBuffer;
    private GraphicsBuffer nInCellBuffer;

    private float fixedDeltaTime = 0.016f; // 60Hz
    private float accumulator = 0f;

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

    [Header("Water sources")]
    private static List<WaterSource> waterSources = new List<WaterSource>();


    [Header("Static walls & Signed Distance Field")]
    [SerializeField] private Vector2 sdfSize = new Vector2(20f, 20f);
    [SerializeField] private int sdfResolution = 1;

    private GraphicsBuffer sdfValuesBuffer;
    private GraphicsBuffer sdfGradientBuffer;
    private float[] sdfValues;
    private Vector2[] sdfGradientValues;


    private bool isThrowingWater;
    public static int RegisterObstacle(Enemy enemy)
    {
        for (int i = 1; i < max_obstacles; i++)
        {
            if (activeObstacles[i] == null)
            {
                activeObstacles[i] = enemy;
                currentCount++;
                return i;
            }
        }
        return -1;
    }

    public static int RegisterWaterSource(WaterSource waterSource)
    {
        int idx = waterSources.Count;
        waterSources.Add(waterSource);
        everlastingParticleCount += waterSource.getNbOfParticles();
        return idx;
    }

    public static void UnregisterObstacle(int id)
    {
        if (id > 0 && id < max_obstacles) activeObstacles[id] = null;
    }

    void OnEnable()
    {
        if (throwWaterAction != null)
        {
            // Quand on commence à appuyer
            throwWaterAction.action.started += OnThrowWaterInput;
            // Quand on relâche
            throwWaterAction.action.canceled += OnThrowWaterInput;

            throwWaterAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (throwWaterAction != null)
        {
            throwWaterAction.action.started -= OnThrowWaterInput;
            throwWaterAction.action.canceled -= OnThrowWaterInput;

            throwWaterAction.action.Disable();
        }
    }

    private void OnThrowWaterInput(InputAction.CallbackContext context)
    {
        if (context.started) isThrowingWater = true;
        if (context.canceled) isThrowingWater = false;
    }


    private void CreateEverlastingParticles()
    {
        // Create everlasting particles
        everlastingParticleCount = 0;
        foreach (WaterSource waterSource in waterSources)
        {
            uint currentParticleCount = waterSource.getNbOfParticles();
            FluidParticle[] everlastingWater = new FluidParticle[currentParticleCount];
            for (int i = 0; i < currentParticleCount; i++)
            {
                everlastingWater[i].position = new Vector2(waterSource.transform.position.x, waterSource.transform.position.y) + waterSource.getSpawnRadius() * Random.insideUnitCircle;
                everlastingWater[i].velocity = Random.insideUnitCircle;
            }

            particleBuffer.SetData(everlastingWater, 0, (int)everlastingParticleCount, (int)currentParticleCount);

            everlastingParticleCount += currentParticleCount;
        }
        particleCount = everlastingParticleCount;
        particleIdx = everlastingParticleCount;
    }


    void Start()
    {
        if (vfxGraph == null || pbfShader == null || throwWaterAction == null)
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


        // Get max number of particles
        everlastingParticleCount = 0;
        foreach (WaterSource waterSource in waterSources) everlastingParticleCount += waterSource.getNbOfParticles();
        maxParticleCount = maxTemporaryParticleCount + everlastingParticleCount;


        particleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)maxParticleCount, Marshal.SizeOf(typeof(FluidParticle))); // equivalent to glGenBuffers() and glBindBuffer() in OpenGL

        hitBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)maxParticleCount, Marshal.SizeOf(typeof(int)));
        creationTimeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)maxParticleCount, Marshal.SizeOf(typeof(float)));


        predictedPositionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)maxParticleCount, Marshal.SizeOf(typeof(Vector2)));
        lambdaBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)maxParticleCount, Marshal.SizeOf(typeof(float)));
        vorticityBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)maxParticleCount, Marshal.SizeOf(typeof(float)));

        int totalGridEntries = gridCellCount * maxParticlesPerCell;
        particlesInCellBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalGridEntries, Marshal.SizeOf(typeof(uint)));

        nInCellBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gridCellCount, Marshal.SizeOf(typeof(uint)));


        int aabbStride = Marshal.SizeOf(typeof(GPUObstacleAABB));
        obstaclesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, max_obstacles, aabbStride);
        obstacleData = new GPUObstacleAABB[max_obstacles];

        collisionFeedbackBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, max_obstacles, sizeof(uint));
        feedbackData = new uint[max_obstacles];
        collisionFeedbackBuffer.SetData(feedbackData);


        // Create the Signed Distance Field
        AxisAlignedRectWall[] walls = Object.FindObjectsByType<AxisAlignedRectWall>(FindObjectsSortMode.None);
        sdfValues = new float[sdfResolution * sdfResolution];
        sdfGradientValues = new Vector2[sdfResolution * sdfResolution];


        int sdfHalfResolution = sdfResolution / 2;
        Vector2 pixelStep = new Vector2((sdfSize.x * 2f) / sdfResolution, (sdfSize.y * 2f) / sdfResolution);

        for (int i = 0; i < sdfResolution; i++)
        {
            for (int j = 0; j < sdfResolution; j++)
            {
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

                        // Keep the nearest wall
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

        CreateEverlastingParticles();


        // VFX PARAMS
        vfxGraph.SetUInt("maxParticleCount", maxParticleCount);
        vfxGraph.SetUInt("particleCount", particleCount);
        vfxGraph.SetGraphicsBuffer("ParticleBuffer", particleBuffer);
        vfxGraph.SetGraphicsBuffer("CreationTimeBuffer", creationTimeBuffer);


        // SHADER PARAMS
        pbfShader.SetInt("sdfResolution", sdfResolution);
        pbfShader.SetVector("sdfSize", sdfSize);

        pbfShader.SetInt("EverlastingParticleCount", (int)everlastingParticleCount);
        pbfShader.SetInt("GridCellCount", gridCellCount);
        pbfShader.SetInt("MaxParticlesPerCell", maxParticlesPerCell);
        pbfShader.SetFloat("cellSize", cellSize);
        pbfShader.SetFloat("dt", fixedDeltaTime);
        pbfShader.SetFloat("smoothingRadius", smoothingRadius);
        pbfShader.SetVector("gravity", gravity);
        pbfShader.SetFloat("vorticity_epsilon", vorticity_epsilon);
        pbfShader.SetFloat("viscosity_c", viscosity_c);
        pbfShader.SetFloat("rho_0", rho_0);
        pbfShader.SetFloat("particleTimeout", particleTimeout);


        // Setting buffers
        pbfShader.SetBuffer(kernelPredict, "Particles", particleBuffer);
        pbfShader.SetBuffer(kernelPredict, "PredictedPositionsBuffer", predictedPositionsBuffer);
        pbfShader.SetBuffer(kernelPredict, "ObstaclesBuffer", obstaclesBuffer);
        pbfShader.SetBuffer(kernelPredict, "CollisionFeedbackBuffer", collisionFeedbackBuffer);
        pbfShader.SetBuffer(kernelPredict, "SdfValuesBuffer", sdfValuesBuffer);
        pbfShader.SetBuffer(kernelPredict, "SdfGradientBuffer", sdfGradientBuffer);
        pbfShader.SetBuffer(kernelPredict, "CreationTimeBuffer", creationTimeBuffer);
        pbfShader.SetBuffer(kernelPredict, "Hit", hitBuffer);


        pbfShader.SetBuffer(kernelClear, "NInCell", nInCellBuffer);

        pbfShader.SetBuffer(kernelBuild, "PredictedPositionsBuffer", predictedPositionsBuffer);
        pbfShader.SetBuffer(kernelBuild, "ParticlesInCell", particlesInCellBuffer);
        pbfShader.SetBuffer(kernelBuild, "NInCell", nInCellBuffer);
        pbfShader.SetBuffer(kernelBuild, "CreationTimeBuffer", creationTimeBuffer);

        pbfShader.SetBuffer(kernelDensity, "PredictedPositionsBuffer", predictedPositionsBuffer);
        pbfShader.SetBuffer(kernelDensity, "ParticlesInCell", particlesInCellBuffer);
        pbfShader.SetBuffer(kernelDensity, "NInCell", nInCellBuffer);
        pbfShader.SetBuffer(kernelDensity, "LambdaBuffer", lambdaBuffer);
        pbfShader.SetBuffer(kernelDensity, "CreationTimeBuffer", creationTimeBuffer);

        pbfShader.SetBuffer(kernelApplyConstraints, "PredictedPositionsBuffer", predictedPositionsBuffer);
        pbfShader.SetBuffer(kernelApplyConstraints, "LambdaBuffer", lambdaBuffer);
        pbfShader.SetBuffer(kernelApplyConstraints, "ParticlesInCell", particlesInCellBuffer);
        pbfShader.SetBuffer(kernelApplyConstraints, "NInCell", nInCellBuffer);
        pbfShader.SetBuffer(kernelApplyConstraints, "ObstaclesBuffer", obstaclesBuffer);
        pbfShader.SetBuffer(kernelApplyConstraints, "CollisionFeedbackBuffer", collisionFeedbackBuffer);
        pbfShader.SetBuffer(kernelApplyConstraints, "CreationTimeBuffer", creationTimeBuffer);
        pbfShader.SetBuffer(kernelApplyConstraints, "Hit", hitBuffer);

        pbfShader.SetBuffer(kernelVorticity, "Vorticity", vorticityBuffer);
        pbfShader.SetBuffer(kernelVorticity, "Particles", particleBuffer);
        pbfShader.SetBuffer(kernelVorticity, "PredictedPositionsBuffer", predictedPositionsBuffer);
        pbfShader.SetBuffer(kernelVorticity, "ParticlesInCell", particlesInCellBuffer);
        pbfShader.SetBuffer(kernelVorticity, "NInCell", nInCellBuffer);
        pbfShader.SetBuffer(kernelVorticity, "CreationTimeBuffer", creationTimeBuffer);

        pbfShader.SetBuffer(kernelUpdateParticles, "Vorticity", vorticityBuffer);
        pbfShader.SetBuffer(kernelUpdateParticles, "Particles", particleBuffer);
        pbfShader.SetBuffer(kernelUpdateParticles, "PredictedPositionsBuffer", predictedPositionsBuffer);
        pbfShader.SetBuffer(kernelUpdateParticles, "ParticlesInCell", particlesInCellBuffer);
        pbfShader.SetBuffer(kernelUpdateParticles, "NInCell", nInCellBuffer);
        pbfShader.SetBuffer(kernelUpdateParticles, "ObstaclesBuffer", obstaclesBuffer);
        pbfShader.SetBuffer(kernelUpdateParticles, "CollisionFeedbackBuffer", collisionFeedbackBuffer);
        pbfShader.SetBuffer(kernelUpdateParticles, "Hit", hitBuffer);
        pbfShader.SetBuffer(kernelUpdateParticles, "CreationTimeBuffer", creationTimeBuffer);

    }

    void Update()
    {
        accumulator += Time.deltaTime;


        System.Array.Clear(obstacleData, 0, obstacleData.Length);

        PlayerScript player = Object.FindFirstObjectByType<PlayerScript>();
        if (player != null)
        {
            Collider2D col = player.GetComponent<Collider2D>();
            if (col != null)
            {
                obstacleData[0].minPos = col.bounds.min;
                obstacleData[0].maxPos = col.bounds.max;
            }
        }

        for (int i = 1; i < max_obstacles; i++)
        {
            if (activeObstacles[i] != null)
            {
                Collider2D col = activeObstacles[i].GetComponent<Collider2D>();
                if (col != null)
                {
                    obstacleData[i].minPos = col.bounds.min;
                    obstacleData[i].maxPos = col.bounds.max;
                }
            }
        }

        obstaclesBuffer.SetData(obstacleData);

        // We check if the player is throwing water
        if (isThrowingWater)
            ThrowWater(player.getFacingDirection());


        // We reload the number of particles to the VFX, as it can change
        vfxGraph.SetUInt("particleCount", particleCount);

        pbfShader.SetFloat("dt", fixedDeltaTime);
        pbfShader.SetFloat("smoothingRadius", smoothingRadius);
        pbfShader.SetVector("gravity", gravity);
        pbfShader.SetFloat("vorticity_epsilon", vorticity_epsilon);
        pbfShader.SetFloat("viscosity_c", viscosity_c);
        pbfShader.SetFloat("rho_0", rho_0);
        pbfShader.SetInt("ObstacleCount", currentCount);
        pbfShader.SetFloat("Time", Time.time);




        // TODO ? : rename these
        int threadGroupsParticles = Mathf.CeilToInt((particleCount) / 64f);
        int threadGroupsGrid = Mathf.CeilToInt(gridCellCount / 64f);

        int maxStepsPerFrame = 2;
        int currentSteps = 0;

        if (threadGroupsParticles == 0)
        {
            accumulator = 0;
            return;
        }
        ;
        while (accumulator >= fixedDeltaTime && currentSteps < maxStepsPerFrame)
        {
            currentSteps++;

            // send constant parameters to the cbuffer
            pbfShader.SetInt("ParticleCount", (int)particleCount);
            pbfShader.SetInt("ParticleIdx", (int)particleIdx);
            pbfShader.SetFloat("surface_tension_c", surfaceTension_c);
            pbfShader.SetFloat("tension_breaking_treshold", tensionBreakingThreshold);

            timer.Restart();

            // Predict positions
            pbfShader.Dispatch(kernelPredict, threadGroupsParticles, 1, 1);

            pbfShader.Dispatch(kernelClear, threadGroupsGrid, 1, 1);

            pbfShader.Dispatch(kernelBuild, threadGroupsParticles, 1, 1);

            for (int iter = 0; iter < solverIterations; iter++)
            {
                // Density and Lambda calculation

                pbfShader.Dispatch(kernelDensity, threadGroupsParticles, 1, 1);

                pbfShader.Dispatch(kernelApplyConstraints, threadGroupsParticles, 1, 1);
            }


            pbfShader.Dispatch(kernelVorticity, threadGroupsParticles, 1, 1);


            pbfShader.Dispatch(kernelUpdateParticles, threadGroupsParticles, 1, 1);

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

        if (!isWaitingForFeedback)
        {
            isWaitingForFeedback = true;
            AsyncGPUReadback.Request(collisionFeedbackBuffer, (request) =>
            {
                if (request.hasError || !Application.isPlaying) { isWaitingForFeedback = false; return; }

                var nativeArray = request.GetData<uint>();

                for (int i = 1; i < max_obstacles; i++)
                {
                    if (activeObstacles[i] != null) {
                        uint hits = nativeArray[i];
                        activeObstacles[i].GenerateSteam(hits);
                        if (hits > 5)
                        {
                            // Damage enemy
                            UnityEngine.Debug.Log($"Enenmy {i} touched");
                            if (activeObstacles[i].InflictDamage(1f))
                            {
                                activeObstacles[i] = null;
                            }
                        }
                    }
                }

                System.Array.Clear(feedbackData, 0, feedbackData.Length);
                collisionFeedbackBuffer.SetData(feedbackData);
                isWaitingForFeedback = false;
            });
        }
    }

    void ThrowWater(bool isFacingRight)
    {
        // Test if we reached the end of the buffer
        uint spawnableBeforeLooping = System.Math.Min(maxParticleCount - particleIdx, particlesPerFrame);
        uint spawnableAfterLooping = particlesPerFrame - spawnableBeforeLooping;

        // Spawn point
        Vector2 baseSpawnPos = spawnPoint.position;
        Vector2 baseSpawnDir = spawnPoint.right;
        if (!isFacingRight)
            baseSpawnDir.x = -baseSpawnDir.x;

        // Particle properties
        FluidParticle[] spawnedWater = new FluidParticle[particlesPerFrame];
        for (int i = 0; i < particlesPerFrame; i++)
        {
            spawnedWater[i].position = baseSpawnPos + Random.insideUnitCircle * spawnRadius;
            spawnedWater[i].velocity = (baseSpawnDir + Random.insideUnitCircle * sprayAngle) * initialVelocity;
        }

        float[] creationTime = new float[particlesPerFrame];
        System.Array.Fill(creationTime, Time.time);

        // Spawning particles before looping
        if (spawnableBeforeLooping > 0)
        {
            particleBuffer.SetData(spawnedWater, 0, (int)particleIdx, (int)spawnableBeforeLooping);
            creationTimeBuffer.SetData(creationTime, 0, (int)particleIdx, (int)spawnableBeforeLooping);
        }

        // Spawn particles after looping
        if (spawnableAfterLooping > 0)
        {
            particleBuffer.SetData(spawnedWater, (int)spawnableAfterLooping, (int)everlastingParticleCount, (int)spawnableAfterLooping);
            creationTimeBuffer.SetData(creationTime, (int)spawnableAfterLooping, (int)everlastingParticleCount, (int)spawnableAfterLooping);
        }


        // Increase the number of particles (if needed) and increase the particle idx
        if (particleCount < maxParticleCount) particleCount += spawnableBeforeLooping;
        if (spawnableAfterLooping > 0) particleIdx = spawnableAfterLooping;
        else particleIdx += spawnableBeforeLooping;
        if (particleIdx == maxParticleCount) particleIdx = everlastingParticleCount;

    }

    void OnDestroy()
    {
        if (particleBuffer != null) { particleBuffer.Release(); particleBuffer = null; }
        if (creationTimeBuffer != null) { creationTimeBuffer.Release(); creationTimeBuffer = null; }
        if (predictedPositionsBuffer != null) { predictedPositionsBuffer.Release(); predictedPositionsBuffer = null; }
        if (lambdaBuffer != null) { lambdaBuffer.Release(); lambdaBuffer = null; }
        if (vorticityBuffer != null) { vorticityBuffer.Release(); vorticityBuffer = null; }
        if (particlesInCellBuffer != null) { particlesInCellBuffer.Release(); particlesInCellBuffer = null; }
        if (nInCellBuffer != null) { nInCellBuffer.Release(); nInCellBuffer = null; }
        if (sdfValuesBuffer != null) { sdfValuesBuffer.Release(); sdfValuesBuffer = null; }
        if (sdfGradientBuffer != null) { sdfGradientBuffer.Release(); sdfGradientBuffer = null; }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = UnityEngine.Color.blue;
        Vector2 tl = new Vector2(-sdfSize.x, sdfSize.y);
        Vector2 br = new Vector2(sdfSize.x, -sdfSize.y);
        Gizmos.DrawLine(sdfSize, tl);
        Gizmos.DrawLine(sdfSize, br);
        Gizmos.DrawLine(-sdfSize, tl);
        Gizmos.DrawLine(-sdfSize, br);
    }
}
