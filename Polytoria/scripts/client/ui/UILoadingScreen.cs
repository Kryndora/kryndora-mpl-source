// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel.Resources;
using Polytoria.Schemas.API;
using Polytoria.Shared.AssetLoaders;

namespace Polytoria.Client.UI;

public partial class UILoadingScreen : Control
{
	[Export] private Label _statusLabel = null!;
	[Export] private ProgressBar _statusProgressbar = null!;
	[Export] public Control? Loader { get; private set; }
	[Export] private TextureRect _gameThumbnailRect = null!;
	[Export] private TextureRect _gameIconRect = null!;
	[Export] private Label _gameTitleLabel = null!;
	[Export] private Label _gameCreatorLabel = null!;
	[Export] private Control _gameDetailsContainer = null!;
	[Export] private AnimationPlayer _animPlay = null!;
	[Export] private AnimationPlayer _bgAnimPlay = null!;

	private KDImageAsset _gameThumbnailImage = null!;
	private KDImageAsset _gameIconImage = null!;

	private bool _infoReady = false;
	private bool _iconReady = false;
	private bool _iconAppeared = false;
	private bool _bgAppeared = false;
	private bool _thumbnailRequested = false;

	private ClientEntry _entry = null!;

	public override void _Ready()
	{
		if (GetNodeOrNull("../../") is not ClientEntry)
		{
			Visible = false;
			return;
		}
		_gameThumbnailImage = new();
		_gameIconImage = new();

		_entry = GetNode<ClientEntry>("../../");
		if (_entry.IsNetEssentialsReady)
		{
			NetworkEssentialsReady();
		}
		else
		{
			_entry.NetworkEssentialsReady += NetworkEssentialsReady;
		}

		_gameDetailsContainer.Visible = false;

		_gameThumbnailImage.ResourceLoaded += OnGameThumbnailLoaded;
		_gameIconImage.ResourceLoaded += OnGameIconLoaded;

		SetStatusText("Waiting for server...");
		Visible = true;
	}

	private void OnGameIconLoaded(Resource resource)
	{
		_gameIconRect.Texture = (Texture2D)resource;
		_iconReady = true;
	}

	private void OnGameThumbnailLoaded(Resource resource)
	{
		if (_bgAppeared) return;
		_bgAppeared = true;

		Texture2D texture = (Texture2D)resource;
		_gameThumbnailRect.Texture = texture;
		if (!_iconReady)
		{
			_gameIconRect.Texture = texture;
			_iconReady = true;
		}
		_bgAnimPlay.Play("fade_in");
	}

	private void SetStatusText(string text)
	{
		_statusLabel.Text = text;
	}

	private void NetworkEssentialsReady()
	{
		_entry.NetworkService.ClientReady += OnClientReady;
		_entry.NetworkService.ClientWorldReady += OnWorldReady;
		_entry.NetworkService.ReplicateSync.InstanceLoadedProgress += InstanceLoadedProgress;
		_entry.NetworkService.ClientConnectedToServer += OnClientConnectedToServer;
		_entry.TargetServerReady += OnServerReady;
		_entry.NetworkEssentialsReady -= NetworkEssentialsReady;

		// Hide loading screen if is server
		if (_entry.NetworkService.IsServer)
		{
			Visible = false;
		}
		SetStatusText("Waiting for server...");

		if (_entry.Root != null)
		{
			if (_entry.Root.WorldInfo.HasValue)
			{
				OnWorldInfoReady(_entry.Root.WorldInfo.Value);
			}
			else
			{
				_entry.Root.WorldInfoReady += OnWorldInfoReady;
			}

			if (_entry.Root.WorldMedia != null)
			{
				OnWorldMediaReady(_entry.Root.WorldMedia);
			}
		}
	}

	private void OnWorldInfoReady(APIPlaceInfo info)
	{
		_gameTitleLabel.Text = info.Name;
		_gameCreatorLabel.Text = "By " + info.Creator.Name;
		LoadGameThumbnail(info);
		AppearInfo();
	}

	private void AppearInfo()
	{
		if (_iconAppeared) return;
		_iconAppeared = true;
		_animPlay.Play("info_appear");
		_gameDetailsContainer.Visible = true;
	}

	private void OnWorldMediaReady(APIPlaceMedia[] _)
	{
		if (_thumbnailRequested || _entry.Root.WorldID == 0) return;
		LoadGameThumbnail((uint)_entry.Root.WorldID);
	}

	private void LoadGameThumbnail(uint gameId)
	{
		if (_thumbnailRequested || gameId == 0) return;
		_thumbnailRequested = true;
		_gameThumbnailImage.ImageType = ImageTypeEnum.WorldThumbnail;
		_gameThumbnailImage.ImageID = gameId;
		_gameThumbnailImage.LoadResource();
	}

	private void LoadGameThumbnail(APIPlaceInfo info)
	{
		if (_thumbnailRequested) return;

		if (!string.IsNullOrWhiteSpace(info.Thumbnail))
		{
			_thumbnailRequested = true;
			WebAssetLoader.Singleton.GetResource(new()
			{
				Type = WebResourceType.Image,
				URL = info.Thumbnail
			}, OnGameThumbnailLoaded);
			return;
		}

		LoadGameThumbnail((uint)info.Id);
	}

	private void InstanceLoadedProgress(int current, int max)
	{
		Loader?.QueueFree();
		Loader = null;
		_statusProgressbar.Value = current;
		_statusProgressbar.MaxValue = max;
		SetStatusText($"Constructing ({current}/{max})...");
	}

	private void OnWorldReady()
	{
		SetStatusText("Waiting for player");
	}

	private void OnServerReady()
	{
		SetStatusText("Connecting...");
	}

	private void OnClientConnectedToServer()
	{
		SetStatusText("Downloading world...");
	}

	private void OnClientReady()
	{
		SetStatusText("Ready!");
		_animPlay.Play("load_ready");

		_gameThumbnailImage.ResourceLoaded -= OnGameThumbnailLoaded;
		_gameIconImage.ResourceLoaded -= OnGameIconLoaded;
	}
}
