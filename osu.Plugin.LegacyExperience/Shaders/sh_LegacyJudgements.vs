#ifndef LEGACYJUDGEMENTS_VS
#define LEGACYJUDGEMENTS_VS

layout(location = 0) in vec2 m_Position;
layout(location = 1) in vec4 m_Color;
layout(location = 2) in float m_Time;

layout(location = 0) out vec4 v_Color;

layout(std140, set = 0, binding = 0) uniform m_JudgementsParameters
{
    float g_Time;
    float g_SparkLifetime;
};

void main(void)
{
    float elapsed = g_Time - m_Time;
    float progress = clamp(elapsed / g_SparkLifetime, 0.0, 1.0);

    float alpha = m_Color.w * (1.0 - progress);

    v_Color = vec4(m_Color.xyz, alpha);
    gl_Position = g_ProjMatrix * vec4(m_Position, 1.0, 1.0);
}

#endif