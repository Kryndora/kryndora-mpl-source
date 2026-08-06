// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Attributes;

namespace Polytoria.Datamodel;

[Instantiable]
public partial class Accessory : Dynamic
{
	private CharacterModel? _targetCharacter;
	private KrynAvatar.CharacterAttachmentEnum _targetAttachment;
	private bool _weldToModelRoot;
	private RemoteTransform3D? remoteTransform;
	private Node? _currentAttachNode;

	[Editable, ScriptProperty]
	public KrynAvatar.CharacterAttachmentEnum TargetAttachment
	{
		get => _targetAttachment;
		set
		{
			_targetAttachment = value;
			RefreshAttachment();
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty]
	public bool WeldToModelRoot
	{
		get => _weldToModelRoot;
		set
		{
			_weldToModelRoot = value;
			RefreshAttachment();
			OnPropertyChanged();
		}
	}

	private void RefreshAttachment()
	{
		if (_targetCharacter == null || !GDNode.IsInsideTree()) { return; }
		remoteTransform?.QueueFree();

		if (_weldToModelRoot && _targetCharacter is KrynAvatar ptm)
		{
			// Anchor stays in the model-root frame (stable everywhere) but the remote
			// transform is parented under the body-follow node so it inherits the jump.
			Node3D followNode = ptm.GetBodyFollowNode();
			Node3D modelRoot = (Node3D)ptm.GDNode;
			_currentAttachNode = followNode;
			remoteTransform = new()
			{
				UseGlobalCoordinates = true,
				UpdatePosition = true,
				UpdateRotation = true,
				UpdateScale = false
			};
			followNode.AddChild(remoteTransform, @internal: Node.InternalMode.Back);
			Transform3D rootT = modelRoot.GlobalTransform;
			remoteTransform.GlobalTransform = new Transform3D(rootT.Basis.Orthonormalized(), rootT.Origin);
			remoteTransform.RemotePath = remoteTransform.GetPathTo(GDNode);
			return;
		}

		Node attachNode = _targetCharacter.GetAttachment(TargetAttachment).GDNode;
		_currentAttachNode = attachNode;
		remoteTransform = new()
		{
			UseGlobalCoordinates = true,
			UpdatePosition = true,
			UpdateRotation = true,
			UpdateScale = false
		};
		attachNode.AddChild(remoteTransform, @internal: Node.InternalMode.Back);
		remoteTransform.RemotePath = remoteTransform.GetPathTo(GDNode);
	}

	public override void Process(double delta)
	{
		base.Process(delta);
		if (_weldToModelRoot && _targetCharacter is KrynAvatar ptm)
		{
			Node desired = ptm.GetBodyFollowNode();
			if (desired != _currentAttachNode)
				RefreshAttachment();
		}
	}

	public override void EnterTree()
	{
		base.EnterTree();
		if (Parent is CharacterModel c)
		{
			_targetCharacter = c;
		}
		RefreshAttachment();
	}

	public override void ExitTree()
	{
		base.ExitTree();
		_targetCharacter = null;
		remoteTransform?.QueueFree();
	}
}
