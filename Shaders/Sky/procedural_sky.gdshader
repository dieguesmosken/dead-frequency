
// Shaders/Sky/procedural_sky.gdshader  (v0.9.9)
// Base: "Procedural Night Sky – Clouds, Moon & Stars", de REDEMO_H747 (Godot Shaders, 08/11/2026), licença CC0:
// https://godotshaders.com/shader/procedural-night-sky-clouds-moon-stars/  — 100% procedural, sem texturas.
// Adaptado para o ciclo de dia do jogo: sol com disco e brilho, nuvens claras de dia e
// estrelas que só aparecem à noite. O DayNightController passa cores e direções a cada quadro:
// zenith/horizon/ground_color, cloud_dark/light_color, star_brightness, moon_direction,
// moon_brightness, sun_direction e sun_strength.
shader_type sky;

// ============================================================================
// Procedural Night Sky
// Target: Godot 4.x
// Fully procedural sky shader. No textures or external assets required.
//
// Features:
// - Procedural sky gradient
// - Animated volumetric-style cloud patterns
// - Procedural moon with surface details
// - Procedural star field
// - Moon halo and cloud lighting
// - Performance-conscious noise calculations
// - Optional cubemap pass optimization
//
// Performance notes:
// Clouds: Configurable noise octaves (1-4).
// Stars: Early-out hashing to reduce unnecessary calculations.
// Moon: Surface noise is evaluated only inside the moon disc.
// No ray marching or texture sampling.
//
// NOTE: This is a procedural visual approximation,
// not a physically accurate atmospheric simulation.
// ============================================================================

// ---------------------------------------------------------------- Gradient --
group_uniforms gradient;

uniform vec3 zenith_color : source_color = vec3(0.012, 0.015, 0.024);
uniform vec3 horizon_color : source_color = vec3(0.090, 0.100, 0.120);
uniform vec3 ground_color : source_color = vec3(0.020, 0.022, 0.026);

// Vertical gradient transition.
uniform float gradient_height : hint_range(0.1, 1.0) = 0.6;


// ------------------------------------------------------------------ Clouds --
group_uniforms clouds;

// 0 = almost clear, 1 = overcast.
uniform float cloud_coverage : hint_range(0.0, 1.0) = 0.62;

// Higher values produce smaller cloud features.
uniform float cloud_scale : hint_range(0.5, 8.0) = 2.5;

// Number of noise octaves.
uniform int cloud_octaves : hint_range(1, 4) = 3;

// Softness of cloud edges.
uniform float cloud_softness : hint_range(0.02, 0.5) = 0.22;

// Cloud noise contrast.
uniform float cloud_contrast : hint_range(0.5, 3.0) = 1.5;

// Cloud turbulence.
uniform float cloud_warp : hint_range(0.0, 1.5) = 0.7;

// Overall cloud opacity.
uniform float cloud_opacity : hint_range(0.0, 1.0) = 0.96;

// Horizontal cloud movement speed.
uniform float cloud_speed : hint_range(0.0, 0.1) = 0.008;

// Cloud fade near the horizon.
uniform float cloud_horizon_fade : hint_range(0.02, 0.5) = 0.12;

uniform vec3 cloud_dark_color : source_color = vec3(0.045, 0.050, 0.060);
uniform vec3 cloud_light_color : source_color = vec3(0.300, 0.320, 0.360);

// Moonlight influence on clouds.
uniform float cloud_moonlight : hint_range(0.0, 2.0) = 0.6;


// -------------------------------------------------------------------- Moon --
group_uniforms moon;

// Direction toward the moon.
uniform vec3 moon_direction = vec3(-0.35, 0.40, -0.85);

uniform vec3 moon_color : source_color = vec3(0.82, 0.85, 0.88);

// Moon angular radius in degrees.
uniform float moon_size : hint_range(0.5, 8.0) = 2.5;

// Moon edge softness.
uniform float moon_edge : hint_range(0.05, 2.0) = 0.25;

uniform float moon_brightness : hint_range(0.0, 3.0) = 1.1;

// Moon halo intensity.
uniform float moon_halo_tight : hint_range(0.0, 1.0) = 0.10;
uniform float moon_halo_wide : hint_range(0.0, 0.5) = 0.035;

// Moon surface detail. 0 disables surface noise.
uniform float moon_detail : hint_range(0.0, 1.0) = 0.5;


