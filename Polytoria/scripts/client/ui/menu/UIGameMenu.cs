// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Shared;
using System;
using System.Collections.Generic;

namespace Polytoria.Client.UI;

public partial class UIGameMenu : Control
{
	public Vector2 GameMenuSize = new(900, 560);
	private static readonly Color OverlayColor = new(0.02f, 0.06f, 0.09f, 0.64f);
	private static readonly Color PanelColor = new(0.035f, 0.065f, 0.10f, 0.98f);
	private static readonly Color SurfaceColor = new(0.055f, 0.095f, 0.14f, 1f);
	private static readonly Color SurfaceHoverColor = new(0.075f, 0.14f, 0.20f, 1f);
	private static readonly Color AccentColor = new(0.14f, 0.82f, 0.90f, 1f);
	private static readonly Color AccentDarkColor = new(0.03f, 0.30f, 0.40f, 1f);
	private static readonly Color LineColor = new(0.16f, 0.30f, 0.40f, 1f);
	private readonly Dictionary<GameMenuViewEnum, UIMenuViewBase> _loadedViews = [];
	private UIMenuViewBase? _currentView = null;

	[Export] private AnimationPlayer _animPlay = null!;
	[Export] private Control _viewContainer = null!;
	[Export] private Control _firstFocus = null!;
	[Export] private Control _gameMenuPanel = null!;

	public bool IsShowing = false;

	public CoreUIRoot CoreUI = null!;
	public event Action<bool>? IsShowingChanged;
	public event Action<GameMenuViewEnum>? ViewChanged;
	private readonly List<UIMenuTabButton> _tabButtons = [];

