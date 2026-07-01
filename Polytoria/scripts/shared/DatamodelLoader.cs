// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Attributes;
using Polytoria.Datamodel;
using Polytoria.Datamodel.Resources;
using Polytoria.Formats;
using Polytoria.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Script = Polytoria.Datamodel.Script;

namespace Polytoria.Shared;

public static class DatamodelLoader
{
	public static async Task LoadWorldFile(World root, string filePath, string? entryPath = null)
	{
		await LoadWorldBytes(root, File.ReadAllBytes(filePath), entryPath);
	}

	public static async Task<Instance?> LoadModelFile(World root, string filePath, Instance? parent = null)
	{
		string ext = Path.GetExtension(filePath).ToLowerInvariant();
		if (ext is ".fbx" or ".obj" or ".gltf" or ".glb")
			return await LoadForeignModel(root, filePath, parent);

		return await LoadModelBytes(root, await File.ReadAllBytesAsync(filePath), parent);
	}

	/// <summary>
	/// Imports an FBX or glTF/GLB file at runtime using Godot's built-in document loaders.
	/// Returns a Kryndora Model containing one Mesh child per imported mesh.
	/// </summary>
	private static async Task<Polytoria.Datamodel.Model?> LoadForeignModel(World root, string filePath, Instance? parent)
	{
		parent ??= root.Environment;
		string ext       = Path.GetExtension(filePath).ToLowerInvariant();
		string loadPath = filePath;
		string modelName = Path.GetFileNameWithoutExtension(filePath);
		string? convertedPath = null;

		if (ext is ".fbx" or ".obj")
		{
			convertedPath = await TryConvertToGlb(filePath);
			if (!string.IsNullOrWhiteSpace(convertedPath))
			{
				loadPath = convertedPath;
			}
		}

		if (!LocalMeshAsset.TryLoadScene(loadPath, out PackedScene? previewScene, out string? error))
		{
			if (ext == ".fbx" && loadPath != filePath)
			{
				throw new InvalidDataException(error ?? $"Could not load converted FBX model: {filePath}");
			}

			throw new InvalidDataException(
				(error ?? $"Could not load foreign model: {filePath}") +
				"\n\nFBX/OBJ support is limited. Install Blender or export the model as .glb/.gltf and import that instead."
			);
		}

		LocalMeshAsset.ImportedMeshInfo[] importedMeshes = LocalMeshAsset.GetImportedMeshes(previewScene!);
		if (importedMeshes.Length == 0)
		{
			throw new InvalidDataException($"Imported model '{Path.GetFileName(filePath)}' did not contain any meshes.");
		}

		Polytoria.Datamodel.Model model = Globals.LoadInstance<Polytoria.Datamodel.Model>(root);
		model.Name = modelName;
#if CREATOR
		model.CreatorInserted();
#endif

		List<Polytoria.Datamodel.Mesh> meshChildren = [];
		List<PackedScene> meshScenes = [];
		List<string> meshNodePaths = [];
		List<int> meshSurfaceIndices = [];

		foreach (LocalMeshAsset.ImportedMeshInfo importedMesh in importedMeshes)
		{
			if (!LocalMeshAsset.TryLoadScene(loadPath, out PackedScene? meshScene, out string? meshError, importedMesh.NodePath, importedMesh.SurfaceIndex))
			{
				PT.PrintErr(meshError ?? $"Could not split imported mesh '{importedMesh.Name}'.");
				continue;
			}

			Polytoria.Datamodel.Mesh mesh = Globals.LoadInstance<Polytoria.Datamodel.Mesh>(root);
			mesh.Name = MakeSafeInstanceName(importedMesh.Name);
			// Every split mesh keeps the original scene hierarchy and transform.
			// Centering each child independently would destroy the model assembly.
			mesh.IncludeOffset = true;
#if CREATOR
			mesh.CreatorInserted();
#endif
			meshChildren.Add(mesh);
			meshScenes.Add(meshScene!);
			meshNodePaths.Add(importedMesh.NodePath);
			meshSurfaceIndices.Add(importedMesh.SurfaceIndex);
		}

		if (meshChildren.Count == 0)
		{
			throw new InvalidDataException($"Imported model '{Path.GetFileName(filePath)}' could not be split into editable mesh children.");
		}

#if CREATOR
		// Go through the History API so the instance is properly registered
		// in the editor explorer (with undo support).
		root.CreatorContext.History.CreateInstances([model], parent);
		root.CreatorContext.History.CreateInstances([.. meshChildren], model);
#else
		model.Parent = parent;
		foreach (Polytoria.Datamodel.Mesh mesh in meshChildren)
		{
			mesh.Parent = model;
		}
#endif

#if CREATOR
		if (root.SessionType == World.SessionTypeEnum.Creator && root.LinkedSession != null)
		{
			string projectPath = await ImportForeignModelFileToProject(root, loadPath, modelName);
			FileLinkAsset fileLink = root.Assets.GetFileLinkByPath(projectPath);
			for (int i = 0; i < meshChildren.Count; i++)
			{
				meshChildren[i].LoadLocalFile(fileLink, meshScenes[i], meshNodePaths[i], meshSurfaceIndices[i]);
			}
		}
		else
#endif
		{
			for (int i = 0; i < meshChildren.Count; i++)
			{
				meshChildren[i].LoadLocalScene(meshScenes[i]);
			}
		}

		foreach (Polytoria.Datamodel.Mesh mesh in meshChildren)
		{
			mesh.IncludeOffset = true;
		}

		return model;
	}

#if CREATOR
	private static async Task<string> ImportForeignModelFileToProject(World root, string sourcePath, string? modelNameOverride = null)
	{
		string fileName = MakeSafeFileName(Path.GetFileName(sourcePath));
		if (!string.IsNullOrWhiteSpace(modelNameOverride))
		{
			fileName = MakeSafeFileName(modelNameOverride + Path.GetExtension(sourcePath).ToLowerInvariant());
		}

		string ext = Path.GetExtension(fileName);
		string baseName = Path.GetFileNameWithoutExtension(fileName);
		string relativePath = Path.Join("assets", "meshes", fileName).SanitizePath();
		string absolutePath = Path.GetFullPath(Path.Join(root.LinkedSession.ProjectFolderPath, relativePath));

		int counter = 2;
		while (File.Exists(absolutePath))
		{
			relativePath = Path.Join("assets", "meshes", $"{baseName}_{counter}{ext}").SanitizePath();
			absolutePath = Path.GetFullPath(Path.Join(root.LinkedSession.ProjectFolderPath, relativePath));
			counter++;
		}

		root.IO.WriteBytesToPath(relativePath, await File.ReadAllBytesAsync(sourcePath));
		return relativePath;
	}