// --------------------------------------------------------------------- Sun --
group_uniforms sun;

// Direção para o sol (o DayNightController atualiza).
uniform vec3 sun_direction = vec3(0.0, 1.0, 0.0);
uniform vec3 sun_color : source_color = vec3(1.0, 0.92, 0.75);
// Raio angular do disco em graus.
uniform float sun_size : hint_range(0.5, 8.0) = 2.2;
// 0 = sol escondido (noite), 1 = sol pleno.
uniform float sun_strength : hint_range(0.0, 3.0) = 0.0;


// ------------------------------------------------------------------- Stars --
group_uniforms stars;

// Fraction of grid cells containing stars.
uniform float star_density : hint_range(0.0, 0.4) = 0.15;

uniform float star_brightness : hint_range(0.0, 2.0) = 0.6;

// Star grid resolution.
uniform float star_scale : hint_range(30.0, 200.0) = 70.0;

// Star radius in grid-cell units.
uniform float star_size : hint_range(0.05, 0.25) = 0.14;


// ----------------------------------------------------------------- Constants --

// Rotation between noise octaves.
const mat2 OCTAVE_ROT = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));

// Sky projection offset.
const float CLOUD_PLANE_OFFSET = 0.3;

// Dithering intensity.
const float DITHER_AMOUNT = 0.0004;


// ----------------------------------------------------------------- Helpers ----

