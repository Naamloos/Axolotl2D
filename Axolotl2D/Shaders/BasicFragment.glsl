#version 330 core

uniform sampler2D uTexture;

// Receive the input from the vertex shader in an attribute
in vec2 frag_texCoords;
in vec4 frag_color;

out vec4 out_color;

void main()
{
    // This will allow us to see the texture coordinates in action!
    out_color = texture(uTexture, frag_texCoords) * frag_color;
}
