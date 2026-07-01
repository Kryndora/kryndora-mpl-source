// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Client.Settings;

namespace Polytoria.Client.UI;

public partial class MinimalPerformanceOverlay : Control
{
	private Label _fpsLabel = null!;
	private Label _pingLabel = null!;


	public override void _Ready()
	{
		_fpsLabel = GetNode<Label>("FPS/Layout/Label");
		_pingLabel = GetNode<Label>("Ping/Layout/Label");
		Visible = false;
		MainUpdateLoop();
	}

	private void UpdateVisible()
	{
		bool showSimpleStats = ClientSettingsService.Instance.Get<bool>(ClientSettingKeys.Overlay.ShowFpsAndPing);
		bool showLegacyOverlay = ClientSettingsService.Instance.Get<OverlayMode>(ClientSettingKeys.Overlay.PerformanceOverlayMode) >= OverlayMode.Minimal;
		Visible = showSimpleStats || showLegacyOverlay;
	}

	private void UpdateAll()
	{
		_fpsLabel.Text = Engine.GetFramesPerSecond().ToString();
		_pingLabel.Text = (CoreUIRoot.Singleton.Root.Players.LocalPlayer?.NetworkPing ?? 0).ToString();
	}

	private async void MainUpdateLoop()
	{
		UpdateAll();
		UpdateVisible();
		await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
		MainUpdateLoop();
	}
}
