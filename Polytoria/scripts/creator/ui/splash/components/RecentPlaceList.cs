// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Creator.Managers;
using Polytoria.Shared;

namespace Polytoria.Creator.UI.Splashes.Components;

public partial class RecentPlaceList : Control
{
	private const string RecentPlaceCardPath = "res://scenes/creator/splash/components/recent_place_card.tscn";
	[Export] private Control _loader = null!;
	[Export] private Control _noProjectsView = null!;
	private int _loadGeneration;

	public override void _Ready()
	{
		LoadList();
	}

	public void Reload()
	{
		LoadList();
	}

	public void Clear()
	{
		foreach (Node item in GetChildren())
		{
			item.QueueFree();
		}
	}

	public async void LoadList()
	{
		int generation = ++_loadGeneration;
		_loader.Visible = true;
		Clear();
		ProjectManager.RecentData[] recents = await ProjectManager.GetRecents();
		if (generation != _loadGeneration)
		{
			return;
		}

		Clear();
		foreach (ProjectManager.RecentData r in recents)
		{
			RecentPlaceCard card = Globals.CreateInstanceFromScene<RecentPlaceCard>(RecentPlaceCardPath);
			card.Data = r;
			card.ListUI = this;
			AddChild(card);
		}
		_noProjectsView.Visible = recents.Length == 0;
		_loader.Visible = false;
	}
}
