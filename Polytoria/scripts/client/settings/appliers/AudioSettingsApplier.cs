using Godot;
using Polytoria.Shared.Settings;

namespace Polytoria.Client.Settings.Appliers;

public sealed partial class AudioSettingsApplier : Node
{
	public override void _Ready()
	{
		ClientSettingsService.Instance.Changed += OnChanged;
		ApplyVolume();
	}

	public override void _ExitTree()
	{
		ClientSettingsService.Instance?.Changed -= OnChanged;
		base._ExitTree();
	}

	private void OnChanged(SettingChangedEvent change)
	{
		if (change.Key == ClientSettingKeys.General.MasterVolume)
			ApplyVolume();
	}

	private static void ApplyVolume()
	{
		float volume = ClientSettingsService.Instance.Get<float>(ClientSettingKeys.General.MasterVolume);
		AudioServer.SetBusVolumeDb(0, Mathf.LinearToDb(volume / 100f));
	}
}
