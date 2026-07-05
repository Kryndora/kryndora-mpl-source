// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;
using System.Collections.Generic;

namespace Polytoria.Creator.UI.Gizmos;

public partial class UIGizmoBox : Control
{
	[Export] private Label _sizeIndLabel = null!;
	public UIField Target = null!;

	private bool _dragging;
	private bool _resizing;
	private int _resizeCorner;
	private Panel[] _handles = null!;
	private Vector2 _dragRaw;
	private Vector2 _curScreen;
	private Vector2 _resizeRaw;
	private Vector2 _resizeCur;
	private const float SnapThreshold = 8f;

	public override void _EnterTree()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_handles = [GetNode<Panel>("L1"), GetNode<Panel>("L2"), GetNode<Panel>("L3"), GetNode<Panel>("L4")];
		foreach (Panel h in _handles)
		{
			h.Visible = true;
			h.MouseFilter = MouseFilterEnum.Ignore;
			h.AddThemeStyleboxOverride("panel", HandleStyle());
		}
		Target.TransformChanged.Connect(OnTransformChanged);
		OnTransformChanged();
		base._EnterTree();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left)
		{
			if (btn.Pressed)
			{
				for (int i = 0; i < _handles.Length; i++)
				{
					if (_handles[i].GetGlobalRect().HasPoint(btn.Position))
					{
						_resizing = true;
						_resizeCorner = i;
						_resizeRaw = CornerScreenPos(i);
						_resizeCur = _resizeRaw;
						GetViewport().SetInputAsHandled();
						return;
					}
				}
				if (GetGlobalRect().HasPoint(btn.Position))
				{
					_dragging = true;
					_dragRaw = Target.NodeControl.GlobalPosition;
					_curScreen = _dragRaw;
					GetViewport().SetInputAsHandled();
				}
			}
			else
			{
				if (_dragging || _resizing)
					GetViewport().SetInputAsHandled();
				_dragging = false;
				_resizing = false;
			}
		}
		else if (@event is InputEventMouseMotion motion)
		{
			if (_resizing)
			{
				_resizeRaw += motion.Relative;
				Vector2 snappedCorner = SnapCorner(_resizeRaw);
				if (snappedCorner != _resizeRaw)
					snappedCorner = snappedCorner.Round();
				ApplyResize(snappedCorner - _resizeCur);
				_resizeCur = snappedCorner;
				GetViewport().SetInputAsHandled();
			}
			else if (_dragging)
			{
				_dragRaw += motion.Relative;
				Vector2 snapped = SnapPos(_dragRaw, Target.NodeControl.Size);
				if (snapped != _dragRaw)
					snapped = snapped.Round();
				Target.PositionOffset += snapped - _curScreen;
				_curScreen = snapped;
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void GatherGuides(List<float> xs, List<float> ys)
	{
		if (Target.NodeControl.GetParent() is Control parentCtrl)
		{
			Rect2 pr = parentCtrl.GetGlobalRect();
			xs.Add(pr.Position.X); xs.Add(pr.GetCenter().X); xs.Add(pr.End.X);
			ys.Add(pr.Position.Y); ys.Add(pr.GetCenter().Y); ys.Add(pr.End.Y);
		}

		if (Target.Parent != null)
			foreach (Instance child in Target.Parent.GetChildren())
			{
				if (child is UIField sib && sib != Target && GodotObject.IsInstanceValid(sib.NodeControl))
				{
					Rect2 sr = sib.NodeControl.GetGlobalRect();
					xs.Add(sr.Position.X); xs.Add(sr.GetCenter().X); xs.Add(sr.End.X);
					ys.Add(sr.Position.Y); ys.Add(sr.GetCenter().Y); ys.Add(sr.End.Y);
				}
			}
	}

	private Vector2 SnapPos(Vector2 pos, Vector2 size)
	{
		if (Input.IsKeyPressed(Key.Alt)) return pos;
		List<float> xs = [];
		List<float> ys = [];
		GatherGuides(xs, ys);
		float[] elXs = [pos.X, pos.X + size.X * 0.5f, pos.X + size.X];
		float[] elYs = [pos.Y, pos.Y + size.Y * 0.5f, pos.Y + size.Y];
		return pos + new Vector2(NearestSnap(elXs, xs), NearestSnap(elYs, ys));
	}

	private Vector2 SnapCorner(Vector2 corner)
	{
		if (Input.IsKeyPressed(Key.Alt)) return corner;
		List<float> xs = [];
		List<float> ys = [];
		GatherGuides(xs, ys);
		return corner + new Vector2(NearestSnap([corner.X], xs), NearestSnap([corner.Y], ys));
	}

	private Vector2 CornerScreenPos(int corner)
	{
		Vector2 p = Target.NodeControl.GlobalPosition;
		Vector2 s = Target.NodeControl.Size;
		return corner switch
		{
			1 => p + new Vector2(s.X, 0f),
			2 => p + new Vector2(0f, s.Y),
			3 => p + s,
			_ => p
		};
	}

	private static float NearestSnap(float[] edges, List<float> guides)
	{
		float best = 0f;
		float bestDist = SnapThreshold;
		foreach (float e in edges)
			foreach (float g in guides)
			{
				float d = g - e;
				if (Mathf.Abs(d) < bestDist) { bestDist = Mathf.Abs(d); best = d; }
			}
		return best;
	}

	private void ApplyResize(Vector2 d)
	{
		bool right = _resizeCorner is 1 or 3;
		bool bottom = _resizeCorner is 2 or 3;
		Vector2 pivot = Target.PivotPoint;
		Vector2 sizeD;
		Vector2 posD;

		if (right) { sizeD.X = d.X; posD.X = pivot.X * d.X; }
		else { sizeD.X = -d.X; posD.X = (1f - pivot.X) * d.X; }

		if (bottom) { sizeD.Y = d.Y; posD.Y = pivot.Y * d.Y; }
		else { sizeD.Y = -d.Y; posD.Y = (1f - pivot.Y) * d.Y; }

		Vector2 newSize = Target.SizeOffset + sizeD;
		newSize.X = Mathf.Max(newSize.X, 4f);
		newSize.Y = Mathf.Max(newSize.Y, 4f);
		Target.SizeOffset = newSize;
		Target.PositionOffset += posD;
	}

	private static StyleBoxFlat HandleStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(1f, 0.7372549f, 0.34509805f),
			BorderColor = new Color(1f, 1f, 1f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerRadiusBottomRight = 2
		};
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
