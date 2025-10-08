shader_type canvas_item;

uniform float intensity : hint_range(0.0, 1.0) = 0.5;  // Độ mạnh của vignette
uniform float softness : hint_range(0.1, 2.0) = 0.8;   // Độ mềm của gradient
uniform vec4 vignette_color : source_color = vec4(1.0, 0.0, 0.0, 1.0);  // Màu đỏ
uniform vec2 rect_size = vec2(2.0, 0.3);  // Kích thước vùng trong suốt (chiều rộng, chiều cao)

void fragment() {
    vec2 uv = UV;
    vec2 centered_uv = uv - vec2(0.5);
    float aspect_ratio = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    centered_uv.x *= aspect_ratio; // Điều chỉnh tỉ lệ khung hình
    
    // Tính khoảng cách đến biên hình chữ nhật
    vec2 rect_half_size = rect_size * 0.5;
    vec2 dist = abs(centered_uv) - rect_half_size;
    float max_dist = max(dist.x, dist.y);
    float vignette_amount = smoothstep(0.0, softness, max_dist);
    vignette_amount = clamp(vignette_amount * intensity, 0.0, 1.0);
    
    // Màu đỏ ở biên, trong suốt ở giữa
    vec3 blended_color = vignette_color.rgb;
    COLOR = vec4(blended_color, vignette_amount * vignette_color.a);
}