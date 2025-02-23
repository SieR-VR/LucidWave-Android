
#ifdef EMBEDDED
varying vec2 texVp;
#else
#extension GL_ARB_separate_shader_objects : enable
layout(location=1) in vec2 texVp;
layout(location=0) out vec4 target;
#endif

uniform ivec2 screenCenter;
// x = bar time
// y = off-sync but smooth bpm based timing
// z = real time since song start
uniform vec3 timing;
uniform ivec2 viewport;
uniform float objectGlow;
// bg_texture.png
uniform sampler2D mainTex;
uniform vec2 tilt;
uniform float clearTransition;

#define HALF_PI 1.570796326794
#define PI 3.14159265359
#define TWO_PI 6.28318530718

vec3 hsv2rgb(vec3 c) {
  vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
  vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
  return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

vec2 rotate_point(vec2 cen,float angle,vec2 p)
{
  float s = sin(angle);
  float c = cos(angle);

  // translate point back to origin:
  p.x -= cen.x;
  p.y -= cen.y;

  // rotate point
  float xnew = p.x * c - p.y * s;
  float ynew = p.x * s + p.y * c;

  // translate point back:
  p.x = xnew + cen.x;
  p.y = ynew + cen.y;
  return p;
}


// Reference to
// https://github.com/Ikeiwa/USC-SDVX-IV-Skin (ikeiwa)
// http://thndl.com/square-shaped-shaders.html
// https://thebookofshaders.com/07/

float portrait(float a, float b)
{
	float resu;
	if (viewport.y > viewport.x)
	{
	 resu = a;
	}
	else{
	 resu = b;
	}
	
	return resu;
}


float GetDistanceShape(vec2 st, int N){
    vec3 color = vec3(0.0);
    float d = 0.0;

    // Angle and radius from the current pixel
    float a = atan(st.x,st.y)+PI;
    float r = TWO_PI/float(N);

    // Shaping function that modulate the distance
    d = cos(floor(.5+a/r)*r-a)*length(st);

    return d;

}

float mirrored(float v) {
    float m = mod(v, 2.0);
    return mix(m, 2.0 - m, step(1.0, m));
}

vec2 ScaleUV(vec2 uv,float scale,vec2 pivot){
	return (uv - pivot) * scale + pivot;
}

float rand(vec2 co){
    return fract(sin(dot(co.xy ,vec2(12.9898,78.233))) * 43758.5453);
}

vec2 rotate2d(vec2 uv,float _angle, vec2 pivot){
    return (uv - pivot) * mat2(cos(_angle),-sin(_angle),
                sin(_angle),cos(_angle)) + pivot;
}


  // Number of sides of your shape
  int N = 4;

  // "Stretch" | lower = "Stretchier"
  float Stretch = .12;

  // Speed
  float speed = 1;

  // Default rotation in radians
  float BaseRotation = 0.0;

  // Rotation of texture for alignment, in radians
  float BaseTexRotation = 0.0;

  // Scale
  vec2 Scale = vec2(portrait(0.8,1.5), 1.6);

void main()
{
	float beatTime = mod(timing.y,1.0);

    float ar = float(viewport.x) / float(viewport.y);
	float var = float(viewport.y) / float(viewport.x);

	vec2 screenUV = vec2(texVp.x / viewport.x, texVp.y / viewport.y);

	vec2 uv = screenUV;
    uv.x *= ar;

    vec2 center = vec2(screenCenter) / vec2(viewport);
    center.x *= ar;

    vec2 point = uv;
	float bgrot =  dot(tilt, vec2(0.5, 1.0));
    point = rotate_point(center, BaseRotation + (bgrot * TWO_PI), point);
    vec2 point_diff = center - point;
    point_diff /= Scale;
    float diff = GetDistanceShape(point_diff,N);
    float thing2 = Stretch / diff;
	float fog = -0.5 / (diff * 7. * Scale.x) + 1.;
    fog = clamp(fog, 0, 1);
    float texY = thing2;
    texY += timing.y * speed;

	//CENTERED UV CALCULATION
	vec2 centerUV = uv;
	centerUV = center - centerUV;
	centerUV *= -1.0;
	centerUV += 0.5;
	//END CENTERED UV CALCULATION

	//PARTICLES UV PRE-CALCULATION
	vec2 particlesUV = point;
	particlesUV.x = center.x - particlesUV.x;
	
	particlesUV.x = mirrored(clamp(particlesUV.x,-1.0,1.0));
    //particlesUV.y += 0.1;

	//PARTICLES
	vec2 particleOffset = vec2(0.5,0.375);
	vec4 particles = vec4(0.0,0.0,1.0,0.0);
	vec4 particles2 = vec4(0.0,0.0,1.0,0.0);
    float particlesTime = timing.z * 2.2;

	for(int i=0;i<3;++i){
        float timeOffset = 0.5*float(i);
		float particleSpawnTime = floor(particlesTime - timeOffset);
		float particleTime = (particlesTime - timeOffset) - particleSpawnTime;

        float rnd = rand(vec2(i,particleSpawnTime));
        float rnd2 = rand(vec2(particleSpawnTime,i));

        vec2 offset = vec2(rnd2*-0.4,rnd*0.4 + 0.075);

        float spriteIndex = floor(rnd*4.0)*0.0625;

        vec2 particleUV = ScaleUV(particlesUV,(1.0-particleTime)*8.0,vec2(-0.02,center.y));
		particleUV /= 0.7;
		particleUV.y += 0.2;
        particleUV += offset;
    	particleUV = clamp(particleUV,0.0,1.0);

		vec4 particle = texture(mainTex, particleUV*0.0625+particleOffset + vec2(spriteIndex,0.0) - 0.0625 * floor(clearTransition)).rgba * min(particleTime*2.0,1.0);
		particles = mix(particles,particle,particle.a);
	}
	
	target = particles;

	//END PARTICLES

    float rot = (atan(point_diff.x,point_diff.y) + BaseTexRotation) / TWO_PI;
	float clear_bright = 0.8 + timing.y * 0.2;

    vec4 col = texture(mainTex, vec2(rot/4.0,mod(texY/4.0,0.25) + floor(clearTransition) * 0.2578125));
    vec4 clear_col = texture(mainTex, vec2(rot/4.0,mod(texY/4.0,0.25) + 0.2578125));

    col.rgb = mix(col,clear_col * 2.4,clearTransition).rgb;
    target.xyz = col.xyz * 1.8;
    target.a = col.a * fog;
	
	centerUV = screenUV;
	centerUV.x -= 0.5;
	centerUV.x *= ar;
	centerUV /= ar;
	centerUV.x += 0.5;
	centerUV = clamp(centerUV,0.0,1.0);
	
	vec2 bguv = centerUV*0.46875+vec2(0.015625,0.515625);
	vec3 bgcol = texture(mainTex, bguv).rgb;
	vec2 bguvClear = centerUV*0.46875+vec2(0.515625,0.515625);
	vec3 bgcolClear = texture(mainTex, bguvClear).rgb;
	
	bgcol = mix(bgcol,bgcolClear,clearTransition);

	target.rgb = bgcol * (1-target.a) + target.rgb * target.a;
	target.rgb = mix(target.rgb,particles.rgb,particles.a);
	target.a = 1.0;
}

//Edited by Halo ID