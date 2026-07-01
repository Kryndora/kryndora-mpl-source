// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Client.Voice;
using Polytoria.Datamodel;
using Polytoria.Shared;

namespace Polytoria.Client.UI.Playerlist;

public partial class UILeaderboardUserOptions : Control
{
	[Export] private AnimationPlayer _animPlay = null!;
	[Export] private Control _optionsLayout = null!;
	[Export] private Button _addFriendBtn = null!;
	[Export] private Button _removeFriendBtn = null!;
	[Export] private Button _viewProfileBtn = null!;
	[Export] private Control _loaderView = null!;
	public bool Active { get; private set; } = false;
	public UILeaderboardUserItem? Target;
	private World _root = null!;
	private int _lastReq = 0;
	private Button _muteBtn = null!;

	public override void _Ready()
	{
		Visible = false;
		_viewProfileBtn.Pressed += OnViewProfile;
		_addFriendBtn.Pressed += OnAddFriend;
		_removeFriendBtn.Pressed += OnRemoveFriend;

		_muteBtn = (Button)_viewProfileBtn.Duplicate();
		_viewProfileBtn.GetParent().AddChild(_muteBtn);
		_muteBtn.Pressed += OnMute;
		SetMuteText("Mute");

		base._Ready();
	}

	private void SetMuteText(string text)
	{
		_muteBtn.Text = text;
		foreach (Node n in _muteBtn.FindChildren("*", "Label", true, false))
			if (n is Label l) l.Text = text;
	}

	private void OnMute()
	{
		if (Target == null) return;
		VoiceService.Instance?.ToggleMute(Target.TargetPlayer.UserID);
		UpdateMuteLabel();
	}

	private void UpdateMuteLabel()
	{
		if (Target == null) return;
		bool isSelf = Target.TargetPlayer == _root?.Players?.LocalPlayer;
		_muteBtn.Visible = !isSelf;
		bool muted = VoiceService.Instance?.IsMuted(Target.TargetPlayer.UserID) ?? false;
		SetMuteText(muted ? "Unmute" : "Mute");
	}

	private void OnAddFriend()
	{
		if (Target == null) return;
		_root.Social.LocalSendFriendshipRequest(Target.TargetPlayer, Datamodel.Services.SocialService.FriendshipRequestType.Friend);
		Disappear();
	}

	private void OnRemoveFriend()
	{
		if (Target == null) return;
		_root.Social.LocalSendFriendshipRequest(Target.TargetPlayer, Datamodel.Services.SocialService.FriendshipRequestType.Unfriend);
		Disappear();
	}

	private void OnViewProfile()
	{
		if (Target == null) return;

		OS.ShellOpen($"{Globals.MainEndpoint}u/{Target.TargetPlayer.Name}");

		Disappear();
	}

	private void ShowLoader(bool show)
	{
		_loaderView.Visible = show;
		_optionsLayout.Visible = !show;
	}

	public async void PopupAt(UILeaderboardUserItem item)
	{
		if (Active) return;
		_lastReq++;
		Active = true;
		Target = item;
		_root = Target.Leaderboard.CoreUI.Root;

		GlobalPosition = item.GetNode<Control>("InfoSpawn").GlobalPosition;
		_animPlay.Stop();
		_animPlay.Play("appear");

		UpdateMuteLabel();

		// TODO: Do add/remove friend options
		/*
		int myReq = _lastReq;
		
		ShowLoader(true);

		// Fetch friendship status
		bool isFriends = await _root.Social.WebCheckAreFriends(_root.Players.LocalPlayer.UserID, item.TargetPlayer.UserID);

		// If another option opened
		if (myReq != _lastReq) return;
		_addFriendBtn.Visible = !isFriends;
		_removeFriendBtn.Visible = isFriends;
		ShowLoader(false);
		*/
	}

	public void Disappear()
	{
		if (!Active) return;
		Active = false;
		Target = null;
		_animPlay.Stop();
		_animPlay.Play("disappear");
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton btn && btn.IsReleased())
		{
			if (!_optionsLayout.GetGlobalRect().HasPoint(btn.GlobalPosition))
			{
				if (Target != null && Target.GetGlobalRect().HasPoint(btn.GlobalPosition))
				{
					return; // Click was on the target item, ignore
				}

				Disappear();
			}
		}
		base._Input(@event);
	}
}
