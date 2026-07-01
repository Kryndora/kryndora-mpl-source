// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Datamodel;
using Polytoria.Datamodel.Services;

namespace Polytoria.Client.UI.Chat;

public partial class UIChatLabel : RichTextLabel
{
	private bool _isPending = false;
	private bool _isDeclined = false;
	private string _content = "";

	public string Content
	{
		get => _content;
		set
		{
			_content = value;
			UpdateContent();
		}
	}

	public Color NameColor = new(1, 1, 1);
	public string AuthorName = null!;
	public Player? AuthorPlayer;

	public bool IsPending
	{
		get => _isPending;
		set
		{
			_isPending = value;

			SelfModulate = _isPending ? new Color(1, 1, 1, 0.3f) : new Color(1, 1, 1, 1);
		}
	}
	public bool IsDeclined
	{
		get => _isDeclined;
		set
		{
			_isDeclined = value;
			Visible = !_isDeclined;
		}
	}

	public override void _Ready()
	{
		UpdateContent();
	}

	private void UpdateContent()
	{
		if (AuthorName == "")
		{
			Text = $"{ChatService.FormatEmojis(Content)}";
		}
		else
		{
			Text = $"[color={NameColor.ToHtml()}]{AuthorName}[/color]: {ChatService.FormatEmojis(Content)}";
		}
	}
}
