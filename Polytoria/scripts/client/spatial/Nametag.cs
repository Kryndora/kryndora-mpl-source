// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;
using Polytoria.Shared;

namespace Polytoria.Client;

public partial class Nametag : Node3D
{
	private Label _titleLabel = null!;
	private ProgressBar _healthBar = null!;
	private TextureRect _badge = null!;
	private Node3D _nametag = null!;

	public NPC Target = null!;

	public override void _Ready()
	{
		_nametag = Globals.CreateInstanceFromScene<Node3D>("res://scenes/client/spatial/nametag.tscn");
		AddChild(_nametag);
		_titleLabel = _nametag.GetNode<Label>("SubViewport/Control/NameRow/Title");
		_healthBar = _nametag.GetNode<ProgressBar>("SubViewport/Control/Healthbar");
		_badge = _nametag.GetNode<TextureRect>("SubViewport/Control/NameRow/Badge");
		_badge.Texture = GD.Load<Texture2D>("res://assets/textures/ui-icons/verify.png");
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		UpdateNameTag();
	}

	public void UpdateNameTag()
	{
		bool useNametag = Target.UseNametag;

		Camera? cam = Target.Root.Environment.CurrentCamera;

		// Check distance from camera if is with-in radius
		if (cam != null && useNametag)
		{
			useNametag = (cam.Position - GlobalPosition).Length() < Target.NametagVisibleRadius;
		}

		// Hide if self is Target
		if (Target == Target.Root.Players?.LocalPlayer)
		{
			useNametag = false;
		}

		Visible = useNametag;
		_titleLabel.Text = Target.DisplayName != string.Empty ? Target.DisplayName : Target.Name;
		_badge.Visible = Target is Player verifiedPlayer && verifiedPlayer.IsVerified;
		_healthBar.Visible = (Target.Health < Target.MaxHealth);
		_healthBar.Value = Target.Health;
		_healthBar.MaxValue = Target.MaxHealth;
	}
}
