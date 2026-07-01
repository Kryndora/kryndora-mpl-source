// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Mobile.Utils;

namespace Polytoria.Mobile.UI;

public partial class NewUserSplash : Control
{
	[Export] private Button _registerBtn = null!;
	[Export] private Button _loginBtn = null!;
	[Export] private Button _closeBtn = null!;

	private Control? _authForm;
	private LineEdit _usernameField = null!;
	private LineEdit _passwordField = null!;
	private Label _errorLabel = null!;
	private Button _submitButton = null!;
	private bool _signupMode;
	private bool _busy;

	public override void _Ready()
	{
		_registerBtn.Pressed += OnRegisterPressed;
		_loginBtn.Pressed += OnLoginPressed;
		_closeBtn.Pressed += OnClosePressed;
	}

	private void OnClosePressed()
	{
		if (_authForm != null && _authForm.Visible)
		{
			_authForm.Visible = false;
			return;
		}

		Visible = false;
		MobileUI.Singleton.SwitchTo(MobileViewEnum.Home);
	}

	public void ShowSplash()
	{
		GetNode<AnimationPlayer>("AnimPlay").Play("appear");
	}

	private void OnRegisterPressed()
	{
		ShowAuthForm(signup: true);
	}

	private void OnLoginPressed()
	{
		ShowAuthForm(signup: false);
	}

	private void ShowAuthForm(bool signup)
	{
		_signupMode = signup;
		EnsureAuthForm();
		_errorLabel.Text = "";
		_usernameField.Text = "";
		_passwordField.Text = "";
		_submitButton.Text = signup ? "Register" : "Login";
		_authForm!.Visible = true;
		_usernameField.GrabFocus();
	}

	private void EnsureAuthForm()
	{
		if (_authForm != null)
		{
			return;
		}

		Panel backdrop = new();
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		backdrop.AddThemeStyleboxOverride("panel", new StyleBoxFlat() { BgColor = new Color(0.035f, 0.055f, 0.08f, 0.97f) });

		CenterContainer center = new();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		backdrop.AddChild(center);

		VBoxContainer content = new() { CustomMinimumSize = new Vector2(360, 0) };
		content.AddThemeConstantOverride("separation", 16);
		center.AddChild(content);

		Label title = new() { Text = "Kryndora", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 40);
		content.AddChild(title);

		_usernameField = new LineEdit() { PlaceholderText = "Username", CustomMinimumSize = new Vector2(0, 56) };
		content.AddChild(_usernameField);

		_passwordField = new LineEdit() { PlaceholderText = "Password", Secret = true, CustomMinimumSize = new Vector2(0, 56) };
		content.AddChild(_passwordField);

		_errorLabel = new Label()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Modulate = new Color(1f, 0.45f, 0.45f)
		};
		content.AddChild(_errorLabel);

		_submitButton = new Button() { Text = "Login", CustomMinimumSize = new Vector2(0, 60) };
		_submitButton.Pressed += OnSubmitPressed;
		content.AddChild(_submitButton);

		Button backButton = new()
		{
			Text = "Back",
			CustomMinimumSize = new Vector2(0, 48),
			Modulate = new Color(1f, 1f, 1f, 0.6f)
		};
		backButton.Pressed += () => _authForm!.Visible = false;
		content.AddChild(backButton);

		_passwordField.TextSubmitted += _ => OnSubmitPressed();

		_authForm = backdrop;
		AddChild(backdrop);
	}

	private async void OnSubmitPressed()
	{
		if (_busy)
		{
			return;
		}

		string username = _usernameField.Text.Trim();
		string password = _passwordField.Text;
		if (username.Length == 0 || password.Length == 0)
		{
			_errorLabel.Text = "Bitte Username und Passwort eingeben.";
			return;
		}

		_busy = true;
		_submitButton.Disabled = true;
		_errorLabel.Text = "";

		string? error = await PolyMobileAuthAPI.LoginWithCredentials(username, password, _signupMode);

		_busy = false;
		_submitButton.Disabled = false;

		if (error == null)
		{
			_authForm!.Visible = false;
		}
		else
		{
			_errorLabel.Text = error;
		}
	}
}
