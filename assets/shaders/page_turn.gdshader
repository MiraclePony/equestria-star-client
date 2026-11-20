// 入口立体书：程序化翻页。
// 一张书页绕书脊旋转 angle（0→π）：正面 = 旧画面的这一页，背面 = 新一页对侧的书本背景。
// 页面翻起时带轻微透视（外缘离观众更近、更高）和弯曲；翻起的那一侧露出新画面（透明），
// 另一侧在被盖住之前仍显示旧画面；页缘在下面投一道柔和阴影。
// 几何量都用“舞台 UV”（1920×1080 书本舞台归一化），屏幕 UV 与舞台 UV 由 stage_origin / stage_size 换算。
shader_type canvas_item;

uniform sampler2D old_tex : filter_linear, repeat_disable;   // 旧画面（整屏截图，屏幕 UV）
uniform sampler2D back_tex : filter_linear, repeat_disable;  // 新一页的书本背景（舞台 UV）
uniform vec2 stage_origin = vec2(0.0);                        // 舞台左上角（屏幕 UV）
uniform vec2 stage_size = vec2(1.0);                          // 舞台大小（屏幕 UV）
uniform float spine = 0.4;        // 旧书脊 x（舞台 UV）
uniform float page_w = 0.55;      // 翻动书页的有符号宽度：>0 右页向左翻，<0 左页向右翻
uniform float back_spine = 0.4;   // 背面贴图里的书脊 x
uniform float back_w = -0.36;     // 背面贴图里对侧书页的有符号宽度
uniform float page_top = 0.03;
uniform float page_bottom = 0.93;
uniform float angle = 0.0;
uniform float fade = 1.0;

void fragment() {
	vec2 suv = (UV - stage_origin) / stage_size;
	float side = sign(page_w);
	float c = cos(angle);
	float s = sin(angle);
	float span = c * page_w;

	// 底层：翻起的一侧露出新画面（透明），另一侧仍是旧画面。
	bool origin_side = (suv.x - spine) * side > 0.0;
	vec4 col = origin_side ? vec4(0.0) : texture(old_tex, UV);

	float mid = (page_top + page_bottom) * 0.5;
	float u = abs(span) > 1e-4 ? (suv.x - spine) / span : -1.0;
	bool on_page = false;
	if (u >= 0.0 && u <= 1.0) {
		float persp = 1.0 + 0.09 * s * u;
		float v = (suv.y - mid) / persp + mid;
		if (v >= page_top && v <= page_bottom) {
			on_page = true;
			float ut = pow(u, 1.0 + 0.35 * s);   // 外缘处压缩 → 看起来像弯曲的纸
			vec4 pc;
			float shade;
			if (c >= 0.0) {
				vec2 o = vec2(spine + ut * page_w, v);
				pc = texture(old_tex, stage_origin + o * stage_size);
				shade = 1.0 - 0.42 * s * ut;
			} else {
				vec2 b = vec2(back_spine + ut * back_w, v);
				pc = texture(back_tex, b);
				shade = 1.0 - 0.30 * s * (1.0 - ut) - 0.18 * s;
			}
			pc.rgb *= shade;
			col = vec4(mix(col.rgb, pc.rgb, pc.a), max(col.a, pc.a));
		}
	}

	if (!on_page && suv.y >= page_top && suv.y <= page_bottom + 0.01) {
		// 页缘外侧的投影，越翻越明显、翻到一半最强。
		float dir = abs(span) > 1e-4 ? sign(span) : side;
		float d = (suv.x - (spine + span)) * dir;
		if (d > 0.0) {
			float sh = 0.38 * s * exp(-d * 28.0);
			if (col.a < 0.01) {
				col = vec4(0.16, 0.1, 0.07, sh);
			} else {
				col.rgb *= 1.0 - sh;
			}
		}
	}

	COLOR = vec4(col.rgb, col.a * fade);
}