	private static string MakeSafeFileName(string fileName)
	{
		foreach (char invalid in Path.GetInvalidFileNameChars())
		{
			fileName = fileName.Replace(invalid, '_');
		}

		return string.IsNullOrWhiteSpace(fileName) ? $"mesh_{Guid.NewGuid():N}.glb" : fileName;
	}
#endif

	private static string MakeSafeInstanceName(string name)
	{
		name = string.IsNullOrWhiteSpace(name) ? "Mesh" : name.Trim();
		foreach (char invalid in new[] { '.', '/', '\\' })
		{
			name = name.Replace(invalid, '_');
		}
		return string.IsNullOrWhiteSpace(name) ? "Mesh" : name;
	}

	private static async Task<string?> TryConvertToGlb(string modelPath)
	{
		string? blenderPath = FindBlenderExecutable();
		if (string.IsNullOrWhiteSpace(blenderPath))
		{
			return null;
		}

		string ext = Path.GetExtension(modelPath).ToLowerInvariant();
		string tempDir = Path.GetFullPath(Path.Join(Path.GetTempPath(), "kryndora_model_import"));
		Directory.CreateDirectory(tempDir);

		string outputPath = Path.Join(tempDir, $"{Path.GetFileNameWithoutExtension(modelPath)}_{Guid.NewGuid():N}.glb");
		string scriptPath = Path.Join(tempDir, $"convert_{Guid.NewGuid():N}.py");
		string importCommand = ext == ".obj"
			? $$"""
if hasattr(bpy.ops.wm, "obj_import"):
    bpy.ops.wm.obj_import(filepath={{ToPythonStringLiteral(modelPath)}})
else:
    bpy.ops.import_scene.obj(filepath={{ToPythonStringLiteral(modelPath)}})
"""
			: $$"""
bpy.ops.import_scene.fbx(filepath={{ToPythonStringLiteral(modelPath)}})
""";

		string script = $$"""
import bpy
import sys

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

# Blender 5.1's FBX importer still writes this old Cycles property for lights.
# Add a no-op compatibility property so FBX files containing lights keep importing.
_kryndora_probe_light = bpy.data.lights.new(name="_kryndora_fbx_light_probe", type='POINT')
_kryndora_cycles_type = type(_kryndora_probe_light.cycles)
if not hasattr(_kryndora_probe_light.cycles, "cast_shadow"):
    setattr(_kryndora_cycles_type, "cast_shadow", property(lambda self: True, lambda self, value: None))
bpy.data.lights.remove(_kryndora_probe_light)

{{importCommand}}

for obj in bpy.context.scene.objects:
    obj.select_set(True)

bpy.ops.export_scene.gltf(
    filepath={{ToPythonStringLiteral(outputPath)}},
    export_format='GLB',
    export_apply=True
)
""";

		await File.WriteAllTextAsync(scriptPath, script);

		ProcessStartInfo startInfo = new()
		{
			FileName = blenderPath,
			Arguments = $"--background --factory-startup --python \"{scriptPath}\"",
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardError = true,
			RedirectStandardOutput = true
		};

		using Process process = new() { StartInfo = startInfo };

		try
		{
			process.Start();
			string stdout = await process.StandardOutput.ReadToEndAsync();
			string stderr = await process.StandardError.ReadToEndAsync();
			await process.WaitForExitAsync();

			if (process.ExitCode != 0 || !File.Exists(outputPath))
			{
				PT.PrintErr("Blender model conversion failed. ", stdout, stderr);
				return null;
			}

			return outputPath;
		}
		catch (Exception ex)
		{
			PT.PrintErr("Blender model conversion failed. ", ex);
			return null;
		}
		finally
		{
			try
			{
				if (File.Exists(scriptPath))
				{
					File.Delete(scriptPath);
				}
			}
			catch
			{
				// Best effort cleanup only.
			}
		}
	}

