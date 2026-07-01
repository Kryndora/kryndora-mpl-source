// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;

namespace Polytoria.Client.UI.Chat;

public partial class BubbleItem : Control
{
	private const float BubbleTimeLength = 5;
	private const float BubbleSizeOffset = 55;
	private AnimationPlayer _animPlay = null!;
	public string Content = null!;

	public override async void _Ready()
	{
		Visible = false;
		_animPlay = GetNode<AnimationPlayer>("AnimationPlayer");
		Label testLabel = GetNode<Label>("TestLabel");
		RichTextLabel textLabel = GetNode<RichTextLabel>("Pivot/Layout/Container/RichTextLabel");
		textLabel.Text = Content;
		testLabel.Text = Content;

		// Wait for textlabel's size to update
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

		testLabel.Visible = false;

		// Apply size based on test label
		textLabel.CustomMinimumSize = new(Mathf.Clamp(testLabel.Size.X + 48, 48, 320), 0);
		textLabel.Size = textLabel.CustomMinimumSize;

		Tween tween = CreateTween();
		PropertyTweener tweener = tween.TweenProperty(this, "custom_minimum_size", new Vector2(0, textLabel.Size.Y + BubbleSizeOffset), 0.4f);
		tweener.SetEase(Tween.EaseType.Out);
		tweener.SetTrans(Tween.TransitionType.Back);
		tween.Play();

		_animPlay.Play("appear");
		Visible = true;

		await ToSignal(GetTree().CreateTimer(BubbleTimeLength), Timer.SignalName.Timeout);
		Disappear();
	}

	public async void Disappear()
	{
		if (!IsInsideTree()) { return; }
		_animPlay.Play("disappear");
		await ToSignal(_animPlay, AnimationPlayer.SignalName.AnimationFinished);
		QueueFree();
	}
}
