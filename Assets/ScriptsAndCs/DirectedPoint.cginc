#pragma once

struct DirectedPoint {
    float3 position;
    float3 tangent;
    float3 acceleration;
    int isActive;
    int life;
};