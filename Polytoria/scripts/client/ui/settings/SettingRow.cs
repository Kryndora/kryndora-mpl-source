using Godot;
using Polytoria.Shared.Settings;

namespace Polytoria.Client.UI;

public sealed partial class SettingRow : PanelContainer
{
	public SettingDef Definition = null!;

	public override void _Ready()
	{
		AddThemeStyleboxOverride("panel", CreatePanelStyle());

		HBoxContainer root = new()
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		root.AddThemeConstantOverride("separation", 18);
		AddChild(root);

		VBoxContainer textLayout = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		root.AddChild(textLayout);

		Label title = new() { Text = Definition.Label };
		title.AddThemeFontSizeOverride("font_size", 20);
		title.AddThemeColorOverride("font_color", new Color(0.92f, 0.97f, 1f));
		textLayout.AddChild(title);

		if (!string.IsNullOrEmpty(Definition.Description))
		{
			Label desc = new()
			{
				Text = Definition.Description,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			desc.AddThemeColorOverride("font_color", new Color(0.58f, 0.68f, 0.76f));
			textLayout.AddChild(desc);
		}

		if (Definition.RequiresRestart)
		{
			Label restart = new()
			{
				Text = "Requires restart"
			};
			restart.AddThemeColorOverride("font_color", new Color(1f, 0.6f, 0.6f));
			restart.AddThemeFontSizeOverride("font_size", 14);
			textLayout.AddChild(restart);
		}

		Control field = SettingFieldFactory.Create(Definition);
		field.CustomMinimumSize = new Vector2(220, 0);
		root.AddChild(field);

		base._Ready();
	}

	private static StyleBoxFlat CreatePanelStyle()
	{
		StyleBoxFlat box = new()
		{
			BgColor = new Color(0.055f, 0.095f, 0.14f, 1f),
			BorderColor = new Color(0.16f, 0.30f, 0.40f, 1f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10
		};

		box.SetContentMargin(Side.Left, 16);
		box.SetContentMargin(Side.Top, 14);
		box.SetContentMargin(Side.Right, 16);
		box.SetContentMargin(Side.Bottom, 14);
		return box;
	}
}