// 2D hash function.
float hash12(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

// 3D hash function.
float hash13(vec3 p3) {
    p3 = fract(p3 * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return fract((p3.x + p3.y) * p3.z);
}

// Smooth 2D value noise.
float vnoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);

    float a = hash12(i);
    float b = hash12(i + vec2(1.0, 0.0));
    float c = hash12(i + vec2(0.0, 1.0));
    float d = hash12(i + vec2(1.0, 1.0));

    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// Layered cloud noise.
float cloud_field(vec2 p) {
    float sum = 0.0;
    float norm = 0.0;
    float amp = 0.5;
    vec2 q = p;

    for (int i = 0; i < 4; i++) {
        if (i >= cloud_octaves) {
            break;
        }

        float n = vnoise(q);

        sum += n * amp;
        norm += amp;

        q = OCTAVE_ROT * q * 2.03
            + (n - 0.5) * cloud_warp * vec2(1.7, -1.3)
            + vec2(11.7, 3.1);

        amp *= 0.5;
    }

    return sum / norm;
}

// Vertical sky gradient.
vec3 sky_gradient(float y) {
    float t = smoothstep(0.0, gradient_height, y);

    vec3 c = mix(horizon_color, zenith_color, t);

    float below = smoothstep(0.0, 0.25, -y);

    return mix(c, ground_color, below);
}

// Procedural star field.
float star_layer(vec3 dir) {
    vec3 p = dir * star_scale;

    // Derivatives are calculated before branching.
    float px = max(length(fwidth(p)), 0.0001);

    vec3 cell = floor(p);
    vec3 f = fract(p);

    float h = hash13(cell);

    if (h > star_density) {
        return 0.0;
    }

    vec3 offs = vec3(
        hash13(cell + 17.31),
        hash13(cell + 41.77),
        hash13(cell + 83.13)
    );

    vec3 center = 0.25 + 0.5 * offs;

    float d = length(f - center);

    float r = max(star_size, px * 0.75);
    float keep = min(1.0, star_size / r);

    float s = 1.0 - smoothstep(0.0, r, d);

    float mag = hash13(cell + 5.19);
    mag = mix(0.3, 1.0, mag * mag);

    return s * s * mag * keep;
}


// --------------------------------------------------------------------- Sky ----

void sky() {
    vec3 dir = EYEDIR;
    vec3 col = sky_gradient(dir.y);

    // Use a lightweight gradient for cubemap rendering.
    if (AT_CUBEMAP_PASS) {
        COLOR = col;
    } else {

        // Moon direction.
        vec3 md = moon_direction;

        float mlen = length(md);

        if (mlen > 0.0001) {
            md = md / mlen;
        } else {
            md = vec3(0.0, 1.0, 0.0);
        }

        float cosang = clamp(dot(dir, md), -1.0, 1.0);


        // Clouds.
        float yc = max(dir.y, 0.0);

        vec2 uv = dir.xz / (yc + CLOUD_PLANE_OFFSET) * cloud_scale;

        uv.x += TIME * cloud_speed;

        float n = cloud_field(uv);

        n = clamp(
            (n - 0.5) * cloud_contrast + 0.5,
            0.0,
            1.0
        );

        float thr = mix(0.78, 0.38, cloud_coverage);

        float body = smoothstep(
            thr,
            thr + cloud_softness,
            n
        );

        float thick = smoothstep(
            thr,
            thr + cloud_softness * 3.0,
            n
        );

        float mask = body * smoothstep(
            0.0,
            cloud_horizon_fade,
            dir.y
        );


        // Stars.
        float ang = degrees(acos(cosang));

        float halo_t = exp(
            -max(ang - moon_size, 0.0) / (moon_size * 1.2)
        );

        float halo_w = exp(
            -ang / (moon_size * 8.0)
        );

        float star_vis = smoothstep(0.02, 0.2, dir.y)
            * (1.0 - clamp(halo_t * 3.0 + halo_w, 0.0, 1.0));

        vec3 star_tint = vec3(0.78, 0.86, 1.0);

        col += star_tint
            * star_layer(dir)
            * star_brightness
            * star_vis;


        // Sol: brilho largo, halo e disco (some de noite com sun_strength = 0).
        if (sun_strength > 0.001) {
            vec3 sd = normalize(sun_direction);
            float scos = clamp(dot(dir, sd), -1.0, 1.0);
            float sang = degrees(acos(scos));
            float sglow = exp(-sang / (sun_size * 10.0)) * 0.35 + exp(-max(sang - sun_size, 0.0) / (sun_size * 1.5)) * 0.5;
            float sdisc = smoothstep(cos(radians(sun_size + 0.4)), cos(radians(sun_size)), scos);
            col += sun_color * sun_strength * (sglow * (1.0 - mask * 0.8) + sdisc * 2.0 * (1.0 - mask));
        }

        // Moon halo and disc.
        col += moon_color * (
            halo_t * moon_halo_tight
            + halo_w * moon_halo_wide
        );

        float disc_in = cos(radians(moon_size));

        float disc_out = cos(
            radians(moon_size + moon_edge)
        );

        float disc = smoothstep(
            disc_out,
            disc_in,
            cosang
        );

        if (disc > 0.0) {
            float shade = 1.0;

            if (moon_detail > 0.001) {
                vec3 helper = vec3(0.0, 1.0, 0.0);

                if (abs(md.y) > 0.99) {
                    helper = vec3(1.0, 0.0, 0.0);
                }

                vec3 mr = normalize(cross(helper, md));
                vec3 mu = cross(md, mr);

                vec2 mp = vec2(
                    dot(dir, mr),
                    dot(dir, mu)
                ) / sin(radians(moon_size));

                float surf = vnoise(mp * 3.0 + 7.0) * 0.65
                    + vnoise(mp * 8.0 + 3.0) * 0.35;

                shade = mix(
                    1.0,
                    0.55 + 0.5 * surf,
                    moon_detail
                );

                float limb = sqrt(
                    max(1.0 - dot(mp, mp), 0.0)
                );

                shade *= mix(0.65, 1.0, limb);
            }

            col += moon_color
                * moon_brightness
                * disc
                * shade;
        }


        // Cloud color and moonlight.
        float tone = clamp(
            0.15 + 0.55 * (1.0 - thick)
            + (n - 0.5) * 0.5,
            0.0,
            1.0
        );

        vec3 cloud_col = mix(
            cloud_dark_color,
            cloud_light_color,
            tone
        );

        float moon_glow = pow(
            max(cosang, 0.0),
            6.0
        );

        cloud_col += moon_color
            * moon_glow
            * cloud_moonlight
            * (1.0 - thick * 0.5)
            * 0.35;

        if (sun_strength > 0.001) {
            float sun_glow = pow(max(dot(dir, normalize(sun_direction)), 0.0), 8.0);
            cloud_col += sun_color * sun_glow * sun_strength * (1.0 - thick * 0.4) * 0.5;
        }

        cloud_col += horizon_color
            * 0.5
            * (1.0 - clamp(dir.y, 0.0, 1.0));

        col = mix(
            col,
            cloud_col,
            mask * cloud_opacity
        );


        // Dithering.
        col += (hash12(FRAGCOORD.xy) - 0.5)
            * DITHER_AMOUNT;

        COLOR = max(col, vec3(0.0));
    }
}
