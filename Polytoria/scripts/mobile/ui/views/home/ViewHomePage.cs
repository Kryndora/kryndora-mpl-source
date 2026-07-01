// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Polytoria.Mobile.Utils;
using Polytoria.Schemas.API;
using Godot;
using Polytoria.Shared;
using Polytoria.Utils;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Polytoria.Mobile.UI;

public partial class ViewHomePage : MobileViewBase
{
	private const string FriendCardPath = "res://scenes/mobile/components/home/user_headshot_card.tscn";
	private static readonly PTHttpClient _client = new();
	private Label _usernameLabel = null!;
	private Label _friendsTitleLabel = null!;
	private HBoxContainer _friendsContainer = null!;
	private PackedScene _friendCardPacked = null!;

	public override void _Ready()
	{
		_usernameLabel = GetNode<Label>("ScrollContainer/VBoxContainer/Control/Layout/Username");
		_friendsTitleLabel = GetNode<Label>("ScrollContainer/VBoxContainer/PanelContainer/Layout/Friends/VBoxContainer/Label2");
		_friendsContainer = GetNode<HBoxContainer>("ScrollContainer/VBoxContainer/PanelContainer/Layout/Friends/ScrollContainer/HBoxContainer2");
		_friendCardPacked = GD.Load<PackedScene>(FriendCardPath);
		if (!string.IsNullOrWhiteSpace(PolyMobileAuthAPI.CurrentUserInfo.Username))
		{
			_usernameLabel.Text = PolyMobileAuthAPI.CurrentUserInfo.Username;
		}
		LoadFriends();
	}

	public override void _EnterTree()
	{
		PolyMobileAuthAPI.UserAuthenticated += OnUserAuthenticated;
		base._EnterTree();
	}

	public override void _ExitTree()
	{
		PolyMobileAuthAPI.UserAuthenticated -= OnUserAuthenticated;
		base._ExitTree();
	}

	private void OnUserAuthenticated(APIMeResponse response)
	{
		if (_usernameLabel != null && IsInstanceValid(_usernameLabel))
		{
			_usernameLabel.Text = response.Username;
		}
		LoadFriends();
	}

	private async void LoadFriends()
	{
		if (_friendsContainer == null || !IsInstanceValid(_friendsContainer))
		{
			return;
		}

		foreach (Node child in _friendsContainer.GetChildren())
		{
			child.QueueFree();
		}

		try
		{
			using HttpRequestMessage request = new(HttpMethod.Get, BuildUrl(Globals.MainEndpoint, "api/local/mobile-friends"));
			request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", PolyMobileAuthAPI.CurrentToken);
			using HttpResponseMessage httpResponse = await _client.SendAsync(request);
			httpResponse.EnsureSuccessStatusCode();
			LocalMobileFriendsResponse response = await httpResponse.Content.ReadFromJsonAsync(LocalMobileFriendsGenerationContext.Default.LocalMobileFriendsResponse);

			_friendsTitleLabel.Text = "Friends (" + response.Count + ")";
			if (response.Friends.Length == 0)
			{
				Label empty = new()
				{
					Text = "No friends yet",
					CustomMinimumSize = new(150, 90),
					VerticalAlignment = VerticalAlignment.Center,
					HorizontalAlignment = HorizontalAlignment.Center,
					Modulate = new(0.75f, 0.82f, 0.9f, 1)
				};
				_friendsContainer.AddChild(empty);
				return;
			}

			Array.Sort(response.Friends, (a, b) =>
			{
				int ra = (a.IsOnline ? 0 : 10) + (a.GameId.HasValue ? 0 : 1);
				int rb = (b.IsOnline ? 0 : 10) + (b.GameId.HasValue ? 0 : 1);
				return ra.CompareTo(rb);
			});

			foreach (LocalMobileFriend friend in response.Friends)
			{
				UserHeadshotCard card = _friendCardPacked.Instantiate<UserHeadshotCard>();
				card.UserID = (uint)friend.Id;
				card.OverrideUsername = friend.Username;
				card.IsOnline = friend.IsOnline;
				card.GameId = friend.GameId;
				card.GameName = friend.GameName;
				card.StatusText = friend.IsOnline && !string.IsNullOrWhiteSpace(friend.GameName)
					? "Playing " + friend.GameName
					: "Offline";
				_friendsContainer.AddChild(card);
			}
		}
		catch (Exception ex)
		{
			PT.PrintErr(ex);
			_friendsTitleLabel.Text = "Friends";
		}
	}

	private static string BuildUrl(string endpoint, string path)
	{
		return endpoint.TrimEnd('/') + "/" + path.TrimStart('/');
	}
}

internal struct LocalMobileFriendsResponse
{
	[JsonPropertyName("count")]
	public int Count { get; set; }

	[JsonPropertyName("friends")]
	public LocalMobileFriend[] Friends { get; set; }
}

internal struct LocalMobileFriend
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("username")]
	public string Username { get; set; }

	[JsonPropertyName("avatarId")]
	public string AvatarId { get; set; }

	[JsonPropertyName("isOnline")]
	public bool IsOnline { get; set; }

	[JsonPropertyName("gameId")]
	public int? GameId { get; set; }

	[JsonPropertyName("gameName")]
	public string GameName { get; set; }
}

[JsonSerializable(typeof(LocalMobileFriendsResponse))]
internal partial class LocalMobileFriendsGenerationContext : JsonSerializerContext { }
