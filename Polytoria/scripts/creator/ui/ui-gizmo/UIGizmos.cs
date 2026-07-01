// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;
using Polytoria.Shared;
using System.Collections.Generic;

namespace Polytoria.Creator.UI.Gizmos;

public partial class UIGizmos : CanvasLayer
{
	private const string GizmoBoxPath = "res://scenes/client/ui/ui-gizmo/gizmo_box.tscn";
	private readonly Dictionary<UIField, UIGizmoBox> _fieldToBox = [];

	public UIGizmoBox AddBox(UIField ui)
	{
		if (_fieldToBox.TryGetValue(ui, out UIGizmoBox? existing)) return existing;
		UIGizmoBox box = Globals.CreateInstanceFromScene<UIGizmoBox>(GizmoBoxPath);
		_fieldToBox[ui] = box;
		box.Target = ui;
		AddChild(box);
		return box;
	}

	public void RemoveBox(UIField ui)
	{
		if (_fieldToBox.TryGetValue(ui, out UIGizmoBox? existing))
		{
			existing.QueueFree();
			_fieldToBox.Remove(ui);
		}
	}
}
