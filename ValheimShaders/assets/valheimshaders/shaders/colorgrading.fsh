#version 330 core

uniform sampler2D sceneTex;
uniform sampler2D lutTex;
uniform float lutStrength;

in vec2 texCoord;
out vec4 outColor;

// 256x16 LUT: 16 slices of 16x16, B selects slice, R=x within slice, G=y
vec3 applyLut(vec3 color) {
    float b = color.b * 15.0;
    float bFloor = floor(b);
    float bFrac = b - bFloor;

    // Half-pixel offsets for correct texel centre sampling
    float slicePixel = 0.5 / 256.0;
    float sliceOffset = 0.5 / 16.0;

    vec2 uv1 = vec2((bFloor + color.r * (15.0 / 16.0)) / 16.0 + slicePixel,
                    color.g * (15.0 / 16.0) + sliceOffset);
    vec2 uv2 = vec2(((bFloor + 1.0) + color.r * (15.0 / 16.0)) / 16.0 + slicePixel, uv1.y);

    return mix(texture(lutTex, uv1).rgb, texture(lutTex, uv2).rgb, bFrac);
}

void main(void) {
    vec3 scene = texture(sceneTex, texCoord).rgb;
    vec3 graded = applyLut(clamp(scene, 0.0, 1.0));
    outColor = vec4(mix(scene, graded, lutStrength), 1.0);
}
