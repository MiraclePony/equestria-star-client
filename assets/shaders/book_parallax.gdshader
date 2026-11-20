// 入口立体书：左页隧道书的鼠标视差。
// depth_map：白 = 靠前（台口、书页、桌面，不动），黑 = 深远（天空、远山，移动最多）。
// offset：UV 单位的最大位移（由 BookParallax 按鼠标位置平滑给出）。
shader_type canvas_item;

uniform sampler2D depth_map : filter_linear, repeat_disable;
uniform vec2 offset = vec2(0.0);

void fragment() {
	// 迭代几次求“位移后的深度”，减少层边缘的撕裂。
	float d = texture(depth_map, UV).r;
	vec2 uv = UV;
	for (int i = 0; i < 4; i++) {
		uv = UV - offset * (1.0 - d);
		d = texture(depth_map, uv).r;
	}
	COLOR = texture(TEXTURE, UV - offset * (1.0 - d));
}
