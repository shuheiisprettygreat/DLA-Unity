using Unity.Mathematics;

public struct DirectedPoint {
    public float3 position;
    public float3 tangent;
    public float3 acceleration;
    public int isActive;
    public int life;
}

public enum NumParticle {
    NUM_1K = 1024,
    NUM_2K = 1024 * 2, 
    NUM_4K = 1024 * 4,
    NUM_16K = 1024 * 16,
    NUM_32K = 1024 * 32,
    NUM_64K = 1024 * 64,
    NUM_128K = 1024 * 128,
    NUM_256K = 1024 * 256,
    NUM_512K = 1024 * 512,
    NUM_1M = 1048576,
    NUM_2M = 1048576 * 2,
    NUM_4M = 1048576 * 4,
}