@tool
extends EditorExportPlugin
class_name PolytoriaMobileExportPlugin

var original_settings := {}

func _get_name() -> String:
	return "MobileExportPlugin"

func _export_begin(features: PackedStringArray, is_debug: bool, path: String, flags: int) -> void:
	if "android" in features or "ios" in features:
		print("Exporting for mobile, applying settings...")

		# Save original settings before changing
		_store_original("application/boot_splash/bg_color")
		_store_original("application/boot_splash/show_image")

		# Apply mobile overrides
		ProjectSettings.set_setting("application/boot_splash/bg_color", "#213e61")
		ProjectSettings.set_setting("application/boot_splash/show_image", false)

		ProjectSettings.save()

	if "android" in features:
		_add_android_dotnet_publish_files()

func _supports_platform(platform) -> bool:
	if platform is EditorExportPlatformAndroid:
		return true
	return false


func _get_export_options_overrides(platform) -> Dictionary:
	return {
		"dotnet/android_use_linux_bionic": false,
	}


func _add_android_dotnet_publish_files() -> void:
	var publish_dirs := {
		"arm64": [
			"res://.godot/mono/temp/bin/ExportDebug/android-arm64",
			"res://.godot/mono/temp/bin/ExportDebug/linux-bionic-arm64",
		],
		"x86_64": [
			"res://.godot/mono/temp/bin/ExportDebug/android-x64",
			"res://.godot/mono/temp/bin/ExportDebug/linux-bionic-x64",
			"res://.godot/mono/temp/bin/ExportDebug/android-x86_64",
			"res://.godot/mono/temp/bin/ExportDebug/linux-bionic-x86_64",
		],
	}

	var found_any := false
	for arch in publish_dirs:
		var source_dir := ""
		for candidate in publish_dirs[arch]:
			var absolute_candidate := ProjectSettings.globalize_path(candidate)
			if DirAccess.dir_exists_absolute(absolute_candidate):
				source_dir = absolute_candidate
				break

		if source_dir.is_empty():
			continue

		found_any = true
		print("Embedding Android .NET publish files from ", source_dir, " for ", arch)
		_add_dotnet_publish_dir(source_dir, source_dir, arch)
		_add_android_shared_libraries(source_dir, arch)

	if not found_any:
		push_warning("Android .NET publish directory was not found, skipping manual export.")


func _add_dotnet_publish_dir(root_dir: String, current_dir: String, arch: String) -> void:
	for file_name in DirAccess.get_files_at(current_dir):
		if file_name.ends_with(".so") or file_name.ends_with(".dbg"):
			continue

		var source_path := current_dir.path_join(file_name)
		var file := FileAccess.open(source_path, FileAccess.READ)
		if file == null:
			push_warning("Failed to read .NET publish file: " + source_path)
			continue

		var relative_path := source_path.trim_prefix(root_dir).trim_prefix("\\").trim_prefix("/")
		relative_path = relative_path.replace("\\", "/")
		var export_path := "res://.godot/mono/publish/" + arch + "/" + relative_path
		add_file(export_path, file.get_buffer(file.get_length()), false)

	for dir_name in DirAccess.get_directories_at(current_dir):
		_add_dotnet_publish_dir(root_dir, current_dir.path_join(dir_name), arch)


func _add_android_shared_libraries(source_dir: String, arch: String) -> void:
	var abi := ""
	match arch:
		"arm64":
			abi = "arm64-v8a"
		"x86_64":
			abi = "x86_64"
		_:
			return

	var staging_dir := ProjectSettings.globalize_path("res://.godot/mono/temp/android_native_libs/" + abi)
	DirAccess.make_dir_recursive_absolute(staging_dir)

	for file_name in DirAccess.get_files_at(source_dir):
		if file_name.ends_with(".so"):
			_stage_android_shared_library(source_dir.path_join(file_name), staging_dir, file_name, abi)

	var native_app_library := source_dir.path_join("native").path_join("Polytoria.so")
	if FileAccess.file_exists(native_app_library):
		_stage_android_shared_library(native_app_library, staging_dir, "libPolytoria.so", abi)
		return

	var publish_dir := source_dir.path_join("publish")
	if DirAccess.dir_exists_absolute(publish_dir):
		var publish_app_library := publish_dir.path_join("Polytoria.so")
		if FileAccess.file_exists(publish_app_library):
			_stage_android_shared_library(publish_app_library, staging_dir, "libPolytoria.so", abi)


func _stage_android_shared_library(source_path: String, staging_dir: String, target_name: String, abi: String) -> void:
	var staged_path := staging_dir.path_join(target_name)
	var copy_error := DirAccess.copy_absolute(source_path, staged_path)
	if copy_error != OK:
		push_warning("Failed to stage Android native library: " + source_path)
		return

	print("Adding Android native library ", staged_path, " for ", abi)
	add_shared_object(staged_path, PackedStringArray([abi]), "lib/" + abi)


func _export_end() -> void:
	if not original_settings.is_empty():
		print("Restoring original settings...")

		for key in original_settings.keys():
			if original_settings[key] == null:
				ProjectSettings.clear(key)
			else:
				ProjectSettings.set_setting(key, original_settings[key])

		ProjectSettings.save()
		original_settings.clear()

func _store_original(key: String) -> void:
	if not original_settings.has(key):
		if ProjectSettings.has_setting(key):
			original_settings[key] = ProjectSettings.get_setting(key)
		else:
			original_settings[key] = null
