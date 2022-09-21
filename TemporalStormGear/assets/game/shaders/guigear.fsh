#version 330 core

in vec2 uv;
in vec2 pos;

out vec4 outColor;

uniform sampler2D tex2d;
uniform float gearCounter;
uniform float stabilityLevel = 0.5;
uniform float shadeYPos;
uniform float hotbarYPos;
uniform float gearHeight;
uniform float temporalStormFactor = 0.0;

#include noise3d.ash

void main () {
	outColor = texture(tex2d, uv);
	if (outColor.a <= 0.01) discard;
	
	vec3 tealCol = vec3(56/255.0, 232/255.0, 182/255.0);
	vec3 tealDarkCol = vec3(80/255.0, 98/255.0, 93/255.0);
	vec3 temporalStormColor = vec3(182/255.0, 108/255.0, 75/255.0);
	
	float noise = abs(gnoise(vec3(pos.x/20, pos.y/20, gearCounter * 0.75)));
	float height = gearHeight - 10;
	
	float relY = (pos.y - hotbarYPos + height * 0.685) / height; // No idea why the 0.68 and not 0.55
	
	float ya = 1 - relY;
	float yb = 0.5 + stabilityLevel/2 + noise/30.0;
	
	if (ya < yb) {
		float b = 1.2f * (max(0, 0.4 - (outColor.r + outColor.g + outColor.b) / 3) + noise/5);
		
		outColor.rgb = mix(outColor.rgb, mix(tealCol, temporalStormColor, temporalStormFactor) - b, clamp((yb - ya)*50, 0, 1));
	}
	
	outColor.rgb *= 1 - max(0, pos.y-shadeYPos)/15;
	outColor.a *= 1;//0.5;
	
	//outColor.rgb = vec3(relY);
	
}