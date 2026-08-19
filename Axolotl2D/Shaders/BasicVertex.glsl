#version 330 core

layout (location = 0) in vec3 aPosition;
// Add a new input attribute for the texture coordinates
layout (location = 1) in vec2 aTextureCoord;
layout (location = 2) in vec4 aColor;

// Add an output variable to pass the texture coordinate to the fragment shader
// This variable stores the data that we want to be received by the fragment
out vec2 frag_texCoords;
out vec4 frag_color;

void main()
{
    gl_Position = vec4(aPosition, 1.0);
    // Assigin the texture coordinates without any modification to be recived in the fragment
    frag_texCoords = aTextureCoord;
    frag_color = aColor;
}
