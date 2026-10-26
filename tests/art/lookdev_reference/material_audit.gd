extends RefCounted
## Read-only scene inventory. Does not change materials, lights or camera state.

static func collect(root: Node) -> Dictionary:
	var result := {"engine": Engine.get_version_info(), "renderer": RenderingServer.get_current_rendering_method(), "meshes": [], "lights": [], "environments": [], "cameras": []}
	var stack: Array[Node] = [root]
	while not stack.is_empty():
		var node: Node = stack.pop_back()
		for child in node.get_children():
			stack.append(child)
		if node is MeshInstance3D and node.mesh != null:
			var entry := {"path": str(node.get_path()), "scale": str(node.global_basis.get_scale()), "cast_shadow": node.cast_shadow, "surfaces": []}
			for i in range(node.mesh.get_surface_count()):
				var mat: Material = node.get_active_material(i)
				var surface := {"index": i, "material": str(mat), "kind": "none" if mat == null else mat.get_class()}
				if mat is BaseMaterial3D:
					surface.merge({"shading_mode": mat.shading_mode, "metallic": mat.metallic, "roughness": mat.roughness, "emission_enabled": mat.emission_enabled, "vertex_color_albedo": mat.vertex_color_use_as_albedo, "albedo_color": str(mat.albedo_color), "texture": str(mat.albedo_texture), "transparency": mat.transparency})
				elif mat is ShaderMaterial:
					surface["shader_path"] = "" if mat.shader == null else mat.shader.resource_path
				entry["surfaces"].append(surface)
			result["meshes"].append(entry)
		elif node is Light3D:
			result["lights"].append({"path": str(node.get_path()), "type": node.get_class(), "energy": node.light_energy, "color": str(node.light_color), "shadows": node.shadow_enabled, "visible": node.is_visible_in_tree()})
		elif node is WorldEnvironment:
			result["environments"].append({"path": str(node.get_path()), "environment": str(node.environment)})
		elif node is Camera3D:
			result["cameras"].append({"path": str(node.get_path()), "current": node.current, "environment_override": str(node.environment), "attributes": str(node.attributes), "projection": node.projection, "near": node.near, "far": node.far, "size": node.size})
	return result

static func write_report(root: Node, path: String = "user://paper_scene_audit.json") -> Error:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string(JSON.stringify(collect(root), "\t"))
	return OK
