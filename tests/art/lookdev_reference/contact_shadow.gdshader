shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back;

// Optional small near-ground contact patch, NOT a substitute for sun shadows.
// PlaneMesh UVs required; place just above the receiver, and disable casting.
uniform vec4 shadow_color : source_color = vec4(0.18, 0.15, 0.28, 1.0);
uniform float opacity : hint_range(0.0, 0.30) = 0.13;
void fragment() {
	float r = length((UV - vec2(0.5)) * 2.0);
	float mask = 1.0 - smoothstep(0.10, 1.0, r);
	ALBEDO = shadow_color.rgb;
	ALPHA = mask * opacity;
}
