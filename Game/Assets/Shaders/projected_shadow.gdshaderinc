// RGBA32F spatial index and compressed logical rectangles; no ray samples.
uniform sampler2D shadow_geometry : filter_nearest, repeat_disable;
uniform vec2 map_size = vec2(1.0);
uniform float sun_angle = -0.5585;
uniform float penumbra = 0.5;
uniform float penumbra_shadow_transition = 1.0;
uniform float penumbra_ambient_transition = 1.0;

vec4 geometry(int index) {
    return texelFetch(shadow_geometry, ivec2(index % 1024, index / 1024), 0);
}
// Angular silhouette of an opaque rectangle as seen by this receiver.
vec2 shadow_interval(vec4 r, vec2 p, float lower, float upper) {
    if (r.y >= p.y) return vec2(upper, lower);
    float far_y = p.y - r.y;
    float near_y = max(0.000001, p.y - r.w);
    float left = min(atan(r.x - p.x, far_y), atan(r.x - p.x, near_y));
    float right = max(atan(r.z - p.x, far_y), atan(r.z - p.x, near_y));
    return vec2(max(lower, left), min(upper, right));
}
// One intensity curve with variable hardness, not a blend of two images.
// Its domain and endpoints stay fixed. As the exponent approaches zero the
// curve approaches a step at the seam; the pixel footprint limits aliasing.
float penumbra_edge_profile(float coordinate, float softness, float pixel_coverage) {
    float q = clamp(coordinate, 0.0, 1.0);
    float hardness_exponent = clamp(softness, 0.0, 1.0);
    float aa_coverage = clamp(q / max(pixel_coverage, 0.000001), 0.0, 1.0);
    if (hardness_exponent == 0.0) return aa_coverage;
    float profile = pow(q * q * (2.0 - q), hardness_exponent);
    return min(profile, aa_coverage);
}

// Ray/rectangle intervals measured from the original receiver. No displaced
// receiver, connectivity id or depth attenuation participates in this rule.
vec2 ray_span(vec4 r, vec2 p, vec2 ray) {
    vec2 enter=vec2(-1e20), leave=vec2(1e20);
    if (abs(ray.x)<0.000001) {
        if(p.x<r.x || p.x>=r.z) return vec2(1e20,-1e20);
    } else {
        float a=(r.x-p.x)/ray.x,b=(r.z-p.x)/ray.x;
        enter.x=min(a,b);leave.x=max(a,b);
    }
    float a=(r.y-p.y)/ray.y,b=(r.w-p.y)/ray.y;
    enter.y=min(a,b);leave.y=max(a,b);
    return vec2(max(0.0,max(enter.x,enter.y)),min(leave.x,leave.y));
}
float solid_prefix(vec2 p,vec2 ray,vec4 header) {
    float end=0.0;
    int count=int(header.y);
    for(int merge=0;merge<count;merge++) {
        float previous=end;
        for(int i=0;i<count;i++) {
            vec2 span=ray_span(geometry(int(header.x)+i*2),p,ray);
            if(span.y>span.x && span.x<=end+0.00001) end=max(end,span.y);
        }
        if(end<=previous) break;
    }
    return end;
}
// An exposed underside can cast onto a recessed solid. Vertical risers
// alone do not: otherwise a descending tile staircase shadows its own slope.
// Test exposure against all rectangles so material/run subdivision is irrelevant.
bool underside_exposed(vec2 point,vec4 header) {
    point.y+=0.00001;
    for(int j=0;j<int(header.y);j++) {
        vec4 other=geometry(int(header.x)+j*2);
        if(point.x>=other.x && point.x<other.z && point.y>=other.y && point.y<other.w) return false;
    }
    return true;
}
float blocked_after_gap(vec2 p,float angle,vec4 header) {
    vec2 ray=vec2(sin(angle),-cos(angle));
    float prefix=solid_prefix(p,ray,header);
    for(int i=0;i<int(header.y);i++) {
        vec4 r=geometry(int(header.x)+i*2);
        vec2 span=ray_span(r,p,ray);
        float bottom_entry=(r.w-p.y)/ray.y;
        if(span.y<=span.x || bottom_entry<=prefix+0.00001) continue;
        // Enter through the underside, not a vertical step edge.
        if(abs(span.x-bottom_entry)>0.00001) continue;
        if(underside_exposed(p+ray*bottom_entry,header)) return 1.0;
    }
    return 0.0;
}
float separated_occlusion(vec2 p,vec2 footprint,vec4 header,float spread) {
    if(spread<0.00001) {
        vec2 ray=vec2(sin(sun_angle),-cos(sun_angle));
        float prefix=solid_prefix(p,ray,header);
        float covered=0.0,slope=-tan(sun_angle),aa=max(length(footprint),0.0001);
        for(int i=0;i<int(header.y);i++) {
            vec4 r=geometry(int(header.x)+i*2);
            if(r.w>=p.y) continue;
            float entry=(r.w-p.y)/ray.y;
            if(entry<=prefix+0.00001) continue;
            vec2 contact=p+ray*entry;
            contact.x=clamp(contact.x,r.x+0.00001,r.z-0.00001);
            contact.y=r.w;
            if(!underside_exposed(contact,header)) continue;
            float left=r.x+slope*(p.y-r.w),right=r.z+slope*(p.y-r.w);
            float distance=min(p.x-left,right-p.x)/sqrt(1.0+slope*slope);
            covered=max(covered,smoothstep(-aa*0.5,aa*0.5,distance));
        }
        return covered;
    }
    float lower=sun_angle-spread,upper=sun_angle+spread,cursor=lower,covered=0.0;
    // Visibility can change only at a rectangle corner. Integrate each exact
    // angular interval; this is not a fixed set of angular samples.
    for(int segment=0;segment<int(header.y)*4+1 && cursor<upper;segment++) {
        float next=upper;
        for(int i=0;i<int(header.y);i++) {
            vec4 r=geometry(int(header.x)+i*2);
            for(int corner=0;corner<4;corner++) {
                vec2 q=vec2(corner<2?r.x:r.z,(corner==0 || corner==2)?r.y:r.w);
                if(q.y>=p.y) continue;
                float angle=atan(q.x-p.x,p.y-q.y);
                if(angle>cursor+0.0000001) next=min(next,angle);
            }
        }
        covered+=(next-cursor)*blocked_after_gap(p,(cursor+next)*0.5,header);
        cursor=next;
    }
    covered=clamp(covered/(2.0*spread),0.0,1.0);
    float pixel=max(2.0*fwidth(covered),0.000001);
    return covered<0.5?0.5*penumbra_edge_profile(2.0*covered,penumbra_ambient_transition,pixel)
        :1.0-0.5*penumbra_edge_profile(2.0*(1.0-covered),penumbra_shadow_transition,pixel);
}

