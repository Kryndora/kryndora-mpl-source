// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;
using Polytoria.Mobile.Utils;
using Polytoria.Shared;
using Polytoria.Utils;

namespace Polytoria.Mobile.UI;

public partial class ViewAvatarPage : MobileViewBase
{
	private const float FrontFacingDegrees = 180f;

	private PolytorianModel? _polytorian;
	private Node3D? _modelNode;
	private bool _editorBuilt;
	private float _modelYaw = FrontFacingDegrees;

	public override void _Ready()
	{
		SetupAvatarPreview();
		BuildEditor();
	}

	private void SetupAvatarPreview()
	{
		SubViewportContainer container = GetNode<SubViewportContainer>("VBoxContainer/Control/AspectRatioContainer/SubViewportContainer");
		SubViewport viewport = container.GetNode<SubViewport>("PlayerModel");
		_polytorian = Globals.LoadInstance<PolytorianModel>();
		_modelNode = (Node3D)_polytorian.GDNode;
		_modelNode.Position = new(0, -1.35f, 0);
		_modelNode.RotationDegrees = new(0, _modelYaw, 0);
		viewport.AddChild(_modelNode);
		_polytorian.InitEntry();

		container.GuiInput += OnPreviewGuiInput;

		_polytorian.LoadAppearance(CurrentUserId(), loadTool: false);
		_polytorian.PlayIdle();
	}

	private void OnPreviewGuiInput(InputEvent @event)
	{
		if (_modelNode == null)
		{
			return;
		}

		float dragX = @event switch
		{
			InputEventScreenDrag drag => drag.Relative.X,
			InputEventMouseMotion motion when (motion.ButtonMask & MouseButtonMask.Left) != 0 => motion.Relative.X,
			_ => 0f
		};

		if (dragX == 0f)
		{
			return;
		}

		_modelYaw -= dragX * 0.4f;
		_modelNode.RotationDegrees = new(0, _modelYaw, 0);
	}

	private static int CurrentUserId()
	{
		return PolyMobileAuthAPI.CurrentUserInfo.Id <= 0 ? 1 : PolyMobileAuthAPI.CurrentUserInfo.Id;
	}

	private async void BuildEditor()
	{
		if (_editorBuilt)
		{
			return;
		}

		GetNodeOrNull<ScrollContainer>("VBoxContainer/MarginContainer/VBoxContainer/ScrollContainer2")?.Hide();
		GridContainer? grid = GetNodeOrNull<GridContainer>("VBoxContainer/MarginContainer/VBoxContainer/ScrollContainer/VBoxContainer/Grid");
		if (grid == null)
		{
			return;
		}

		foreach (Node child in grid.GetChildren())
		{
			child.QueueFree();
		}

		grid.Columns = 1;
		_editorBuilt = true;

		try
		{
			MobileAvatarCatalog catalog = await PolyAPI.GetAvatarCatalog();
			AddCategory(grid, "Body", catalog.Bodies, "body");
			AddCategory(grid, "Face", catalog.Faces, "face");
			AddCategory(grid, "Shirt", catalog.Shirts, "shirt");
			AddCategory(grid, "Hat", catalog.Accessories, "hat");
		}
		catch (System.Exception ex)
		{
			PT.PrintErr("Avatar catalog load failed: ", ex);
		}
	}

	private void AddCategory(GridContainer grid, string title, MobileAvatarItem[] items, string category)
	{
		if (items == null)
		{
			return;
		}

		Label header = new()
		{
			Text = title,
			CustomMinimumSize = new(0, 40),
			VerticalAlignment = VerticalAlignment.Center,
			Modulate = new(0.82f, 0.9f, 1, 1)
		};
		header.AddThemeFontSizeOverride("font_size", 22);
		grid.AddChild(header);

		GridContainer row = new() { Columns = 2 };
		row.AddThemeConstantOverride("h_separation", 10);
		row.AddThemeConstantOverride("v_separation", 10);
		grid.AddChild(row);

		foreach (MobileAvatarItem item in items)
		{
			string id = item.Id;
			Button button = new()
			{
				Text = item.Name,
				CustomMinimumSize = new(150, 56),
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			button.Pressed += () => OnItemSelected(category, id);
			row.AddChild(button);
		}
	}

	private async void OnItemSelected(string category, string id)
	{
		MobileAvatarSetRequest req = new();
		switch (category)
		{
			case "body":
				req.AvatarId = id;
				break;
			case "face":
				req.FaceId = id;
				break;
			case "shirt":
				req.ShirtId = id;
				break;
			case "hat":
				req.AccessoryId = id;
				break;
		}

		try
		{
			await PolyAPI.SetAvatar(req);
			_polytorian?.LoadAppearance(CurrentUserId(), loadTool: false);
			_polytorian?.PlayIdle();
		}
		catch (System.Exception ex)
		{
			PT.PrintErr("Avatar update failed: ", ex);
		}
	}
}