	public override void _Ready()
	{
		Visible = false;
		ApplyStaticSkin();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("toggle_menu"))
		{
			ToggleMenu();
		}
		base._UnhandledInput(@event);
	}

	public void RegisterTabButton(UIMenuTabButton tabbtn)
	{
		_tabButtons.Add(tabbtn);
	}

	public void ToggleMenu()
	{
		if (IsShowing)
		{
			HideMenu();
		}
		else
		{
			ShowMenu();
		}
	}

	public void ShowMenu()
	{
		if (IsShowing) { return; }
		IsShowing = true;
		_animPlay.Play("appear");
		Visible = true;
		_firstFocus.GrabFocus();
		IsShowingChanged?.Invoke(IsShowing);
		CoreUIRoot.Singleton.Root.Input.IsMenuOpened = true;
		SwitchView(GameMenuViewEnum.Overview);
		RefreshSize();
	}

	private void RefreshSize()
	{
		Rect2 rect = GetViewportRect();
		if (rect.Size.X < GameMenuSize.X)
		{
			_gameMenuPanel.Size = new(rect.Size.X, _gameMenuPanel.Size.Y);
		}
		else
		{
			_gameMenuPanel.Size = new(GameMenuSize.X, _gameMenuPanel.Size.Y);
		}
		if (rect.Size.Y < GameMenuSize.Y)
		{
			_gameMenuPanel.Size = new(_gameMenuPanel.Size.X, rect.Size.Y);
		}
		else
		{
			_gameMenuPanel.Size = new(_gameMenuPanel.Size.X, GameMenuSize.Y);
		}
		_gameMenuPanel.SetDeferred(Control.PropertyName.AnchorsPreset, (int)LayoutPreset.Center);
	}

	public void HideMenu()
	{
		if (!IsShowing) { return; }
		GetViewport().GuiReleaseFocus();
		IsShowing = false;
		_animPlay.Stop(true);
		_animPlay.Play("disappear");
		CoreUIRoot.Singleton.Root.Input.IsMenuOpened = false;
		IsShowingChanged?.Invoke(IsShowing);
		_currentView?.HideView();
	}

	public void SwitchView(GameMenuViewEnum switchTo)
	{
		// Hide the current view if it exists
		if (_currentView != null)
		{
			_currentView.Visible = false;
			_currentView.HideView();
		}

		// Check if the view is already loaded
		if (!_loadedViews.TryGetValue(switchTo, out UIMenuViewBase? view))
		{
			string pathToLoad = switchTo switch
			{
				GameMenuViewEnum.Overview => "res://scenes/client/ui/menu/views/overview.tscn",
				GameMenuViewEnum.Players => "res://scenes/client/ui/menu/views/players.tscn",
				GameMenuViewEnum.Report => "res://scenes/client/ui/menu/views/report.tscn",
				GameMenuViewEnum.Settings => "res://scenes/client/ui/menu/views/settings.tscn",
				_ => throw new ArgumentOutOfRangeException(nameof(switchTo), $"No scene defined for {switchTo}")
			};

			PackedScene scene = GD.Load<PackedScene>(pathToLoad);
			if (scene != null)
			{
				view = scene.Instantiate<UIMenuViewBase>();
				_viewContainer.AddChild(view);
				_loadedViews[switchTo] = view;
			}
			else
			{
				PT.PrintErr("Failed to load settings scene at: " + pathToLoad);
				return;
			}
		}

		ApplyViewSkin(view, switchTo);

		// Update first focus
		foreach (UIMenuTabButton tabBtn in _tabButtons)
		{
			if (view.FirstFocus != null)
			{
				tabBtn.FocusNeighborBottom = tabBtn.GetPathTo(view.FirstFocus);
			}
		}

		// Show the new view
		view.Menu = this;
		view.ShowView();
		view.Visible = true;
		ViewChanged?.Invoke(switchTo);
		_currentView = view;
	}

	private void ApplyStaticSkin()
	{
		AddThemeStyleboxOverride("panel", Box(OverlayColor));

		if (_gameMenuPanel is Panel panel)
		{
			panel.AddThemeStyleboxOverride("panel", Box(PanelColor, 18, LineColor, 1, shadowSize: 18));
		}

		if (GetNodeOrNull<HBoxContainer>("Pivot/Pivot2/Panel/Layout/Layout") is HBoxContainer tabBar)
		{
			tabBar.CustomMinimumSize = new Vector2(0, 76);
			tabBar.AddThemeConstantOverride("separation", 8);
			tabBar.AddThemeStyleboxOverride("panel", Box(new Color(0.025f, 0.045f, 0.07f, 1f), 18));
		}

		StyleTabButton(GetNodeOrNull<Button>("Pivot/Pivot2/Panel/Layout/Layout/Overview"), "Home");
		StyleTabButton(GetNodeOrNull<Button>("Pivot/Pivot2/Panel/Layout/Layout/Players"), "People");
		StyleTabButton(GetNodeOrNull<Button>("Pivot/Pivot2/Panel/Layout/Layout/Settings"), "Options");
	}

	private static void StyleTabButton(Button? button, string text)
	{
		if (button == null) return;

		button.CustomMinimumSize = new Vector2(0, 64);
		button.AddThemeStyleboxOverride("normal", Box(new Color(0.025f, 0.045f, 0.07f, 0.0f), 14));
		button.AddThemeStyleboxOverride("hover", Box(SurfaceHoverColor, 14, new Color(0.10f, 0.35f, 0.46f), 1));
		button.AddThemeStyleboxOverride("pressed", Box(AccentDarkColor, 14, AccentColor, 1));
		button.AddThemeStyleboxOverride("focus", Box(new Color(0, 0, 0, 0), 14, AccentColor, 2));
		button.AddThemeFontSizeOverride("font_size", 21);

		if (button.GetNodeOrNull<HBoxContainer>("Layout") is HBoxContainer layout)
		{
			layout.SetAnchorsPreset(LayoutPreset.FullRect);
			layout.OffsetLeft = 22;
			layout.OffsetTop = 0;
			layout.OffsetRight = -22;
			layout.OffsetBottom = 0;
			layout.Alignment = BoxContainer.AlignmentMode.Center;
			layout.AddThemeConstantOverride("separation", 10);
		}

		if (button.GetNodeOrNull<TextureRect>("Layout/TextureRect") is TextureRect icon)
		{
			icon.CustomMinimumSize = new Vector2(24, 0);
			icon.SelfModulate = new Color(0.80f, 0.95f, 1f, 1f);
		}

		if (button.GetNodeOrNull<Label>("Layout/Label") is Label label)
		{
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 21);
		}
	}

	private static void ApplyViewSkin(UIMenuViewBase view, GameMenuViewEnum viewType)
	{
		switch (viewType)
		{
			case GameMenuViewEnum.Overview:
				ApplyOverviewSkin(view);
				break;
			case GameMenuViewEnum.Players:
				ApplyPlayersSkin(view);
				break;
			case GameMenuViewEnum.Settings:
				ApplySettingsSkin(view);
				break;
		}
	}

	private static void ApplyOverviewSkin(UIMenuViewBase view)
	{
		if (view.GetNodeOrNull<TextureRect>("PlaceBackground") is TextureRect place)
		{
			place.CustomMinimumSize = new Vector2(0, 190);
			place.SelfModulate = new Color(0.62f, 0.78f, 0.86f, 0.92f);
		}

		if (view.GetNodeOrNull<Label>("PlaceBackground/PlaceInfo/PlaceType") is Label typeLabel)
		{
			typeLabel.Text = "KRYNDORA SESSION";
			typeLabel.Modulate = new Color(0.58f, 0.92f, 1f, 0.75f);
			typeLabel.AddThemeFontSizeOverride("font_size", 13);
		}

		if (view.GetNodeOrNull<Label>("PlaceBackground/PlaceInfo/PlaceName") is Label nameLabel)
		{
			nameLabel.AddThemeFontSizeOverride("font_size", 28);
		}

		if (view.GetNodeOrNull<PanelContainer>("PanelContainer") is PanelContainer statsCard)
		{
			statsCard.AddThemeStyleboxOverride("panel", Box(SurfaceColor, 12, LineColor, 1, 26));
		}

		if (view.GetNodeOrNull<Label>("PanelContainer/Layout/Layout/Label") is Label statsLabel)
		{
			statsLabel.Text = "Session Stats";
			statsLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.88f, 0.96f));
			statsLabel.AddThemeFontSizeOverride("font_size", 16);
		}

		if (view.GetNodeOrNull<Panel>("Panel") is Panel actionsPanel)
		{
			actionsPanel.AddThemeStyleboxOverride("panel", Box(new Color(0.025f, 0.045f, 0.07f, 1f), 0, LineColor, 1));
		}

		StyleActionButton(view.GetNodeOrNull<Button>("Panel/ActionMenu/Screenshot"), "Capture", AccentColor);
		StyleActionButton(view.GetNodeOrNull<Button>("Panel/ActionMenu/Respawn"), "Respawn", new Color(0.45f, 0.88f, 0.58f));
		StyleActionButton(view.GetNodeOrNull<Button>("Panel/ActionMenu/Leave"), "Exit", new Color(1.0f, 0.44f, 0.48f));

		if (view.GetNodeOrNull<Button>("PlaceBackground/ReportButton") is Button report)
		{
			report.SelfModulate = new Color(1f, 0.54f, 0.42f, 1f);
			report.AddThemeStyleboxOverride("normal", Box(new Color(0.03f, 0.08f, 0.10f, 0.68f), 18, new Color(1f, 0.54f, 0.42f), 2));
			report.AddThemeStyleboxOverride("hover", Box(new Color(0.16f, 0.08f, 0.08f, 0.82f), 18, new Color(1f, 0.54f, 0.42f), 2));
			report.AddThemeStyleboxOverride("pressed", Box(new Color(0.26f, 0.08f, 0.08f, 0.95f), 18, new Color(1f, 0.54f, 0.42f), 2));
		}
	}

	private static void StyleActionButton(Button? button, string text, Color accent)
	{
		if (button == null) return;

		button.AddThemeStyleboxOverride("normal", Box(new Color(0.025f, 0.045f, 0.07f, 0f), 10));
		button.AddThemeStyleboxOverride("hover", Box(new Color(accent.R * 0.14f, accent.G * 0.14f, accent.B * 0.14f, 0.55f), 10, accent, 1));
		button.AddThemeStyleboxOverride("pressed", Box(new Color(accent.R * 0.20f, accent.G * 0.20f, accent.B * 0.20f, 0.75f), 10, accent, 1));

		if (button.GetNodeOrNull<ColorRect>("Rect") is ColorRect line)
		{
			line.CustomMinimumSize = new Vector2(0, 3);
			line.Color = accent;
		}

		if (button.GetNodeOrNull<TextureRect>("Rect2") is TextureRect glow)
		{
			glow.SelfModulate = accent;
			glow.Modulate = new Color(1f, 1f, 1f, 0.12f);
		}

		if (button.GetNodeOrNull<HBoxContainer>("Layout") is HBoxContainer layout)
		{
			layout.Alignment = BoxContainer.AlignmentMode.Center;
			layout.AddThemeConstantOverride("separation", 10);
		}

		if (button.GetNodeOrNull<TextureRect>("Layout/TextureRect") is TextureRect icon)
		{
			icon.CustomMinimumSize = new Vector2(24, 0);
			icon.SelfModulate = new Color(0.88f, 0.96f, 1f, 1f);
		}

		if (button.GetNodeOrNull<Label>("Layout/Label") is Label label)
		{
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 22);
		}
	}

	private static void ApplyPlayersSkin(UIMenuViewBase view)
	{
		view.AddThemeStyleboxOverride("panel", Box(SurfaceColor, 12, LineColor, 1, 22));

		if (view.GetNodeOrNull<GridContainer>("Scroll/Grid") is GridContainer grid)
		{
			grid.AddThemeConstantOverride("h_separation", 14);
			grid.AddThemeConstantOverride("v_separation", 14);
		}
	}

	private static void ApplySettingsSkin(UIMenuViewBase view)
	{
		if (view.GetNodeOrNull<PanelContainer>("Categories") is PanelContainer categories)
		{
			categories.CustomMinimumSize = new Vector2(232, 0);
			categories.AddThemeStyleboxOverride("panel", Box(SurfaceColor, 12, LineColor, 1, 18));
		}

		if (view.GetNodeOrNull<ScrollContainer>("ScrollContainer") is ScrollContainer scroll)
		{
			scroll.AddThemeStyleboxOverride("panel", Box(new Color(0.025f, 0.045f, 0.07f, 0f)));
		}
	}

	private static StyleBoxFlat Box(Color bg, int radius = 0, Color? border = null, int borderWidth = 0, float margin = 0, int shadowSize = 0)
	{
		StyleBoxFlat box = new()
		{
			BgColor = bg,
			BorderColor = border ?? new Color(0, 0, 0, 0),
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius,
			ShadowColor = new Color(0, 0, 0, 0.28f),
			ShadowSize = shadowSize,
			ShadowOffset = new Vector2(0, 8)
		};

		if (margin > 0)
		{
			box.SetContentMargin(Side.Left, margin);
			box.SetContentMargin(Side.Top, margin);
			box.SetContentMargin(Side.Right, margin);
			box.SetContentMargin(Side.Bottom, margin);
		}

		return box;
	}

	public enum GameMenuViewEnum
	{
		Overview,
		Players,
		Report,
		Settings
	}
}
