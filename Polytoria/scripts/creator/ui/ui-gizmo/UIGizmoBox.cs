// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;

namespace Polytoria.Creator.UI.Gizmos;

public partial class UIGizmoBox : Control
{
	[Export] private Label _sizeIndLabel = null!;
	public UIField Target = null!;

	public override void _EnterTree()
	{
		Target.TransformChanged.Connect(OnTransformChanged);
		OnTransformChanged();
		base._EnterTree();
	}

	public override void _ExitTree()
	{
		Target.TransformChanged.Disconnect(OnTransformChanged);
		base._ExitTree();
	}

	private void OnTransformChanged()
	{
		GlobalPosition = Target.NodeControl.GlobalPosition;
		Size = Target.NodeControl.Size;
		Scale = Target.NodeControl.Scale;
		Rotation = Target.NodeControl.Rotation;
		_sizeIndLabel.Text = $"{Target.AbsoluteSize.X}x{Target.AbsoluteSize.Y}";
	}
}
