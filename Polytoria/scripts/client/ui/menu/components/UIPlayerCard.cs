// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;
using Polytoria.Datamodel.Resources;
using Polytoria.Shared;

namespace Polytoria.Client.UI;

public partial class UIPlayerCard : Node
{
	[Export] private Button _reportButton = null!;
	[Export] private Button _profileButton = null!;

	private Button _addFriendButton = null!;

	public Player TargetPlayer = null!;

	private PTImageAsset _plrIconAsset = null!;

	public override void _Ready()
	{
		_plrIconAsset = new();
		GetNode<Label>("Layout/Label").Text = TargetPlayer.Name;

		_plrIconAsset.ResourceLoaded += OnIconLoaded;
		_plrIconAsset.ImageType = ImageTypeEnum.UserAvatarHeadshot;
		_plrIconAsset.ImageID = (uint)TargetPlayer.UserID;
		_plrIconAsset.LoadResource();

		_addFriendButton = GetNode<Button>("Layout/Layout/AddFriend");
		_addFriendButton.Icon = GD.Load<Texture2D>("res://assets/textures/ui-icons/add-friend.png");

		Player? local = TargetPlayer.Root?.Players?.LocalPlayer;
		if (local != null && local == TargetPlayer)
		{
			_addFriendButton.Visible = false;
		}
		else
		{
			_addFriendButton.Pressed += OnAddFriend;
			RefreshFriendState();
		}

		_profileButton.Pressed += OnProfile;
		_reportButton.Pressed += OnReport;
	}

	private async void RefreshFriendState()
	{
		Player? local = TargetPlayer.Root?.Players?.LocalPlayer;
		if (local == null) return;

		try
		{
			bool areFriends = await TargetPlayer.Root.Social.WebCheckAreFriends(local.UserID, TargetPlayer.UserID);
			if (!IsInstanceValid(this) || !areFriends) return;

			_addFriendButton.Pressed -= OnAddFriend;
			_addFriendButton.Icon = GD.Load<Texture2D>("res://assets/textures/ui-icons/accept-friend.png");
			_addFriendButton.MouseFilter = Control.MouseFilterEnum.Ignore;
			_addFriendButton.FocusMode = Control.FocusModeEnum.None;
		}
		catch { }
	}

	private void OnAddFriend()
	{
		TargetPlayer.Root.Social.LocalSendFriendshipRequest(TargetPlayer, Datamodel.Services.SocialServiceKryndora.FriendshipRequestType.Friend);
	}

	private void OnReport()
	{
		OS.ShellOpen(Globals.MainEndpoint + "report/user/" + TargetPlayer.UserID);
	}

	private void OnProfile()
	{
		OS.ShellOpen(Globals.MainEndpoint + "users/" + TargetPlayer.UserID);
	}

	public override void _ExitTree()
	{
		_plrIconAsset.ResourceLoaded -= OnIconLoaded;
		_addFriendButton.Pressed -= OnAddFriend;
		_profileButton.Pressed -= OnProfile;
		_reportButton.Pressed -= OnReport;
		base._ExitTree();
	}

	private void OnIconLoaded(Resource resource)
	{
		GetNode<TextureRect>("Layout/Icon").Texture = (Texture2D)resource;
	}
}
