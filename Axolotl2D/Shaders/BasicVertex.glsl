#version 330 core
layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec2 aTextureCoord;
layout (location = 2) in vec4 aColor;
layout (location = 3) in vec2 aWorldPosition;
layout (location = 4) in vec2 aTangent;
layout (location = 5) in vec2 aBitangent;
layout (location = 6) in vec2 aNormalTextureCoord;
out vec2 frag_texCoords;
out vec4 frag_color;
out vec2 frag_worldPosition;
out vec2 frag_tangent;
out vec2 frag_bitangent;
out vec2 frag_normalTexCoords;
void main()
{
    gl_Position = vec4(aPosition, 1.0);
    frag_texCoords = aTextureCoord;
    frag_color = aColor;
    frag_worldPosition = aWorldPosition;
    frag_tangent = aTangent;
    frag_bitangent = aBitangent;
    frag_normalTexCoords = aNormalTextureCoord;
}
