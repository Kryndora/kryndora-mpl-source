// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel.Resources;
using Polytoria.Mobile;
using Polytoria.Schemas.API;
using Polytoria.Shared;
using Polytoria.Utils;
using System;

namespace Polytoria.Mobile.UI;

public partial class UserHeadshotCard : Node
{
	[Export] public uint UserID;
	public string? OverrideUsername { get; set; }
	public bool IsOnline { get; set; }
	public string StatusText { get; set; } = "";
	public int? GameId { get; set; }
	public string GameName { get; set; } = "";

	[Export] private TextureRect _imageRect = null!;
	[Export] private Label _usernameLabel = null!;

	private readonly PTImageAsset _iconAsset = new();

	public override void _Ready()
	{
		_imageRect.Texture = null;
		_usernameLabel.Text = "";
		_iconAsset.ResourceLoaded += OnIconLoaded;
		if (IsOnline)
		{
			AddOnlineDot();
		}
		AddTapToProfile();
		LoadUserCard();
	}

	private void AddOnlineDot()
	{
		Panel dot = new()
		{
			CustomMinimumSize = new(20, 20),
			OffsetLeft = 74,
			OffsetTop = 74,
			OffsetRight = 96,
			OffsetBottom = 96,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		StyleBoxFlat style = new()
		{
			BgColor = new(0.15f, 0.9f, 0.38f, 1),
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerRadiusBottomRight = 10,
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			BorderColor = new(0.07f, 0.13f, 0.2f, 1)
		};
		dot.AddThemeStyleboxOverride("panel", style);
		GetNode<Panel>("VBoxContainer/Panel").AddChild(dot);
	}

	private void AddTapToProfile()
	{
		Button tap = new() { MouseFilter = Control.MouseFilterEnum.Stop };
		tap.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		tap.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		tap.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		tap.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		tap.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		tap.Pressed += () =>
		{
			if (GameId.HasValue)
			{
				MobileUI.Singleton.ShowFriendActionSheet(GameId.Value, GameName, (int)UserID);
			}
			else
			{
				MobileUI.Singleton.SwitchTo(MobileViewEnum.Profile, (int)UserID);
			}
		};
		AddChild(tap);
	}

	private void OnIconLoaded(Resource resource)
	{
		_imageRect.Texture = (Texture2D)resource;
	}

	public async void LoadUserCard()
	{
		_iconAsset.ImageType = ImageTypeEnum.UserAvatarHeadshot;
		_iconAsset.ImageID = UserID;
		_iconAsset.LoadResource();

		try
		{
			if (!string.IsNullOrWhiteSpace(OverrideUsername))
			{
				_usernameLabel.Text = OverrideUsername;
			}
			else
			{
				APIUserInfo userData = await PolyAPI.GetUserFromID((int)UserID);
				_usernameLabel.Text = userData.Username;
			}
		}
		catch (Exception ex)
		{
			PT.PrintErr(ex);
		}
	}
}
