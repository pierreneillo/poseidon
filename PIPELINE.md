┌─────────────────────────────────────────────────────────────────────────────┐
│ 1. THE DATA CONTRACT (Shared File)                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│ File     : FluidParticle.cs (and HLSL equivalent)                           │
│ Hardware : CPU & GPU (Acts as a template for both)                          │
│ Action   : Defines the strict memory footprint/layout of a particle.        │
│            Ex: `struct { float2 position; float2 velocity; }`               │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │
            Defines the size of the    │ allocated memory blocks
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ 2. THE ORCHESTRATOR (Entry point for each frame)                            │
├─────────────────────────────────────────────────────────────────────────────┤
│ File     : FluidBridgeSystem.cs                                             │
│ Hardware : CPU (Unity ECS)                                                  │
│ Action   : - Reads player inputs (click, reversed gravity, etc.).           │
│            - Allocates/Maintains the "GraphicsBuffer" in video memory.      │
│            - Sends variables (Time.deltaTime) to the Compute Shader.        │
│            - Commands the GPU to execute the Compute Shader (Dispatch).     │
│            - Gives the GraphicsBuffer address to the VFX Graph file.        │
└───────┬──────────────────────────────┬──────────────────────────────┬───────┘
        │                              │                              │
        │ [Data] Allocates in VRAM     │ [Data] SetBuffer()           │ [Data] SetGraphicsBuffer()
        │                              │ & Dispatch()                 │ Passes memory address
        ▼                              ▼                              ▼
┌───────────────┐              ┌────────────────┐             ┌───────────────┐
│ 3. THE MEMORY │◄──[Data]─────┤ 4. THE PHYSICS │             │ 5. THE RENDER │
│               │   Reads old  │                │             │               │
├───────────────┤   positions  ├────────────────┤             ├───────────────┤
│ Object :      │              │ File :         │             │ File :        │
│ GraphicsBuffer│              │ FluidSim.      │             │ WaterVFX.vfx  │
│               │              │ compute        │             │               │
│ Hardware :    │              │                │             │ Hardware :    │
│ VRAM (GPU)    │              │ Hardware :     │             │ GPU (Render)  │
│               │              │ GPU (Compute)  │             │               │
│ Action :      │◄──[Data]─────┤                │             │ Action :      │
│ - Hosts the   │   Writes new │ Action :       │             │ - Reads the   │
│ array of C#   │   positions  │ - Executes PBD.│             │   position of │
│ structs,      │              │ - Builds Hash  │◄─[Data]─────┤   each        │
│ directly      │              │   Grid.        │  Reads the  │   particle.   │
│ inaccessible  │              │ - Applies      │  position   │ - Draws a     │
│ by the CPU.   │              │   pressure &   │             │   very blurry │
│               │              │   vorticity.   │             │   round sprite│
│               │              │ - Moves the    │             │   (Metaball)  │
│               │              │   points.      │             │   at the spot.│
└───────────────┘              └────────────────┘             └───────┬───────┘
                                                                      │
                                     [Data] Generates a raw 2D image  │
                                     containing clusters of blurry    │
                                     circles that add up together.    ▼
                                              ┌───────────────────────────────┐
                                              │ 6. THE LIQUID FILTER          │
                                              ├───────────────────────────────┤
                                              │ File     : MetaballPass.shader│
                                              │ Hardware : GPU (Post-Process) │
                                              │ Action   : - Reads VFX image. │
                                              │            - If pixel alpha   │
                                              │              < 0.5 -> discard │
                                              │            - If pixel alpha   │
                                              │              > 0.5 -> fill    │
                                              │              with solid blue. │
                                              │ Final Render = Water with     │
                                              │ visual surface tension.       │
                                              └───────────────────────────────┘
