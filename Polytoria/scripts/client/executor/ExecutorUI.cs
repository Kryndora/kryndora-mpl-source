// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;
using Polytoria.Shared;
using Polytoria.Shared.Misc;

namespace Polytoria.Client.Executor;

public partial class ExecutorUI : Window
{
	[Export] private CodeEdit _codeField = null!;
	[Export] private Button _runCompatBtn = null!;
	[Export] private Button _runBtn = null!;
	[Export] private Button _clearBtn = null!;
	private InputHelper? _inputHelper;

	public override void _EnterTree()
	{
		CloseRequested += OnCloseRequested;
		_runBtn.Pressed += OnRun;
		_runCompatBtn.Pressed += OnRunCompat;
		_clearBtn.Pressed += OnClear;
		_inputHelper = new InputHelper();
		_inputHelper.GodotUnhandledInputEvent += UnhandledKeyInput;
		Globals.Singleton.AddChild(_inputHelper);
		base._EnterTree();
	}

	public override void _ExitTree()
	{
		CloseRequested -= OnCloseRequested;
		_runBtn.Pressed -= OnRun;
		_runCompatBtn.Pressed -= OnRunCompat;
		_clearBtn.Pressed -= OnClear;
		if (_inputHelper != null)
		{
			_inputHelper.GodotUnhandledInputEvent -= UnhandledKeyInput;
			_inputHelper.QueueFree();
			_inputHelper = null;
		}
		base._ExitTree();
	}

	public void UnhandledKeyInput(InputEvent @event)
	{
		if (@event is InputEventKey k && k.Keycode == Key.F9 && k.IsReleased())
		{
			if (Kryndora.Client.KryndoraAdminExecutor.CanOpenExecutor())
			{
				if (Visible)
					Hide();
				else
					PopupCentered(Size);

				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void OnCloseRequested()
	{
		Hide();
		GetViewport().SetInputAsHandled();
	}

	private void OnRun()
	{
		Run(false);
	}

	private void OnRunCompat()
	{
		Run(true);
	}

	private void Run(bool compat)
	{
		Kryndora.Client.KryndoraAdminExecutor.RunScript(_codeField.Text, compat);
	}

	private void OnClear()
	{
		_codeField.Text = "";
	}
}
