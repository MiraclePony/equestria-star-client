shader_type spatial;
render_mode unshaded, specular_disabled, shadows_disabled, cull_disabled;
global uniform float paper_clock;
global uniform float paper_motion;
uniform vec2 map_centre;
uniform float hex_diameter=1.1;
// Preset 9 sunlit sea (Claude): 0 keeps V5 byte-identical.
uniform float sun_mix=0.0;
varying vec2 world;
void vertex(){world=(MODEL_MATRIX*vec4(VERTEX,1.0)).xz;}
void fragment(){
    vec2 p=(world-map_centre)/hex_diameter;
    // Broad, quiet paper sheets: static contours have no high frequency noise.
    float depth=sin(p.x*.23+p.y*.11)+.55*cos(p.y*.29-p.x*.08);
    vec3 shallow=mix(vec3(.24,.48,.44),vec3(.40,.62,.52),sun_mix), water=mix(vec3(.10,.30,.38),vec3(.20,.44,.46),sun_mix), deep=mix(vec3(.07,.20,.29),vec3(.13,.31,.40),sun_mix);
    vec3 c=mix(water,shallow,smoothstep(-.15,1.3,depth));
    c=mix(c,deep,(1.0-smoothstep(-1.3,-.40,depth))*.70);
    float band=sin(p.y*1.7+p.x*.45+sin(p.x*.20)*1.2-paper_clock*.24);
    float broken=smoothstep(.15,.8,sin(p.x*.75+p.y*.13));
    float wave=smoothstep(.975,.997,band)*broken;
    c=mix(c,mix(vec3(.68,.81,.78),vec3(.95,.92,.80),sun_mix),wave*mix(.21,.34,sun_mix)*paper_motion);
    ALBEDO=c;ROUGHNESS=1.0;
}
