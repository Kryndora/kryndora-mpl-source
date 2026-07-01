// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Creator.Mcp;
using Polytoria.Client.Settings.Appliers;
using Polytoria.Creator.Managers;
using Polytoria.Creator.Settings;
using Polytoria.Creator.Utils;
using Polytoria.Datamodel.Creator;
using Polytoria.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace Polytoria.Creator;

public partial class CreatorEntry : Node
{
	public const int CreatorPort = 24220;
	private ConfirmationDialog? _creatorAuthDialog;
	private bool _isPollingCreatorAuth;

	public async override void _EnterTree()
	{
		Dictionary<string, string> cmdargs = Globals.ReadCmdArgs();
		cmdargs.TryGetValue("token", out string? launchToken);

		CreatorService creatorService = new();
		AddChild(creatorService);

		AddChild(new CreatorMcpBridge { Name = "CreatorMcpBridge" });

		CreatorSettingsService creatorSettingsService = new()
		{
			Name = "CreatorSettingsService"
		};
		AddChild(creatorSettingsService, true, InternalMode.Front);
		creatorSettingsService.Init();

		creatorSettingsService.AddChild(new GraphicsSettingsApplier { Name = GraphicsSettingsApplier.NodeName, Settings = creatorSettingsService }, true, InternalMode.Front);

		GetViewport().GuiEmbedSubwindows = true;

		// Open project
		cmdargs.TryGetValue("proj", out string? creatorFilePath);
		if (creatorFilePath != null)
		{
			_ = CreatorService.Singleton.CreateNewSession(creatorFilePath);
		}

		// Import legacy world cmd arguments
		cmdargs.TryGetValue("liin", out string? legacyImportIn);
		cmdargs.TryGetValue("liout", out string? legacyImportOut);

		if (legacyImportIn != null && legacyImportOut != null)
		{
			_ = ProjectManager.ImportLegacyWorld(legacyImportIn, legacyImportOut, new() { MainWorld = "main.poly", ProjectName = new DirectoryInfo(legacyImportOut).Name });
		}

		// Login creator with token
		await EnsureCreatorAuthentication(launchToken);
	}

	private async Task EnsureCreatorAuthentication(string? launchToken)
	{
		if (!string.IsNullOrWhiteSpace(launchToken))
		{
			try
			{
				await PolyCreatorAPI.LoginWithToken(launchToken);
				if (PolyCreatorAPI.IsUserAuthenticated)
					return;
			}
			catch
			{
			}
		}

		if (await PolyCreatorAPI.TryLoginFromLocalSession())
			return;

		ShowCreatorAuthDialog();
	}

	private void ShowCreatorAuthDialog()
	{
		if (_creatorAuthDialog != null)
			return;

		_creatorAuthDialog = new()
		{
			Title = "Welcome to Kryndora Studio",
			DialogText = "Hey, welcome to Kryndora Studio.\n\nPlease create an account or log in on the Kryndora website before using Studio. This keeps Play/Test sessions connected to your real account.",
			DialogCloseOnEscape = false,
			Exclusive = true
		};

		_creatorAuthDialog.GetOkButton().Text = "Login / Create";
		_creatorAuthDialog.GetCancelButton().Text = "Close";
		_creatorAuthDialog.Confirmed += () =>
		{
			OS.ShellOpen(Globals.MainEndpoint.TrimEnd('/') + "/auth/creator");
			if (!_isPollingCreatorAuth)
				_ = PollCreatorAuthentication();
		};
		_creatorAuthDialog.Canceled += () => GetTree().Quit();

		AddChild(_creatorAuthDialog);
		_creatorAuthDialog.PopupCentered(new Vector2I(540, 260));
	}

	private async Task PollCreatorAuthentication()
	{
		_isPollingCreatorAuth = true;
		for (int i = 0; i < 180 && !PolyCreatorAPI.IsUserAuthenticated; i++)
		{
			if (await PolyCreatorAPI.TryLoginFromLocalSession())
				break;

			await Task.Delay(1000);
		}

		_isPollingCreatorAuth = false;
		if (!PolyCreatorAPI.IsUserAuthenticated || _creatorAuthDialog == null)
			return;

		_creatorAuthDialog.QueueFree();
		_creatorAuthDialog = null;
	}
}
