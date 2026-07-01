// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Polytoria.Renderer;

public partial class RendererEntry : AppEntry
{
	private readonly ConcurrentQueue<string> _requests = new();
	private bool _daemon;
	private bool _rendering;
	private volatile bool _stdinClosed;

	public override void _Ready()
	{
		Dictionary<string, string> args = Globals.ReadCmdArgs();
		if (args.ContainsKey("daemon"))
		{
			StartDaemon();
		}
		else
		{
			_ = RenderOnce(args);
		}
	}

	private void StartDaemon()
	{
		_daemon = true;
		SetProcess(true);
		Console.WriteLine("AVATAR_RENDER: daemon-ready");
		Console.Out.Flush();

		Thread reader = new(ReadStdinLoop) { IsBackground = true };
		reader.Start();
	}

	private void ReadStdinLoop()
	{
		try
		{
			string? line;
			while ((line = Console.In.ReadLine()) != null)
			{
				string trimmed = line.Trim();
				if (trimmed.Length > 0)
					_requests.Enqueue(trimmed);
			}
		}
		catch
		{
		}
		_stdinClosed = true;
	}

	public override void _Process(double delta)
	{
		if (!_daemon)
			return;

		if (_stdinClosed && _requests.IsEmpty && !_rendering)
		{
			GetTree().Quit(0);
			return;
		}

		if (_rendering)
			return;

		if (_requests.TryDequeue(out string? request))
		{
			_rendering = true;
			_ = HandleRequest(request);
		}
	}

	private async Task HandleRequest(string request)
	{
		string outputPath = "";
		try
		{
			string[] parts = request.Split('|');
			int avatarId = int.Parse(parts[0]);
			outputPath = parts[1];
			RendererViewport.AvatarPhotoTypeEnum photoType = parts.Length > 2 && parts[2] == "head"
				? RendererViewport.AvatarPhotoTypeEnum.Headshot
				: RendererViewport.AvatarPhotoTypeEnum.FullAvatar;

			RendererViewport viewport = new();
			AddChild(viewport);
			viewport.Setup();
			await viewport.AddAvatar(avatarId, photoType);
			byte[] png = await viewport.SavePng();

			string? directory = Path.GetDirectoryName(outputPath);
			if (!string.IsNullOrWhiteSpace(directory))
				Directory.CreateDirectory(directory);
			File.WriteAllBytes(outputPath, png);

			viewport.QueueFree();

			Console.WriteLine($"AVATAR_RENDER: done|{outputPath}");
			Console.Out.Flush();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"AVATAR_RENDER: error|{outputPath}|{ex.Message}");
			Console.Out.Flush();
		}
		finally
		{
			_rendering = false;
		}
	}

	private async Task RenderOnce(Dictionary<string, string> args)
	{
		try
		{
			int avatarId = args.TryGetValue("avatar-id", out string? avatarIdValue) && int.TryParse(avatarIdValue, out int parsedAvatarId)
				? parsedAvatarId
				: 64499;
			string outputPath = args.TryGetValue("output", out string? outputValue) && !string.IsNullOrWhiteSpace(outputValue)
				? outputValue
				: ProjectSettings.GlobalizePath("res://temp/test.png");

			Stopwatch sw = Stopwatch.StartNew();
			RendererViewport viewport = new();
			AddChild(viewport);
			viewport.Setup();
			PT.Print("Viewport setup: ", sw.ElapsedMilliseconds, "ms");
			Console.WriteLine($"AVATAR_RENDER: viewport-ready {sw.ElapsedMilliseconds}ms");

			RendererViewport.AvatarPhotoTypeEnum photoType = args.ContainsKey("headshot")
				? RendererViewport.AvatarPhotoTypeEnum.Headshot
				: RendererViewport.AvatarPhotoTypeEnum.FullAvatar;

			sw.Restart();
			PT.Print("Loading avatar ", avatarId, "...");
			Console.WriteLine($"AVATAR_RENDER: loading-avatar {avatarId}");
			string? bodyOverride = args.TryGetValue("body", out string? bodyValue) && !string.IsNullOrWhiteSpace(bodyValue)
				? bodyValue
				: null;
			string? shirtOverride = args.TryGetValue("shirt", out string? shirtValue) && !string.IsNullOrWhiteSpace(shirtValue)
				? shirtValue
				: null;
			await viewport.AddAvatar(avatarId, photoType, bodyOverride, shirtOverride);
			PT.Print("Load avatar: ", sw.ElapsedMilliseconds, "ms");
			Console.WriteLine($"AVATAR_RENDER: avatar-ready {sw.ElapsedMilliseconds}ms");

			sw.Restart();
			Console.WriteLine("AVATAR_RENDER: saving-png");
			byte[] png = await viewport.SavePng();
			string? outputDirectory = Path.GetDirectoryName(outputPath);
			if (!string.IsNullOrWhiteSpace(outputDirectory))
				Directory.CreateDirectory(outputDirectory);
			File.WriteAllBytes(outputPath, png);
			PT.Print("Saved avatar to ", outputPath, " in ", sw.ElapsedMilliseconds, "ms");
			Console.WriteLine($"AVATAR_RENDER: saved {outputPath} {png.Length}bytes {sw.ElapsedMilliseconds}ms");
			GetTree().Quit(0);
		}
		catch (Exception ex)
		{
			PT.PrintErr("Avatar render failed: ", ex);
			Console.Error.WriteLine($"AVATAR_RENDER: failed {ex}");
			GetTree().Quit(1);
		}
	}
}