	private static string? FindBlenderExecutable()
	{
		string? envPath = System.Environment.GetEnvironmentVariable("KRYNDORA_BLENDER_PATH");
		if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
		{
			return envPath;
		}

		string[] pathEntries = (System.Environment.GetEnvironmentVariable("PATH") ?? "")
			.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		foreach (string pathEntry in pathEntries)
		{
			string candidate = Path.Join(pathEntry, OperatingSystem.IsWindows() ? "blender.exe" : "blender");
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		if (OperatingSystem.IsWindows())
		{
			string programFiles = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
			string blenderRoot = Path.Join(programFiles, "Blender Foundation");
			if (Directory.Exists(blenderRoot))
			{
				string? newest = Directory.GetFiles(blenderRoot, "blender.exe", SearchOption.AllDirectories)
					.OrderByDescending(File.GetLastWriteTimeUtc)
					.FirstOrDefault();
				if (!string.IsNullOrWhiteSpace(newest))
				{
					return newest;
				}
			}
		}

		return null;
	}

	private static string ToPythonStringLiteral(string value)
	{
		StringBuilder sb = new("r\"");
		sb.Append(value.Replace("\"", "\\\""));
		sb.Append('"');
		return sb.ToString();
	}

	public static async Task LoadWorldBytes(World root, byte[] data, string? entryPath = null)
	{
		PolyFileTypeEnum fileType = DetermineFileTypeFromBytes(data);
		PT.Print("Determined file type: ", fileType);
		if (fileType == PolyFileTypeEnum.PolyXML)
		{
			if (data[0] == 0xEF)
			{
				data = [.. data.Skip(3)];
			}

			// XML Format
			await XmlFormat.LoadString(root, data.GetStringFromUtf8());
		}
		else if (fileType == PolyFileTypeEnum.Packed)
		{
			// Poly Format
			PackedFormat.LoadPackedWorld(root, data, entryPath);
		}
	}

	public static async Task<string> GetImportFolderName(byte[] data)
	{
		PolyFileTypeEnum fileType = DetermineFileTypeFromBytes(data);
		if (fileType == PolyFileTypeEnum.PolyXML)
		{
			if (data[0] == 0xEF)
			{
				data = [.. data.Skip(3)];
			}

			XmlFormat.GameItem gameItem = XmlFormat.ParseContent(data.GetStringFromUtf8());

			return gameItem.Name ?? "Model";
		}
		else if (fileType == PolyFileTypeEnum.Packed)
		{
			PackedFormat.ModelData? modelData = PackedFormat.ReadModelData(data);

			if (modelData == null) return "";
			return modelData.Value.ModelName;
		}
		return "";
	}

	public static async Task<Instance?> LoadModelBytes(World root, byte[] data, Instance? parent = null, string? modelNameOverride = null)
	{
		parent ??= root.TemporaryContainer;

		PolyFileTypeEnum fileType = DetermineFileTypeFromBytes(data);
		string modelName = modelNameOverride ?? await GetImportFolderName(data);
		string baseFolder = Globals.ToolboxFolderName + "/" + modelName + "/";

		if (fileType == PolyFileTypeEnum.PolyXML)
		{
			if (data[0] == 0xEF)
			{
				data = [.. data.Skip(3)];
			}

			// XML Format
			Instance? m = await XmlFormat.LoadModelString(root, data.GetStringFromUtf8(), parent);

			if (m != null)
			{
				// iterate through scripts
				foreach (Instance item in m.GetDescendants())
				{
					item.ModelRoot = m;
					if (item is Script s)
					{
						string scriptPath = baseFolder + s.CreateLuaFileName();
						root.IO.WriteBytesToPath(scriptPath, s.Source.ToUtf8Buffer());
						s.LinkedScript = root.Assets.GetFileLinkByPath(scriptPath);
					}
				}
#if CREATOR
				// Save model to linked session
				if (root.LinkedSession != null)
				{
					string modelPath = baseFolder + modelName + ".model";
					SaveImportedModelIfPossible(m, modelPath);
				}
#endif
			}
			return m;
		}
		else if (fileType == PolyFileTypeEnum.Packed)
		{
			Instance? packedModel = PackedFormat.LoadPackedModel(root, data, parent);
			if (packedModel != null)
			{
#if CREATOR
				// Save model to linked session
				if (root.LinkedSession != null)
				{
					string modelPath = baseFolder + modelName + ".model";
					SaveImportedModelIfPossible(packedModel, modelPath);
				}
#endif
				return packedModel;
			}
		}
		return null;
	}

#if CREATOR
	private static void SaveImportedModelIfPossible(Instance instance, string modelPath)
	{
		if (instance.GetType().IsDefined(typeof(StaticAttribute), true))
		{
			PT.PrintWarn("Imported model root is a static class; skipping linked model save for ", instance.Name);
			return;
		}

		instance.Root.LinkedSession.SaveModel(instance, modelPath);
	}
#endif

	public static async Task<PolyFileTypeEnum> DetermineFileType(string filePath)
	{
		byte[] b = await File.ReadAllBytesAsync(filePath);
		return DetermineFileTypeFromBytes(b);
	}

	public static PolyFileTypeEnum DetermineFileTypeFromBytes(byte[] data)
	{
		if (data.Length <= 0) return PolyFileTypeEnum.Empty;
		if (data[0] == 0xEF || data[0] == 0x3C)
		{
			return PolyFileTypeEnum.PolyXML;
		}
		else
		{
			return PolyFileTypeEnum.Packed;
		}
	}
}

public enum PolyFileTypeEnum
{
	Packed,
	PolyXML,
	Empty
}
