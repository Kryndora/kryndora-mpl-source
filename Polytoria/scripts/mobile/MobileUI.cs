// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using DeepLinkAddon;
using Godot;
using Polytoria.Client;
using Polytoria.Mobile.UI;
using Polytoria.Mobile.Utils;
using Polytoria.Schemas.API;
using Polytoria.Shared;
using Polytoria.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Polytoria.Mobile;

public partial class MobileUI : Control
{
	public static MobileUI Singleton { get; private set; } = null!;
	private static readonly PTHttpClient _client = new();
	public MobileUI()
	{
		Singleton = this;
	}

	public event Action<MobileViewEnum>? ViewPathSwitched;

	private Control _mainView = null!;
	public MobileViewBase? CurrentViewNode;
	public MobileViewEnum CurrentView;

	[Export] public StartupSplash? StartSplash { get; private set; }
	[Export] public NewUserSplash NewUserSplash = null!;
	[Export] public MobileLoadingScreen LoadingScreen = null!;

	private Deeplink _deepLink = new();
	private readonly Dictionary<MobileViewEnum, MobileViewBase> _viewCache = [];

	public override void _Ready()
	{
		Dictionary<string, string> cmdargs = Globals.ReadCmdArgs();
		cmdargs.TryGetValue("token", out string? mobileToken);
		cmdargs.TryGetValue("code", out string? mobileCode);
		cmdargs.TryGetValue("state", out string? mobileState);

		AddChild(_deepLink, true);

		if (Globals.IsMobileBuild)
		{
			GetTree().Root.ContentScaleFactor = Globals.MobileScale;
		}

		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		if (StartSplash != null)
		{
			StartSplash!.Visible = true;
		}

		PolyMobileAuthAPI.UserAuthenticated += OnUserAuthenticated;
		PolyMobileAuthAPI.AskForAuthentication += OnAskForAuthentication;

		PolyMobileAuthAPI.SetupClient();
		if (mobileToken != null)
		{
			_ = PolyMobileAuthAPI.LoginWithAuthToken(mobileToken);
		}

		if (mobileCode != null && mobileState != null)
		{
			_ = PolyMobileAuthAPI.LoginWithCodeAndState(mobileCode, mobileState);
		}

		_deepLink.DeeplinkReceived += OnDeeplinkReceived;

		_mainView = GetNode<Control>("Layout/MainView");
		if (Globals.IsMobileBuild)
		{
			DisplayServer.ScreenSetOrientation(DisplayServer.ScreenOrientation.Portrait);
			DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
		}

		if (Globals.IsInGDEditor)
		{
			DisplayServer.WindowSetSize((Vector2I)new Vector2(412, 700));
		}

		SwitchTo(MobileViewEnum.Home);
	}

	private void OnUserAuthenticated(APIMeResponse me)
	{
		HideStartupSplash();
		if (NewUserSplash != null && IsInstanceValid(NewUserSplash))
		{
			NewUserSplash.Visible = false;
		}
	}

	private void OnAskForAuthentication()
	{
		HideStartupSplash();
		if (!Globals.IsInGDEditor)
		{
			NewUserSplash.ShowSplash();
		}
	}

	private void HideStartupSplash()
	{
		StartSplash?.HideSplash();
		StartSplash = null;
	}

	private async void OnDeeplinkReceived(DeeplinkURL url)
	{
		// Handle polytoria://auth link
		if (url.Host == "auth")
		{
			NameValueCollection authQuery = HttpUtility.ParseQueryString(url.Query);
			string code = authQuery.Get("code")!;
			string state = authQuery.Get("state")!;

			LoadingScreen.ShowScreen();
			await PolyMobileAuthAPI.LoginWithCodeAndState(code, state);
			LoadingScreen.HideScreen();
		}

		if (url.Host == "client")
		{
			PT.Print(url);
		}
	}

	public async void LaunchGame(int placeID)
	{
		LoadingScreen.ShowScreen();

		try
		{
			using HttpRequestMessage request = new(HttpMethod.Post, BuildUrl(Globals.MainEndpoint, "api/local/mobile-join/" + placeID));
			request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", PolyMobileAuthAPI.CurrentToken);
			request.Content = new ByteArrayContent([]);
			using HttpResponseMessage response = await _client.SendAsync(request);
			response.EnsureSuccessStatusCode();

			LocalMobileJoinResponse res = await response.Content.ReadFromJsonAsync(
				LocalMobileJoinGenerationContext.Default.LocalMobileJoinResponse);
			if (!res.Success || string.IsNullOrWhiteSpace(res.Address) || res.Port <= 0)
			{
				throw new InvalidOperationException("The game server could not be started.");
			}

			Node app = Globals.Singleton.SwitchEntry(Globals.AppEntryEnum.Client);
			if (app is ClientEntry ce)
			{
				ClientEntry.ClientEntryData entryData = new()
				{
					ConnectAddress = res.Address,
					ConnectPort = res.Port,
					Token = res.Token,
					TestUserID = res.UserID
				};
				ce.Entry(entryData);
			}
		}
		catch (Exception ex)
		{
			OS.Alert(ex.Message, "World join failed");
		}

		LoadingScreen.HideScreen();
	}

