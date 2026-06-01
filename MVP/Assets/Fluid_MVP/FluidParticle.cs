using UnityEngine;
using Unity.Mathematics;
using System.Runtime.InteropServices;
using UnityEngine.VFX;

[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
[StructLayout(LayoutKind.Sequential)] // imposes the compiler to keep the order we defined
public struct FluidParticle // using a struct because it's a value type (contigous block in memory)
{
    public Vector2 position;
    public Vector2 velocity;
}
