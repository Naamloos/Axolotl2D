#version 330 core
const int MAX_LIGHTS = 16;
const int MAX_SHADOW_EDGES = 64;
struct Light2D
{
    vec2 position;
    vec2 direction;
    vec3 color;
    float intensity;
    float radius;
    float height;
    float falloff;
    int kind;
    float spotCos;
    int layerMask;
    int castsShadows;
};
uniform sampler2D uTexture;
uniform sampler2D uNormalMap;
uniform int uUseLighting;
uniform vec3 uAmbient;
uniform int uLightingLayer;
uniform int uLightCount;
uniform Light2D uLights[MAX_LIGHTS];
uniform int uShadowEdgeCount;
uniform vec4 uShadowEdges[MAX_SHADOW_EDGES];
uniform int uShadowMasks[MAX_SHADOW_EDGES];
in vec2 frag_texCoords;
in vec4 frag_color;
in vec2 frag_worldPosition;
in vec2 frag_tangent;
in vec2 frag_bitangent;
in vec2 frag_normalTexCoords;
out vec4 out_color;
float cross2(vec2 left, vec2 right) { return left.x * right.y - left.y * right.x; }
bool segmentsIntersect(vec2 a, vec2 b, vec2 c, vec2 d)
{
    vec2 r = b - a;
    vec2 s = d - c;
    float denominator = cross2(r, s);
    if (abs(denominator) < 0.00001) return false;
    float t = cross2(c - a, s) / denominator;
    float u = cross2(c - a, r) / denominator;
    return t > 0.0001 && t < 0.9999 && u >= 0.0 && u <= 1.0;
}
bool isShadowed(vec2 lightPosition)
{
    for (int edgeIndex = 0; edgeIndex < uShadowEdgeCount; edgeIndex++)
    {
        if ((uShadowMasks[edgeIndex] & uLightingLayer) == 0) continue;
        vec4 edge = uShadowEdges[edgeIndex];
        if (segmentsIntersect(lightPosition, frag_worldPosition, edge.xy, edge.zw)) return true;
    }
    return false;
}
void main()
{
    vec4 albedo = texture(uTexture, frag_texCoords) * frag_color;
    if (uUseLighting == 0) { out_color = albedo; return; }
    vec3 tangentNormal = texture(uNormalMap, frag_normalTexCoords).rgb * 2.0 - 1.0;
    vec3 normal = normalize(vec3(
        frag_tangent * tangentNormal.x + frag_bitangent * tangentNormal.y,
        tangentNormal.z));
    vec3 illumination = uAmbient;
    for (int lightIndex = 0; lightIndex < uLightCount; lightIndex++)
    {
        Light2D light = uLights[lightIndex];
        if ((light.layerMask & uLightingLayer) == 0) continue;
        vec2 planar = light.position - frag_worldPosition;
        float distanceToLight = length(planar);
        if (distanceToLight >= light.radius) continue;
        if (light.kind == 1)
        {
            vec2 fromLight = normalize(frag_worldPosition - light.position);
            if (dot(fromLight, normalize(light.direction)) < light.spotCos) continue;
        }
        if (light.castsShadows != 0 && isShadowed(light.position)) continue;
        float attenuation = pow(max(0.0, 1.0 - distanceToLight / light.radius), light.falloff);
        vec3 toLight = normalize(vec3(planar, light.height));
        float diffuse = max(0.0, dot(normal, toLight));
        illumination += light.color * light.intensity * attenuation * diffuse;
    }
    out_color = vec4(albedo.rgb * illumination, albedo.a);
}
