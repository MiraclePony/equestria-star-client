shader_type spatial;
render_mode diffuse_lambert, specular_disabled, cull_back;

uniform vec4 paper_color : source_color = vec4(0.6, 0.8, 0.5, 1.0);
// Red vertex channel convention: 1=open, 0=occluded. Off by default because
// existing models may already use vertex RGB as their color palette.
uniform float vertex_ao_strength : hint_range(0.0, 1.0) = 0.0;
uniform float grain_amount : hint_range(0.0, 0.03) = 0.0;
uniform float grain_scale : hint_range(1.0, 100.0) = 35.0;
varying vec3 object_position;

float hash31(vec3 p) {
	p = fract(p * 0.1031);
	p += dot(p, p.yzx + 33.33);
	return fract((p.x + p.y) * p.z);
}

void vertex() {
	object_position = VERTEX;
}

void fragment() {
	// Stable in object coordinates. Grain fades when its cells become subpixel.
	vec3 p = object_position * grain_scale;
	float footprint = max(length(dFdx(p)), length(dFdy(p)));
	float fade = 1.0 - smoothstep(0.35, 0.90, footprint);
	float noise_value = hash31(floor(p)) - 0.5;
	float occlusion = mix(1.0, mix(0.62, 1.0, COLOR.r), vertex_ao_strength);
	ALBEDO = paper_color.rgb * occlusion * (1.0 + noise_value * grain_amount * fade);
	ROUGHNESS = 0.88;
	METALLIC = 0.0;
	SPECULAR = 0.0;
	// Opaque: do not write ALPHA. Real folds and edge thickness belong in geometry.
}
