// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Polytoria.Attributes;
using Polytoria.Scripting;

namespace Polytoria.Datamodel.Services;

// TODO: Fix this service, uses old ClientSettings
[Static("Preferences")]
[ExplorerExclude]
[SaveIgnore]
public sealed partial class PreferencesService : Instance
{
	[ScriptProperty] public PTSignal<string, object> SettingChanged { get; private set; } = new();
	[ScriptProperty] public static bool UsePhotoMode => false;//ClientSettings.Singleton.Settings.PhotoMode;
	[ScriptProperty] public static bool UsePostProcessing => false;//ClientSettings.Singleton.Settings.PostProcessing;


	public override void Init()
	{
		// ClientSettings.Singleton.OnSettingChanged += OnSettingChanged;
		base.Init();
	}

	public override void PreDelete()
	{
		// ClientSettings.Singleton.OnSettingChanged -= OnSettingChanged;
		base.PreDelete();
	}
}
