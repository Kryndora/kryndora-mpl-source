// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;

namespace Polytoria.Creator.UI.TextEditor;

public sealed partial class TextEditorContainer : Control
{
	private static readonly PackedScene _textEditorPacked = GD.Load<PackedScene>("res://scenes/creator/tabs/text_editor/text_editor.tscn");

	public TextEditorRoot EditorRoot = null!;
	public string TargetFilePath = "";
	public string TargetFilePathAbsolute = "";
	public string OriginTabName = "";
	public CreatorSession TargetSession;

	public TextEditorContainer(string path, string fpath, CreatorSession session)
	{
		TargetFilePath = path;
		TargetFilePathAbsolute = fpath;
		TargetSession = session;
		EditorRoot = _textEditorPacked.Instantiate<TextEditorRoot>();
		EditorRoot.Container = this;
	}

	public override void _Ready()
	{
		AddChild(EditorRoot);
		EditorRoot.SavedChanged += OnSavedChanged;
		base._Ready();
	}

	private void OnSavedChanged(bool obj)
	{
		// Unsaved indicator
		Tabs.Singleton.SetTabTitle(this, OriginTabName + (obj == true ? "" : " (*)"));
	}
}
