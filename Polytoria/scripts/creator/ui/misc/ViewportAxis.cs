// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;

namespace Polytoria.Creator.UI;

public partial class ViewportAxis : Node
{
	[Export] public WorldContainerOverlay Overlay = null!;
	[Export] private Node3D _pivot = null!;
	[Export] private Node _container = null!;

	public override void _Process(double delta)
	{
		Camera3D cam = Overlay.World.CreatorContext.Freelook.Camera3D;
		_pivot.GlobalRotation = cam.GlobalRotation;
	}
}
