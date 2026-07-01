// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;

namespace Polytoria.Client.UI.Touch;

public partial class JoystickArea : InputFallbackBase
{
	private bool _dragging = false;
	private Vector2 _startPos;
	private Vector2 _endPos;
	private Line2D _line = null!;

	public override void _Ready()
	{
		_line = GetNode<Line2D>("Line");
	}

	public override void _Process(double delta)
	{
		if (!_dragging) { return; }

		_line.ClearPoints();
		_line.AddPoint(_startPos);
		_line.AddPoint(_endPos);

		Vector2 normalized = (_startPos - _endPos).Normalized();

		InputEventJoypadMotion leftX = new()
		{
			Axis = JoyAxis.LeftX,
			AxisValue = -normalized.X
		};

		InputEventJoypadMotion leftY = new()
		{
			Axis = JoyAxis.LeftY,
			AxisValue = -normalized.Y
		};
		leftX.SetMeta("emulated", 1);
		leftY.SetMeta("emulated", 1);

		Input.ParseInputEvent(leftX);
		Input.ParseInputEvent(leftY);
	}

	private static void SendInputEnd()
	{
		InputEventJoypadMotion leftX = new()
		{
			Axis = JoyAxis.LeftX,
			AxisValue = 0
		};

		InputEventJoypadMotion leftY = new()
		{
			Axis = JoyAxis.LeftY,
			AxisValue = 0
		};

		Input.ParseInputEvent(leftX);
		Input.ParseInputEvent(leftY);
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventScreenTouch eventTouch)
		{
			if (eventTouch.Pressed)
			{
				_startPos = eventTouch.Position;
				_endPos = _startPos;
				_dragging = true;
				_line.Visible = true;
				AcceptEvent();
			}
			else
			{
				_line.Visible = false;
				_dragging = false;
				SendInputEnd();
			}
		}
		if (@event is InputEventScreenDrag eventDrag && _dragging)
		{
			_endPos = eventDrag.Position;
			AcceptEvent();
		}
		base._GuiInput(@event);
	}
}
