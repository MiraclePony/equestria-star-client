extends CanvasLayer
## Place below ALL HUD CanvasLayers. A single explicit screen copy avoids reading
## a stale back-buffer left by another screen-reading material.

const GRADE := preload("res://tests/art/lookdev_reference/paper_grade.gdshader")
var grade_material: ShaderMaterial
var grade_rect: ColorRect
var copy: BackBufferCopy

func _ready() -> void:
	layer = -10
	copy = BackBufferCopy.new()
	copy.copy_mode = BackBufferCopy.COPY_MODE_VIEWPORT
	add_child(copy)
	grade_material = ShaderMaterial.new()
	grade_material.shader = GRADE
	grade_rect = ColorRect.new()
	grade_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	grade_rect.material = grade_material
	add_child(grade_rect)
	grade_rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if get_viewport().use_hdr_2d:
		push_warning("PaperGrade bypassed: this version requires HDR 2D disabled.")
		set_enabled(false)

func set_enabled(enabled: bool) -> void:
	var active := enabled and not get_viewport().use_hdr_2d
	grade_rect.visible = active
	copy.copy_mode = BackBufferCopy.COPY_MODE_VIEWPORT if active else BackBufferCopy.COPY_MODE_DISABLED

func apply_palette(preset: int) -> void:
	# Small finishing differences. The lighting rig does most of the work.
	grade_material.set_shader_parameter("saturation", 1.02 if preset != 2 else 1.0)
	grade_material.set_shader_parameter("contrast", 1.025)
	grade_material.set_shader_parameter("strength", 0.50)
	grade_material.set_shader_parameter("highlight_shift", Vector3(0.012, 0.004, -0.006) if preset == 2 else Vector3(0.006, 0.003, -0.004))
