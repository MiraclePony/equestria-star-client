extends Node3D
## Standalone rig. Integrating project must explicitly disable its previous sun/
## WorldEnvironment and clear Camera3D.environment before using this rig.

const GradeLayer := preload("res://tests/art/lookdev_reference/paper_grade_layer.gd")
@export_enum("A - Paper Day", "B - Clear Sky", "C - Soft Afternoon") var preset: int = 0
@export var hex_diameter: float = 4.0
@export var enable_grade: bool = true
@export var enable_forward_plus_ao: bool = true
var environment: Environment
var sun: DirectionalLight3D
var bounce: DirectionalLight3D
var grade: CanvasLayer
var world_environment: WorldEnvironment

func _ready() -> void:
	world_environment = WorldEnvironment.new()
	world_environment.name = "PaperWorldEnvironment"
	add_child(world_environment)
	sun = DirectionalLight3D.new()
	sun.name = "PaperSun"
	add_child(sun)
	bounce = DirectionalLight3D.new()
	bounce.name = "PaperBounce"
	add_child(bounce)
	grade = GradeLayer.new()
	grade.name = "PaperGrade"
	add_child(grade)
	apply_preset(preset)
	if ProjectSettings.get_setting("rendering/lights_and_shadows/use_physical_light_units", false):
		push_warning("This lookdev uses nonphysical energy units. Disable physical units or recalibrate lights.")
	if get_viewport().get_camera_3d() != null and get_viewport().get_camera_3d().environment != null:
		push_warning("Camera environment overrides this WorldEnvironment. Clear it explicitly before evaluation.")

func apply_preset(index: int) -> void:
	preset = clampi(index, 0, 2)
	var renderer := RenderingServer.get_current_rendering_method()
	environment = Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("86bdc5")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	environment.ambient_light_color = Color("b9c5e8")
	environment.ambient_light_energy = 0.30
	environment.ambient_light_sky_contribution = 0.65
	environment.reflected_light_source = Environment.REFLECTION_SOURCE_DISABLED
	var sky_material := ProceduralSkyMaterial.new()
	sky_material.sky_top_color = Color("779dc7")
	sky_material.sky_horizon_color = Color("d5e7e5")
	sky_material.ground_bottom_color = Color("665b75")
	sky_material.ground_horizon_color = Color("a1b3b7")
	sky_material.sky_energy_multiplier = 0.55
	sky_material.ground_energy_multiplier = 0.40
	sun.sky_mode = DirectionalLight3D.SKY_MODE_LIGHT_ONLY
	bounce.sky_mode = DirectionalLight3D.SKY_MODE_LIGHT_ONLY
	if renderer != "gl_compatibility":
		var sky := Sky.new()
		sky.sky_material = sky_material
		environment.sky = sky
	environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment.tonemap_exposure = 1.0
	environment.tonemap_white = 1.0
	environment.adjustment_enabled = false # Exactly one grading stage.
	environment.glow_enabled = false
	environment.fog_enabled = false
	environment.volumetric_fog_enabled = false
	environment.ssr_enabled = false
	environment.ssil_enabled = false
	environment.sdfgi_enabled = false
	environment.ssao_enabled = enable_forward_plus_ao and renderer == "forward_plus"
	environment.ssao_radius = hex_diameter * 0.10
	environment.ssao_intensity = 0.65
	environment.ssao_power = 1.10
	environment.ssao_detail = 0.25
	environment.ssao_light_affect = 0.0
	world_environment.environment = environment
	sun.rotation_degrees = Vector3(-48.0, -35.0, 0.0)
	sun.light_color = Color("fff4e4")
	sun.light_energy = 1.15
	sun.light_specular = 0.10
	sun.shadow_enabled = true
	sun.shadow_opacity = 1.0
	sun.shadow_bias = 0.08
	sun.shadow_normal_bias = 1.2
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_blend_splits = true
	sun.directional_shadow_max_distance = hex_diameter * 24.0
	sun.light_angular_distance = 0.7 if renderer == "forward_plus" else 0.0
	# Shadow filtering is not PCSS; Mobile cannot obtain directional PCSS by
	# increasing light_angular_distance. Orthographic clip range must also be tight.
	bounce.rotation_degrees = Vector3(48.0, 145.0, 0.0)
	bounce.light_color = Color("c6cff4")
	bounce.light_energy = 0.11
	bounce.light_specular = 0.0
	bounce.shadow_enabled = false
	if preset == 1:
		sun.light_color = Color("f4f7ff")
		sun.light_energy = 1.10
		sky_material.sky_energy_multiplier = 0.65
		bounce.light_energy = 0.12
	elif preset == 2:
		sun.rotation_degrees.x = -38.0
		sun.light_color = Color("ffe9d2")
		sun.light_energy = 1.10
		bounce.light_energy = 0.13
	if renderer == "gl_compatibility":
		# Compatibility is a separate color/lighting calibration, not an
		# identical screenshot promise. Keep highlights safely below clipping.
		environment.tonemap_mode = Environment.TONE_MAPPER_LINEAR
		sun.light_energy *= 0.58
		bounce.light_energy *= 0.70
		sky_material.sky_energy_multiplier *= 0.65
		sky_material.ground_energy_multiplier *= 0.65
		# Avoid transient radiance cubemaps during repeated GL preset switches.
		# Constant fill also keeps this fallback cheaper and easier to calibrate.
		environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
		environment.ambient_light_sky_contribution = 0.0
		environment.ambient_light_energy = 0.28
		environment.sky = null
	grade.apply_palette(preset)
	grade.set_enabled(enable_grade)

func set_grade_enabled(enabled: bool) -> void:
	enable_grade = enabled
	grade.set_enabled(enabled)

func use_flat_baseline() -> void:
	# Deliberately flat diagnostic baseline, not a reconstruction of user settings.
	apply_preset(0)
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_sky_contribution = 0.0
	environment.ambient_light_color = Color.WHITE
	environment.ambient_light_energy = 0.85
	environment.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	environment.ssao_enabled = false
	sun.light_color = Color.WHITE
	sun.light_energy = 0.5
	sun.shadow_enabled = false
	bounce.light_energy = 0.0
	grade.set_enabled(false)
