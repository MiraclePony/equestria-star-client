shader_type canvas_item;
render_mode unshaded, blend_disabled;

// LDR canvas pass: Viewport.use_hdr_2d MUST be false. Screen texture is already
// display-referred after 3D tone mapping. Do not add source_color or gamma here.
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;
uniform float strength : hint_range(0.0, 1.0) = 0.50;
uniform float saturation : hint_range(0.0, 1.4) = 1.02;
uniform float contrast : hint_range(0.8, 1.2) = 1.025;
uniform vec3 shadow_shift = vec3(-0.004, -0.001, 0.012);
uniform vec3 highlight_shift = vec3(0.006, 0.003, -0.004);

void fragment() {
	vec4 src = textureLod(screen_texture, SCREEN_UV, 0.0);
	float y = dot(src.rgb, vec3(0.2126, 0.7152, 0.0722));
	vec3 c = mix(vec3(y), src.rgb, saturation);
	c = (c - vec3(0.5)) * contrast + vec3(0.5);
	float low = 1.0 - smoothstep(0.12, 0.55, y);
	float high = smoothstep(0.50, 0.92, y);
	// Protect blacks and white paper; no haze, sharpening, outlines, blur or noise.
	float protection = smoothstep(0.02, 0.12, y) * (1.0 - smoothstep(0.88, 1.0, y));
	c += (shadow_shift * low + highlight_shift * high) * protection;
	COLOR = vec4(mix(src.rgb, clamp(c, 0.0, 1.0), strength), src.a);
}