	public void ShowFriendActionSheet(int gameId, string gameName, int userId)
	{
		Control overlay = new() { Name = "FriendActionSheet", MouseFilter = MouseFilterEnum.Stop };
		overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		Button backdrop = new() { MouseFilter = MouseFilterEnum.Stop };
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		StyleBoxFlat dim = new() { BgColor = new Color(0, 0, 0, 0.55f) };
		backdrop.AddThemeStyleboxOverride("normal", dim);
		backdrop.AddThemeStyleboxOverride("hover", dim);
		backdrop.AddThemeStyleboxOverride("pressed", dim);
		backdrop.AddThemeStyleboxOverride("focus", dim);
		backdrop.Pressed += () => overlay.QueueFree();
		overlay.AddChild(backdrop);

		PanelContainer sheet = new()
		{
			AnchorLeft = 0f,
			AnchorRight = 1f,
			AnchorTop = 1f,
			AnchorBottom = 1f,
			OffsetTop = -310f
		};
		StyleBoxFlat sheetStyle = new()
		{
			BgColor = new Color(0.11f, 0.14f, 0.19f, 1f),
			CornerRadiusTopLeft = 22,
			CornerRadiusTopRight = 22,
			ContentMarginLeft = 22,
			ContentMarginRight = 22,
			ContentMarginTop = 24,
			ContentMarginBottom = 30
		};
		sheet.AddThemeStyleboxOverride("panel", sheetStyle);
		overlay.AddChild(sheet);

		VBoxContainer vbox = new();
		vbox.AddThemeConstantOverride("separation", 14);
		sheet.AddChild(vbox);

		Label titleLabel = new() { Text = gameName, HorizontalAlignment = HorizontalAlignment.Center };
		titleLabel.AddThemeFontSizeOverride("font_size", 26);
		titleLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1, 1));
		vbox.AddChild(titleLabel);

		Button join = MakeSheetButton("Join", new Color(0.03f, 0.03f, 0.05f, 1f), new Color(1, 1, 1, 1));
		join.Pressed += () => { overlay.QueueFree(); LaunchGame(gameId); };
		vbox.AddChild(join);

		Button profile = MakeSheetButton("View Profile", new Color(0.18f, 0.22f, 0.29f, 1f), new Color(0.86f, 0.91f, 0.97f, 1f));
		profile.Pressed += () => { overlay.QueueFree(); SwitchTo(MobileViewEnum.Profile, userId); };
		vbox.AddChild(profile);

		Button cancel = MakeSheetButton("Cancel", new Color(0.14f, 0.17f, 0.22f, 1f), new Color(0.7f, 0.75f, 0.82f, 1f));
		cancel.Pressed += () => overlay.QueueFree();
		vbox.AddChild(cancel);

		AddChild(overlay);
	}

	private static Button MakeSheetButton(string text, Color bg, Color fg)
	{
		Button b = new() { Text = text, CustomMinimumSize = new Vector2(0, 56) };
		StyleBoxFlat style = new()
		{
			BgColor = bg,
			CornerRadiusTopLeft = 14,
			CornerRadiusTopRight = 14,
			CornerRadiusBottomLeft = 14,
			CornerRadiusBottomRight = 14
		};
		b.AddThemeStyleboxOverride("normal", style);
		b.AddThemeStyleboxOverride("hover", style);
		b.AddThemeStyleboxOverride("pressed", style);
		b.AddThemeStyleboxOverride("focus", style);
		b.AddThemeColorOverride("font_color", fg);
		b.AddThemeColorOverride("font_hover_color", fg);
		b.AddThemeColorOverride("font_pressed_color", fg);
		b.AddThemeFontSizeOverride("font_size", 20);
		return b;
	}

	private static string BuildUrl(string endpoint, string path)
	{
		return endpoint.TrimEnd('/') + "/" + path.TrimStart('/');
	}

	public void SwitchTo(MobileViewEnum viewEnum, object? args = null)
	{
		if (viewEnum == CurrentView)
		{
			return;
		}

		if (CurrentViewNode != null)
		{
			CurrentViewNode.HideView();
			CurrentViewNode.Visible = false;
		}

		// Check if cached
		if (!_viewCache.TryGetValue(viewEnum, out MobileViewBase? page))
		{
			PT.Print("Loading ", viewEnum);
			string pathToLoad = viewEnum switch
			{
				MobileViewEnum.Home => "res://scenes/mobile/views/home.tscn",
				MobileViewEnum.Worlds => "res://scenes/mobile/views/worlds.tscn",
				MobileViewEnum.PlaceInfo => "res://scenes/mobile/views/place_info.tscn",
				MobileViewEnum.Avatar => "res://scenes/mobile/views/avatar.tscn",
				MobileViewEnum.Profile => "res://scenes/mobile/views/profile.tscn",
				MobileViewEnum.Dev => "res://scenes/mobile/views/test.tscn",
				_ => throw new ArgumentOutOfRangeException(nameof(viewEnum),
					 $"No scene defined for {viewEnum}")
			};

			PT.Print("Loading ", viewEnum);

			PackedScene packed = ResourceLoader.Load<PackedScene>(pathToLoad, cacheMode: ResourceLoader.CacheMode.IgnoreDeep);
			page = packed.Instantiate<MobileViewBase>();
			_viewCache[viewEnum] = page;
			_mainView.AddChild(page);
			page.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		}

		CurrentViewNode = page;
		page.ShowView(args);
		page.Visible = true;
		ViewPathSwitched?.Invoke(viewEnum);
	}
}

public enum MobileViewEnum
{
	None,
	Home,
	Worlds,
	Avatar,
	Store,
	Dev,
	PlaceInfo,
	Profile
}

public struct LocalMobileJoinResponse
{
	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("address")]
	public string Address { get; set; }

	[JsonPropertyName("port")]
	public int Port { get; set; }

	[JsonPropertyName("userID")]
	public int UserID { get; set; }

	[JsonPropertyName("placeID")]
	public int PlaceID { get; set; }

	[JsonPropertyName("token")]
	public string Token { get; set; }
}

[JsonSerializable(typeof(LocalMobileJoinResponse))]
internal partial class LocalMobileJoinGenerationContext : JsonSerializerContext { }
