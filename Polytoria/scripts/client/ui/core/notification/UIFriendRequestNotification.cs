// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;
using Polytoria.Datamodel.Services;

namespace Polytoria.Client.UI.Notification;

public partial class UIFriendRequestNotification : UINotificationBase
{
	[Export] private AnimationPlayer _animPlay = null!;
	[Export] private TextureRect _iconRect = null!;
	[Export] private Button _viewButton = null!;

	private const string NameLabelPath = "Control/Control/Panel/MarginContainer/HBoxContainer/VBoxContainer/VBoxContainer/Label2";
	private const string AcceptButtonPath = "Control/Control/Panel/MarginContainer/HBoxContainer/VBoxContainer/HBoxContainer/Save";
	private const string IgnoreButtonPath = "Control/Control/Panel/MarginContainer/HBoxContainer/VBoxContainer/HBoxContainer/View";

	public override void Fire(object? data)
	{
		if (data is FriendRequestNotifyPayload payload)
		{
			GetNode<Label>(NameLabelPath).Text = payload.FromName;

			int fromId = payload.FromUserId;
			string fromName = payload.FromName;
			GetNode<Button>(AcceptButtonPath).Pressed += () => OnAccept(fromId, fromName);
			GetNode<Button>(IgnoreButtonPath).Pressed += QueueFree;

			_animPlay.Play("appear");
		}
		else
		{
			QueueFree();
		}
	}

	private void OnAccept(int fromUserId, string fromName)
	{
		NotificationCenter.CoreUI.Root.Social.LocalAcceptFriendRequest(fromUserId);
		NotificationCenter.FireMessage("You are now friends with " + fromName + "!", "New Friend");
		QueueFree();
	}

	public struct FriendRequestNotifyPayload()
	{
		public int FromUserId = 0;
		public string FromName = "";
	}
}
