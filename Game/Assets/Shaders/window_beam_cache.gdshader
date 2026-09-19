shader_type canvas_item;
render_mode unshaded;
uniform sampler2D cells : filter_nearest, repeat_disable;
uniform vec2 grid_size;
uniform float angle;
uniform float aperture;
uniform float shadow_curve=1.0;
uniform float ambient_curve=1.0;

// Wall-only air receives light from a depth opening. Foreground blocks stop it.
float trace_opening(vec2 p, vec2 direction) {
    vec2 cell=floor(p);
    vec2 step_dir=sign(direction);
    vec2 delta=1.0/max(abs(direction),vec2(0.00001));
    vec2 edge=mix(p-cell,cell+vec2(1.0)-p,step(vec2(0.0),direction))*delta;
    for(int i=0;i<64;i++) {
        float distance=min(edge.x,edge.y);
        if(distance>24.0) return 0.0;
        if(edge.x<edge.y) { cell.x+=step_dir.x; edge.x+=delta.x; }
        else { cell.y+=step_dir.y; edge.y+=delta.y; }
        if(any(lessThan(cell,vec2(0.0))) || any(greaterThanEqual(cell,grid_size))) return 0.0;
        vec2 material=texelFetch(cells,ivec2(cell),0).rg;
        if(material.r>0.5) return 0.0;
        if(material.g<0.5) return 1.0-smoothstep(0.0,24.0,distance);
    }
    return 0.0;
}
void fragment() {
    vec2 p=UV*grid_size;
    vec2 material=texture(cells,UV).rg;
    float beam=0.0;
    if(material.r<0.5 && material.g>0.5) {
        // Cached spatial field; symmetric solar-disc quadrature keeps direction fixed.
        for(int i=0;i<15;i++) {
            float u=(float(i)+0.5)/15.0*2.0-1.0;
            float a=angle+aperture*u;
            beam+=trace_opening(p,vec2(sin(a),-cos(a)))*sqrt(1.0-u*u);
        }
        beam/=11.832;
    }
    beam=clamp(beam,0.0,1.0);
    // Shape the two halves independently without changing cone geometry.
    if(beam<0.5) beam=0.5*pow(beam*2.0,mix(8.0,1.0,ambient_curve));
    else beam=1.0-0.5*pow((1.0-beam)*2.0,mix(8.0,1.0,shadow_curve));
    COLOR=vec4(vec3(beam),1.0);
}