// Oclusao solar num ponto. Separada porque o receptor solido precisa avalia-la em mais de um ponto.
float sun_occlusion(vec2 p, vec2 pixel_footprint) {
    vec2 ray = vec2(sin(sun_angle), -cos(sun_angle));
    ivec2 bins = (ivec2(map_size / 2.0) + ivec2(15)) / 16;
    ivec2 bin = clamp(ivec2(floor(p / 16.0)), ivec2(0), bins - ivec2(1));
    vec4 header = geometry(bin.y * bins.x + bin.x);
    float spread = radians(8.0) * pow(1.0 - clamp(penumbra, 0.0, 1.0), 2.0);
    float covered = 0.0;
    int count = int(header.y);
    bool receiver_solid=false;
    for (int i=0;i<count;i++) {
        vec4 r=geometry(int(header.x)+i*2);
        if (p.x>=r.x && p.x<r.z && p.y>=r.y && p.y<r.w) { receiver_solid=true; break; }
    }
    if (receiver_solid) return separated_occlusion(p,pixel_footprint,header,spread);
    if (spread < 0.00001) {
        // Point-source limit with screen-space edge coverage.
        float slope = -tan(sun_angle);
        float aa = max(length(pixel_footprint), 0.0001);
        for (int i = 0; i < count; i++) {
            vec4 r = geometry(int(header.x) + i * 2);
            if (r.y >= p.y) continue;
            float far_y = p.y - r.y, near_y = max(0.0, p.y - r.w);
            float left = r.x + min(slope * far_y, slope * near_y);
            float right = r.z + max(slope * far_y, slope * near_y);
            float distance = min(p.x - left, right - p.x) / sqrt(1.0 + slope * slope);
            covered = max(covered, smoothstep(-aa * 0.5, aa * 0.5, distance));
            if (covered >= 1.0) break;
        }
    } else {
        // Integrate the UNION of blocked source intervals. Rectangle decomposition
        // cannot create additional penumbrae inside one connected silhouette.
        // Exact interval sweep, no fixed angular sample count or interval capacity.
        float lower = sun_angle - spread, upper = sun_angle + spread;
        float cursor = lower;
        for (int segment = 0; segment < count && cursor < upper; segment++) {
            float start = upper, end = lower;
            for (int i = 0; i < count; i++) {
                vec2 v = shadow_interval(geometry(int(header.x) + i * 2), p, lower, upper);
                if (v.y <= cursor || v.x >= v.y) continue;
                float candidate = max(cursor, v.x);
                if (candidate < start) { start = candidate; end = v.y; }
                else if (candidate == start) end = max(end, v.y);
            }
            if (end <= start) break;
            for (int merge = 0; merge < count && end < upper; merge++) {
                float extended = end;
                for (int i = 0; i < count; i++) {
                vec2 v = shadow_interval(geometry(int(header.x) + i * 2), p, lower, upper);
                    if (v.x <= end && v.y > extended) extended = v.y;
                }
                if (extended <= end) break;
                end = extended;
            }
            covered += (end - start) / (2.0 * spread);
            cursor = end;
        }
        // Only Penumbra determines the angular extent. These controls change
        // intensity, never coordinate scale, clipping thresholds or ramp width.
        covered = clamp(covered, 0.0, 1.0);
        float coverage_pixel = max(2.0 * fwidth(covered), 0.000001);
        covered = covered < 0.5
            ? 0.5 * penumbra_edge_profile(2.0 * covered, penumbra_ambient_transition, coverage_pixel)
            : 1.0 - 0.5 * penumbra_edge_profile(2.0 * (1.0 - covered), penumbra_shadow_transition, coverage_pixel);
    }
    return covered;
}


float projected_shadow(vec2 p, vec2 footprint) {
    if (cos(sun_angle)<=0.001) return 0.0;
    return sun_occlusion(p,footprint);
}
