// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Attributes;
using Polytoria.Datamodel.Resources;
using Polytoria.Networking;
using Polytoria.Schemas.API;
using Polytoria.Scripting;
using Polytoria.Shared;
using Polytoria.Shared.Misc;
using Polytoria.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Polytoria.Datamodel;

[Instantiable]
public sealed partial class PolytorianModel : CharacterModel
{
	private const double NetLookBlendUpdateInterval = 0.1;
	private double _lastNetUpdateTime = 0.0;

	private static readonly BoxShape3D _collisionBox = new() { Size = new(2f, 5.8f, 1f) };
	internal Node3D? CollisionPivot;
	internal CollisionShape3D? CollisionShape;
	private Physical? _oldPhyParent;

	internal MeshInstance3D HeadMeshInstance = null!;
	internal MeshInstance3D TorsoMeshInstance = null!;
	internal MeshInstance3D LeftArmMeshInstance = null!;
	internal MeshInstance3D RightArmMeshInstance = null!;
	internal MeshInstance3D LeftLegMeshInstance = null!;
	internal MeshInstance3D RightLegMeshInstance = null!;
	internal Node3D Pivot = null!;

	private const float BlendSpeed = 5f;
	private const float LookBlendSpeed = 15f;
	private const string DefaultBodyColor = "#FFFFFF";
	private const string BlockyAvatarObjPath = "res://assets/models/avatars/soft-blocky/my_blocky_character.obj.txt";
	private static readonly Vector3 BlockyAvatarScale = new(0.86f, 1.42f, 0.88f);
	private static readonly Dictionary<string, ArrayMesh> _blockyAvatarMeshCache = [];

	private int _loadAppearanceCount = 0;

	internal Skeleton3D Skeleton = null!;
	internal AnimationTree AnimTree = null!;

	private ImageAsset? _faceImage;
	private MeshAsset? _bodyMesh;
	private readonly StandardMaterial3D _headMat = new();
	private readonly StandardMaterial3D _blockyHeadMat = new();
	private readonly StandardMaterial3D _faceMat = new();
	private readonly StandardMaterial3D _customFaceMat = new();
	private readonly StandardMaterial3D _torsoMat = new();
	private readonly StandardMaterial3D _leftArmMat = new();
	private readonly StandardMaterial3D _rightArmMat = new();
	private readonly StandardMaterial3D _leftLegMat = new();
	private readonly StandardMaterial3D _rightLegMat = new();
	private readonly StandardMaterial3D[] _shirtMats = new StandardMaterial3D[3];
	private readonly StandardMaterial3D[] _pantsMats = new StandardMaterial3D[2];
	private PhysicalBoneSimulator3D _ragdollBoneSim = null!;
	private PhysicalBoneSimulator3D? _lastPhysicalBoneSim = null!;
	private readonly Dictionary<string, float> _blendTargets = [];
	private int _toBeLoadedCount = 0;
	private bool _faceLoaded = false;
	private float _lastLookBlendX = 0;
	private float _lastLookBlendY = 0;
	private bool _faceOverrided = false;
	private bool _bodyOverrided = false;
	private CharacterAnimHelper _helper = null!;
	private readonly Dictionary<CharacterAttachmentEnum, Dynamic> _attachmentEnumToDyn = [];
	private PackedScene? _bodyPkScene;
	private bool _updateClothDirty = false;
	private string _bodyStyle = "default";
	private readonly List<MeshInstance3D> _blockyBodyParts = [];
	private MeshInstance3D? _blockyFaceInstance;

	public PhysicalBone3D? VelocityPhysicalBone;

	[Editable, ScriptProperty, Export, SyncVar]
	public Color HeadColor
	{
		get => _headMat.AlbedoColor;
		set
		{
			_headMat.AlbedoColor = value;
			_blockyHeadMat.AlbedoColor = value;
			_faceMat.AlbedoColor = new(1, 1, 1, value.A);
			MatApplyAlpha(_headMat, value);
			MatApplyAlpha(_blockyHeadMat, value);
			RefreshBodyStyleFromSyncedColors();
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, SyncVar]
	public Color TorsoColor
	{
		get => _torsoMat.AlbedoColor;
		set
		{
			_torsoMat.AlbedoColor = value;
			_shirtMats[1].AlbedoColor = new(1, 1, 1, value.A);
			MatApplyAlpha(_torsoMat, value);
			RefreshBodyStyleFromSyncedColors();
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, SyncVar]
	public Color LeftArmColor
	{
		get => _leftArmMat.AlbedoColor;
		set
		{
			_leftArmMat.AlbedoColor = value;
			_shirtMats[0].AlbedoColor = new(1, 1, 1, value.A);
			MatApplyAlpha(_leftArmMat, value);
			RefreshBodyStyleFromSyncedColors();
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, SyncVar]
	public Color RightArmColor
	{
		get => _rightArmMat.AlbedoColor;
		set
		{
			_rightArmMat.AlbedoColor = value;
			_shirtMats[2].AlbedoColor = new(1, 1, 1, value.A);
			MatApplyAlpha(_rightArmMat, value);
			RefreshBodyStyleFromSyncedColors();
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, SyncVar]
	public Color LeftLegColor
	{
		get => _leftLegMat.AlbedoColor;
		set
		{
			_leftLegMat.AlbedoColor = value;
			_pantsMats[0].AlbedoColor = new(1, 1, 1, value.A);
			MatApplyAlpha(_leftLegMat, value);
			RefreshBodyStyleFromSyncedColors();
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, SyncVar]
	public Color RightLegColor
	{
		get => _rightLegMat.AlbedoColor;
		set
		{
			_rightLegMat.AlbedoColor = value;
			_pantsMats[1].AlbedoColor = new(1, 1, 1, value.A);
			MatApplyAlpha(_rightLegMat, value);
			RefreshBodyStyleFromSyncedColors();
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty, NoSync, Attributes.Obsolete("Use FaceImage instead"), CloneIgnore]
	public int FaceID
	{
		get => (int)((_faceImage is PTImageAsset polyImg) ? polyImg.ImageID : 0);
		set
		{
			if (value == 0) { FaceImage = null; return; }
			PTImageAsset imgAsset = new();
			FaceImage = imgAsset;
			imgAsset.ImageID = (uint)value;
		}
	}

	[Editable, ScriptProperty, SyncVar]
	public ImageAsset? FaceImage
	{
		get => _faceImage;
		set
		{
			if (_faceImage != null && _faceImage != value)
			{
				_faceImage.ResourceLoaded -= OnFaceLoaded;
				_faceImage.UnlinkFrom(this);
			}
			_faceImage = value;

			_faceMat.AlbedoTexture = null;
			if (_faceImage != null)
			{
				_faceOverrided = true;
				_faceLoaded = false;
				AddLoadCount();
				_faceImage.LinkTo(this);
				_faceImage.ResourceLoaded += OnFaceLoaded;

				if (_faceImage.IsResourceLoaded && _faceImage.Resource != null)
				{
					OnFaceLoaded(_faceImage.Resource);
				}
				else
				{
					_faceImage.QueueLoadResource();
				}
			}
			else
			{
				// Set to default face
				_faceMat.AlbedoTexture = GD.Load<Texture2D>("res://assets/textures/client/character/SmileFace.png");
			}
			OnPropertyChanged();
		}
	}

	[Editable, ScriptProperty]
	public MeshAsset? BodyMesh
	{
		get => _bodyMesh;
		set
		{
			if (_bodyMesh != null && _bodyMesh != value)
			{
				_bodyMesh.ResourceLoaded -= OnBodyLoaded;
				_bodyMesh.UnlinkFrom(this);
			}
			OnBodyLoaded(null);
			_bodyMesh = value;
			if (_bodyMesh != null)
			{
				AddLoadCount();
				_bodyOverrided = true;
				_bodyMesh.LinkTo(this);
				_bodyMesh.ResourceLoaded += OnBodyLoaded;
				if (_bodyMesh.IsResourceLoaded && _bodyMesh.Resource != null)
				{
					OnBodyLoaded(_bodyMesh.Resource);
				}
				else
				{
					_bodyMesh.QueueLoadResource();
				}
			}
			OnPropertyChanged();
		}
	}

	[ScriptProperty] public bool Ragdolling { get; private set; } = false;
	[ScriptProperty] public Vector3 RagdollPosition => VelocityPhysicalBone == null ? Vector3.Zero : VelocityPhysicalBone.GlobalPosition;
	[ScriptProperty] public Vector3 RagdollRotation => VelocityPhysicalBone == null ? Vector3.Zero : VelocityPhysicalBone.GlobalRotationDegrees.FlipEuler();

	// These two's not reliable yet, as it doesn't wait for mesh to load. TODO: Come back and fix
	public bool IsAvatarLoaded { get; private set; } = false;
	public event Action? AvatarLoaded;

	[ScriptProperty] public PTSignal RagdollStarted { get; private set; } = new();
	[ScriptProperty] public PTSignal RagdollStopped { get; private set; } = new();

	public override void Init()
	{
		FaceImage = null;
		_headMat.NextPass = _faceMat;

		_shirtMats[0] = new() { Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, RenderPriority = 1 };
		_shirtMats[1] = new() { Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, RenderPriority = 1 };
		_shirtMats[2] = new() { Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, RenderPriority = 1 };

		_pantsMats[0] = new() { Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, RenderPriority = 1 };
		_pantsMats[1] = new() { Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, RenderPriority = 1 };

		_faceMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

		_helper = new() { Name = "CharacterHelper", Target = this };
		Globals.Singleton.AddChild(_helper, true);

		Skeleton = GDNode.GetNode<Skeleton3D>("Character/Poly/Skeleton3D");
		Skeleton.ShowRestOnly = false;
		_ragdollBoneSim = GDNode.GetNode<PhysicalBoneSimulator3D>("Character/Poly/Skeleton3D/RagdollBone");
		HeadMeshInstance = GDNode.GetNode<MeshInstance3D>("Character/Poly/Skeleton3D/Head");
		TorsoMeshInstance = GDNode.GetNode<MeshInstance3D>("Character/Poly/Skeleton3D/Torso");
		LeftArmMeshInstance = GDNode.GetNode<MeshInstance3D>("Character/Poly/Skeleton3D/LeftArm");
		RightArmMeshInstance = GDNode.GetNode<MeshInstance3D>("Character/Poly/Skeleton3D/RightArm");
		LeftLegMeshInstance = GDNode.GetNode<MeshInstance3D>("Character/Poly/Skeleton3D/LeftLeg");
		RightLegMeshInstance = GDNode.GetNode<MeshInstance3D>("Character/Poly/Skeleton3D/RightLeg");
		Pivot = GDNode.GetNode<Node3D>("Character/Poly");

		Pivot.Scale = NodeSize;

		HeadMeshInstance.MaterialOverride = _headMat;
		TorsoMeshInstance.MaterialOverride = _torsoMat;
		LeftArmMeshInstance.MaterialOverride = _leftArmMat;
		RightArmMeshInstance.MaterialOverride = _rightArmMat;
		LeftLegMeshInstance.MaterialOverride = _leftLegMat;
		RightLegMeshInstance.MaterialOverride = _rightLegMat;

		AnimTree = GDNode.GetNode<AnimationTree>("AnimationTree");
		AnimTree.Active = true;

		base.Init();
		SetProcess(true);

		ApplyBodyParts();
	}

	public override void PreDelete()
	{
		// Free helper
		_helper?.QueueFree();

		// Free body part materials
		_headMat.Dispose();
		_blockyHeadMat.Dispose();
		_faceMat.Dispose();
		_torsoMat.Dispose();
		_leftArmMat.Dispose();
		_rightArmMat.Dispose();
		_leftLegMat.Dispose();
		_rightLegMat.Dispose();

		// Free materials
		_shirtMats[0].Dispose();
		_shirtMats[1].Dispose();
		_shirtMats[2].Dispose();
		_pantsMats[0].Dispose();
		_pantsMats[1].Dispose();

		base.PreDelete();
	}

	public override Node CreateGDNode()
	{
		return Globals.LoadNetworkedObjectScene(ClassName)!;
	}

	public override void EnterTree()
	{
		if (Parent is Physical phy)
		{
			_oldPhyParent = phy;

			// Configure default collision shape for PolytorianModel
			CollisionPivot = new()
			{
				Scale = NodeSize
			};
			CollisionShape = new()
			{
				Shape = _collisionBox
			};
			Physical.SetRemoteLinkOffset(CollisionShape, new(0, 3f - 0.1f, 0));
			Physical.SetRemoteLinkTarget(CollisionShape, CollisionPivot);
			GDNode.AddChild(CollisionPivot);
			CollisionPivot.Position = new(0, -3f, 0);

			phy.GDNode.AddChild(CollisionShape);
			phy.AddCollisionShape(CollisionShape);
			phy.UpdateCollision();
		}
		base.EnterTree();
	}

	public override void ExitTree()
	{
		if (_oldPhyParent != null)
		{
			_oldPhyParent.RemoveCollisionShape(CollisionShape!);
			if (Node.IsInstanceValid(CollisionPivot))
			{
				CollisionPivot.QueueFree();
			}

			CollisionPivot = null;
			CollisionShape = null;
		}
		base.ExitTree();
	}

	public override async void Ready()
	{
		if (Root == null)
		{
			// Create default character on null root (eg. loading screens/mobile)
			Animator = New<Animator>();
			Animator.Name = "Animator";
			Animator.Parent = this;
		}

		Animator = await WaitChild<Animator>("Animator", 5);

		if (Animator == null) return;

		AnimTree.AdvanceExpressionBaseNode = _helper.GetPath();

		Animator.SetNetworkAuthority(NetworkAuthority);

		Animator.AnimationTree = AnimTree;
		Animator.AnimatorInit();
		Animator.ImportAnimationRaw("emote_dance", "Dance");
		Animator.ImportAnimationRaw("emote_helicopter", "Helicopter");
		Animator.ImportAnimationRaw("emote_sit", "Sit");

		Animator.ImportOneShotAnimationRaw("emote_wave", "Wave");
		Animator.ImportOneShotAnimationRaw("emote_point", "Point");

		AnimationLibrary emoteLib = Animator.AnimPlay.GetAnimationLibrary("");
		if (!emoteLib.HasAnimation("Dance2"))
		{
			emoteLib.AddAnimation("Dance2", GD.Load<Animation>("res://assets/animations/emotes/Dance2.res"));
		}
		Animator.ImportAnimationRaw("emote_dance2", "Dance2");
		/*
		Animator.ImportOneShotAnimationRaw("poly_welcome", "polytorian_2/welcome");
		Animator.ImportOneShotAnimationRaw("avataredit_pose1", "polytorian_2/pose1");
		Animator.ImportOneShotAnimationRaw("avataredit_pose2", "polytorian_2/pose2");
		Animator.ImportOneShotAnimationRaw("avataredit_pose3", "polytorian_2/pose3");
		*/

		Animator.ImportOneShotAnimationRaw("slash", "ToolSlash", true);
		Animator.ImportOneShotAnimationRaw("eat", "ToolEat", true);
		Animator.ImportOneShotAnimationRaw("drink", "ToolDrink", true);
	}

	internal override void OnNodeSizeChanged(Vector3 newSize)
	{
		Pivot?.Scale = newSize;
		CollisionPivot?.Scale = newSize;
		base.OnNodeSizeChanged(newSize);
	}

	public override void Process(double delta)
	{
		base.Process(delta);

		if (_updateClothDirty)
		{
			_updateClothDirty = false;
			UpdateClothMaterials();
		}

		foreach (KeyValuePair<string, float> kvp in _blendTargets)
		{
			string propName = kvp.Key;
			float target = kvp.Value;
			float current = (float)AnimTree.Get(propName);

			float targetBlendSpeed = BlendSpeed;
			float newValue;

			if (propName.Contains("Look"))
			{
				targetBlendSpeed = LookBlendSpeed;

				newValue = Mathf.Lerp(current, target, MathUtils.ExpDecay((float)delta, targetBlendSpeed));
			}
			else
			{
				newValue = Mathf.MoveToward(current, target, (float)delta * targetBlendSpeed);
			}

			AnimTree.Set(propName, newValue);
		}
	}

	private void UpdateClothMaterials()
	{
		StandardMaterial3D? BuildClothChain()
		{
			StandardMaterial3D? head = null;
			StandardMaterial3D? tail = null;
			foreach (var c in GetChildrenOfClass<Clothing>())
			{
				// Skip unloaded ones
				if (c.ClothTexture == null) continue;
				StandardMaterial3D m = new()
				{
					AlbedoTexture = c.ClothTexture,
					Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
					RenderPriority = 1
				};
				if (head == null) { head = m; tail = m; }
				else { tail!.NextPass = m; tail = m; }
			}
			return head;
		}

		StandardMaterial3D? DuplicateWithColor(StandardMaterial3D? source, StandardMaterial3D? previous)
		{
			if (source == null) return null;
			var dup = (StandardMaterial3D)source.Duplicate();
			if (previous != null) dup.AlbedoColor = previous.AlbedoColor;
			return dup;
		}

		var head = BuildClothChain();

		_leftArmMat.NextPass = DuplicateWithColor(head, _shirtMats[0]);
		_torsoMat.NextPass = DuplicateWithColor(head, _shirtMats[1]);
		_rightArmMat.NextPass = DuplicateWithColor(head, _shirtMats[2]);
		_leftLegMat.NextPass = DuplicateWithColor(head, _pantsMats[0]);
		_rightLegMat.NextPass = DuplicateWithColor(head, _pantsMats[1]);

		if (head != null)
		{
			_shirtMats[0] = (StandardMaterial3D)_leftArmMat.NextPass!;
			_shirtMats[1] = (StandardMaterial3D)_torsoMat.NextPass!;
			_shirtMats[2] = (StandardMaterial3D)_rightArmMat.NextPass!;
			_pantsMats[0] = (StandardMaterial3D)_leftLegMat.NextPass!;
			_pantsMats[1] = (StandardMaterial3D)_rightLegMat.NextPass!;
		}

	}

	private void OnFaceLoaded(Resource tex)
	{
		_faceMat.AlbedoTexture = (Texture2D)tex;
		_customFaceMat.AlbedoTexture = (Texture2D)tex;
		if (!_faceLoaded)
		{
			_faceLoaded = true;
			AssetLoadCheckout();
		}
	}

	private void AddLoadCount()
	{
		IsAvatarLoaded = false;
		_toBeLoadedCount++;
	}

	private void AssetLoadCheckout()
	{
		_toBeLoadedCount--;
		if (_toBeLoadedCount < 0)
		{
			_toBeLoadedCount = 0;
		}
		if (!IsAvatarLoaded && _toBeLoadedCount == 0)
		{
			IsAvatarLoaded = true;
			AvatarLoaded?.Invoke();
		}
	}

	private void OnBodyLoaded(Resource? resource)
	{
		if (resource is PackedScene scene)
		{
			if (_bodyPkScene == scene) return;
			_bodyPkScene = scene;

			Node n = scene.Instantiate();

			ApplyBodyPart(n, HeadMeshInstance, "Head");
			ApplyBodyPart(n, LeftArmMeshInstance, "LeftArm");
			ApplyBodyPart(n, RightArmMeshInstance, "RightArm");
			ApplyBodyPart(n, LeftLegMeshInstance, "LeftLeg");
			ApplyBodyPart(n, RightLegMeshInstance, "RightLeg");
			ApplyBodyPart(n, TorsoMeshInstance, "Torso");

			n.QueueFree();
		}
		else if (resource == null)
		{
			_bodyPkScene = null;
			ApplyBodyStyle(_bodyStyle);
		}
	}

	private readonly Dictionary<MeshInstance3D, Skin> _defaultSkins = [];

	private void CaptureDefaultSkins()
	{
		if (_defaultSkins.Count > 0) return;
		_defaultSkins[HeadMeshInstance] = HeadMeshInstance.Skin;
		_defaultSkins[LeftArmMeshInstance] = LeftArmMeshInstance.Skin;
		_defaultSkins[RightArmMeshInstance] = RightArmMeshInstance.Skin;
		_defaultSkins[LeftLegMeshInstance] = LeftLegMeshInstance.Skin;
		_defaultSkins[RightLegMeshInstance] = RightLegMeshInstance.Skin;
		_defaultSkins[TorsoMeshInstance] = TorsoMeshInstance.Skin;
	}

	private void ApplyBodyPart(MeshInstance3D m3d, string style, string k)
	{
		if (style == "chunky")
		{
			m3d.Mesh = GD.Load<Godot.Mesh>($"res://assets/models/bodyparts/chunky/{k}.tres");
			m3d.Skin = GD.Load<Skin>($"res://assets/models/bodyparts/chunky/{k}_skin.tres");
		}
		else if (style == "test")
		{
			m3d.Mesh = GD.Load<Godot.Mesh>($"res://assets/models/bodyparts/test/{k}.tres");
			if (_defaultSkins.TryGetValue(m3d, out Skin? skin))
			{
				m3d.Skin = skin;
			}
		}
		else
		{
			m3d.Mesh = GD.Load<Godot.Mesh>($"res://assets/models/bodyparts/default/{k}.tres");
			if (_defaultSkins.TryGetValue(m3d, out Skin? skin))
			{
				m3d.Skin = skin;
			}
		}
	}

	[ScriptProperty, SyncVar]
	public bool IsChunkyBody
	{
		get => _bodyStyle == "chunky";
		set
		{
			_bodyStyle = value ? "chunky" : (_bodyStyle == "chunky" ? "default" : _bodyStyle);
			ApplyBodyParts();
			OnPropertyChanged();
		}
	}

	[ScriptProperty, SyncVar]
	public bool IsTestBody
	{
		get => _bodyStyle == "test";
		set
		{
			_bodyStyle = value ? "test" : (_bodyStyle == "test" ? "default" : _bodyStyle);
			ApplyBodyParts();
			OnPropertyChanged();
		}
	}

	internal void ApplyBodyStyle(string? bodyStyle)
	{
		IsChunkyBody = bodyStyle == "chunky";
		IsTestBody = bodyStyle == "test";
	}

	private void ApplyBodyParts()
	{
		if (HeadMeshInstance == null) return;
		CaptureDefaultSkins();
		if (_bodyStyle == "test")
		{
			ApplyCustomAvatar(true);
			return;
		}
		ApplyCustomAvatar(false);
		ApplyBodyPart(HeadMeshInstance, _bodyStyle, "Head");
		ApplyBodyPart(LeftArmMeshInstance, _bodyStyle, "LeftArm");
		ApplyBodyPart(RightArmMeshInstance, _bodyStyle, "RightArm");
		ApplyBodyPart(LeftLegMeshInstance, _bodyStyle, "LeftLeg");
		ApplyBodyPart(RightLegMeshInstance, _bodyStyle, "RightLeg");
		ApplyBodyPart(TorsoMeshInstance, _bodyStyle, "Torso");
		SetDefaultBodyVisible(true);
		SetBlockyBodyVisible(false);
		SetBlockyFaceVisible(false);
	}

	private Node3D? _customAvatar;
	private string? _shirtStyle;
	private const string CustomAvatarScenePath = "res://assets/models/avatars/test/avatar.glb";

	[SyncVar, ScriptProperty]
	public string ShirtStyle
	{
		get => _shirtStyle ?? "default";
		internal set
		{
			_shirtStyle = value;
			if (_customAvatar != null)
				ApplyTestAvatarMaterials(_customAvatar, value == "kryndora");
			OnPropertyChanged();
		}
	}

	internal void SetTestShirtStyle(string? style)
	{
		ShirtStyle = style ?? "default";
	}

	private void ApplyCustomAvatar(bool enable)
	{
		if (enable)
		{
			SetDefaultBodyVisible(false);
			SetBlockyBodyVisible(false);
			SetBlockyFaceVisible(false);
			if (_customAvatar == null && Pivot != null)
			{
				PackedScene? scene = ResourceLoader.Load<PackedScene>(CustomAvatarScenePath);
				if (scene != null)
				{
					_customAvatar = scene.Instantiate<Node3D>();
					Pivot.AddChild(_customAvatar);
					AnimationPlayer? ap = FindAnimPlayer(_customAvatar);
					CustomAvatarDriver driver = new();
					_customAvatar.AddChild(driver);
					driver.Setup(ap, _customAvatar, this);
					ApplyTestAvatarMaterials(_customAvatar, _shirtStyle == "kryndora");
				}
			}
		}
		else
		{
			if (_customAvatar != null)
			{
				_customAvatar.QueueFree();
				_customAvatar = null;
			}
		}
	}

	private static AnimationPlayer? FindAnimPlayer(Node node)
	{
		if (node is AnimationPlayer ap) return ap;
		foreach (Node c in node.GetChildren())
		{
			AnimationPlayer? r = FindAnimPlayer(c);
			if (r != null) return r;
		}
		return null;
	}

	private void ApplyTestAvatarMaterials(Node root, bool withShirt)
	{
		MeshInstance3D? mesh = FindMeshInstance(root);
		if (mesh?.Mesh == null) return;
		Texture2D? shirt = withShirt ? ResourceLoader.Load<Texture2D>("res://assets/models/avatars/test/shirt.png") : null;
		for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
		{
			Material? cur = mesh.Mesh.SurfaceGetMaterial(i);
			string name = cur?.ResourceName ?? "";
			if (name.Contains("Torso") || name.Contains("Arm"))
			{
				mesh.SetSurfaceOverrideMaterial(i, shirt != null ? new StandardMaterial3D { AlbedoTexture = shirt } : null);
			}
			else if (!name.Contains("Leg"))
			{
				Color headColor = cur is BaseMaterial3D bm ? bm.AlbedoColor : new Color(0.85f, 0.85f, 0.9f);
				_customFaceMat.AlbedoTexture = _faceMat.AlbedoTexture;
				_customFaceMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
				_customFaceMat.Uv1Scale = new Vector3(0.85f, 0.85f, 1f);
				_customFaceMat.Uv1Offset = new Vector3(0.075f, 0.115f, 0f);
				StandardMaterial3D headMat = new() { AlbedoColor = headColor, NextPass = _customFaceMat };
				mesh.SetSurfaceOverrideMaterial(i, headMat);
			}
		}
	}

	private static MeshInstance3D? FindMeshInstance(Node node)
	{
		if (node is MeshInstance3D mi) return mi;
		foreach (Node c in node.GetChildren())
		{
			MeshInstance3D? r = FindMeshInstance(c);
			if (r != null) return r;
		}
		return null;
	}

	private static string ResolveAvatarBodyStyle(APIAvatarResponse avatarData)
	{
		return avatarData.BodyStyle == "softblocky" ? "chunky" : (avatarData.BodyStyle == "test" ? "test" : "default");
	}

	private void RefreshBodyStyleFromSyncedColors()
	{
		ApplyBodyParts();
	}

	private static bool IsNoobBodyColors(APIAvatarBodyColors colors)
	{
		return HexColorEquals(colors.Head, "#FFC71F")
			&& HexColorEquals(colors.Torso, "#1F52E5")
			&& HexColorEquals(colors.LeftArm, "#FFC71F")
			&& HexColorEquals(colors.RightArm, "#FFC71F")
			&& HexColorEquals(colors.LeftLeg, "#28A347")
			&& HexColorEquals(colors.RightLeg, "#28A347");
	}

	private static bool IsSoftBlockyBodyColors(APIAvatarBodyColors colors)
	{
		return HexColorEquals(colors.Head, "#B8C4C9")
			&& HexColorEquals(colors.Torso, "#B8C4C9")
			&& HexColorEquals(colors.LeftArm, "#B8C4C9")
			&& HexColorEquals(colors.RightArm, "#B8C4C9")
			&& HexColorEquals(colors.LeftLeg, "#B8C4C9")
			&& HexColorEquals(colors.RightLeg, "#B8C4C9");
	}

	private bool IsCurrentNoobBodyColors()
	{
		return ColorEquals(_headMat.AlbedoColor, "#FFC71F")
			&& ColorEquals(_torsoMat.AlbedoColor, "#1F52E5")
			&& ColorEquals(_leftArmMat.AlbedoColor, "#FFC71F")
			&& ColorEquals(_rightArmMat.AlbedoColor, "#FFC71F")
			&& ColorEquals(_leftLegMat.AlbedoColor, "#28A347")
			&& ColorEquals(_rightLegMat.AlbedoColor, "#28A347");
	}

	private bool IsCurrentSoftBlockyBodyColors()
	{
		return ColorEquals(_headMat.AlbedoColor, "#B8C4C9")
			&& ColorEquals(_torsoMat.AlbedoColor, "#B8C4C9")
			&& ColorEquals(_leftArmMat.AlbedoColor, "#B8C4C9")
			&& ColorEquals(_rightArmMat.AlbedoColor, "#B8C4C9")
			&& ColorEquals(_leftLegMat.AlbedoColor, "#B8C4C9")
			&& ColorEquals(_rightLegMat.AlbedoColor, "#B8C4C9");
	}

	private static bool ColorEquals(Color actual, string expected)
	{
		Color expectedColor = Color.FromString(expected, new Color());
		return Mathf.Abs(actual.R - expectedColor.R) < 0.01f
			&& Mathf.Abs(actual.G - expectedColor.G) < 0.01f
			&& Mathf.Abs(actual.B - expectedColor.B) < 0.01f;
	}

	private static bool HexColorEquals(string? actual, string expected)
	{
		return NormalizeHexColor(actual) == NormalizeHexColor(expected);
	}

	private static string NormalizeHexColor(string? color)
	{
		if (string.IsNullOrWhiteSpace(color)) return "";
		string normalized = color.Trim();
		if (normalized.StartsWith('#')) normalized = normalized[1..];
		return normalized.ToUpperInvariant();
	}

	private void SetDefaultBodyVisible(bool visible)
	{
		HeadMeshInstance.Visible = visible;
		TorsoMeshInstance.Visible = visible;
		LeftArmMeshInstance.Visible = visible;
		RightArmMeshInstance.Visible = visible;
		LeftLegMeshInstance.Visible = visible;
		RightLegMeshInstance.Visible = visible;
	}

	private void SetBlockyBodyVisible(bool visible)
	{
		if (visible)
		{
			EnsureBlockyBodyParts();
			ForceBlockyBodyOpaque();
		}

		foreach (MeshInstance3D part in _blockyBodyParts)
		{
			part.Visible = visible;
		}
	}

	private void SetBlockyFaceVisible(bool visible)
	{
		if (visible)
		{
			EnsureBlockyFace();
		}

		if (_blockyFaceInstance != null)
		{
			_blockyFaceInstance.Visible = visible;
		}
	}

	private Transform3D GetBoneGlobalRest(string boneName)
	{
		int boneIdx = Skeleton.FindBone(boneName);
		if (boneIdx < 0) return Transform3D.Identity;
		Transform3D globalRest = Skeleton.GetBoneRest(boneIdx);
		int parentIdx = Skeleton.GetBoneParent(boneIdx);
		while (parentIdx >= 0)
		{
			globalRest = Skeleton.GetBoneRest(parentIdx) * globalRest;
			parentIdx = Skeleton.GetBoneParent(parentIdx);
		}
		return globalRest;
	}

	private void EnsureBlockyFace()
	{
		if (_blockyFaceInstance != null || Skeleton == null) return;
		Node3D headAttachment = Skeleton.GetNode<Node3D>("O_Head");

		Transform3D boneRest = GetBoneGlobalRest("Head_2");
		Basis invBasis = boneRest.Basis.Inverse();

		_blockyFaceInstance = new()
		{
			Name = "BlockyFace",
			Mesh = CreateBlockyFaceMesh(),
			MaterialOverride = _faceMat,
			Transform = new Transform3D(invBasis, invBasis * new Vector3(0.0f, 0.5f, 0.0f)),
			Visible = false
		};

		headAttachment.AddChild(_blockyFaceInstance);
	}

	private static ArrayMesh CreateBlockMesh(Vector3 min, Vector3 max, int boneIndex)
	{
		List<Vector3> vertices = [];
		List<Vector3> normals = [];
		List<Vector2> uvs = [];
		List<int> indices = [];

		AddQuad(vertices, normals, uvs, indices, new(max.X, min.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(max.X, min.Y, max.Z), Vector3.Right);
		AddQuad(vertices, normals, uvs, indices, new(min.X, min.Y, max.Z), new(min.X, max.Y, max.Z), new(min.X, max.Y, min.Z), new(min.X, min.Y, min.Z), Vector3.Left);
		AddQuad(vertices, normals, uvs, indices, new(min.X, max.Y, min.Z), new(min.X, max.Y, max.Z), new(max.X, max.Y, max.Z), new(max.X, max.Y, min.Z), Vector3.Up);
		AddQuad(vertices, normals, uvs, indices, new(min.X, min.Y, max.Z), new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z), Vector3.Down);
		AddQuad(vertices, normals, uvs, indices, new(min.X, min.Y, max.Z), new(max.X, min.Y, max.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z), Vector3.Back);
		AddQuad(vertices, normals, uvs, indices, new(max.X, min.Y, min.Z), new(min.X, min.Y, min.Z), new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z), Vector3.Forward);

		return CreateSkinnedMesh(vertices, normals, uvs, indices, boneIndex);
	}

	private static ArrayMesh CreateBlockyFaceMesh()
	{
		List<Vector3> vertices = [];
		List<Vector3> normals = [];
		List<Vector2> uvs = [];
		List<int> indices = [];

		AddQuad(vertices, normals, uvs, indices, new(0.64f, -0.28f, 0.93f), new(-0.64f, -0.28f, 0.93f), new(-0.64f, 0.36f, 0.93f), new(0.64f, 0.36f, 0.93f), Vector3.Back);

		return CreateStaticMesh(vertices, normals, uvs, indices);
	}

	private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> indices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
	{
		int start = vertices.Count;
		vertices.Add(a);
		vertices.Add(b);
		vertices.Add(c);
		vertices.Add(d);

		normals.Add(normal);
		normals.Add(normal);
		normals.Add(normal);
		normals.Add(normal);

		uvs.Add(new(0, 1));
		uvs.Add(new(1, 1));
		uvs.Add(new(1, 0));
		uvs.Add(new(0, 0));

		indices.Add(start);
		indices.Add(start + 1);
		indices.Add(start + 2);
		indices.Add(start);
		indices.Add(start + 2);
		indices.Add(start + 3);
	}

	private static ArrayMesh CreateSkinnedMesh(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> indices, int boneIndex)
	{
		Vector3[] vertexArray = new Vector3[vertices.Count];
		Vector3[] normalArray = new Vector3[normals.Count];
		Vector2[] uvArray = new Vector2[uvs.Count];
		int[] indexArray = indices.ToArray();
		int[] boneArray = new int[vertices.Count * 4];
		float[] weightArray = new float[vertices.Count * 4];

		for (int i = 0; i < vertices.Count; i++)
		{
			vertexArray[i] = vertices[i];
			normalArray[i] = normals[i];
			uvArray[i] = uvs[i];
			boneArray[i * 4] = boneIndex;
			weightArray[i * 4] = 1f;
		}

		Godot.Collections.Array arrays = [];
		arrays.Resize((int)Godot.Mesh.ArrayType.Max);
		arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertexArray;
		arrays[(int)Godot.Mesh.ArrayType.Normal] = normalArray;
		arrays[(int)Godot.Mesh.ArrayType.TexUV] = uvArray;
		arrays[(int)Godot.Mesh.ArrayType.Bones] = boneArray;
		arrays[(int)Godot.Mesh.ArrayType.Weights] = weightArray;
		arrays[(int)Godot.Mesh.ArrayType.Index] = indexArray;

		ArrayMesh mesh = new();
		mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}

	private void EnsureBlockyBodyParts()
	{
		if (_blockyBodyParts.Count > 0) return;
		if (Skeleton == null) return;

		AddBlockyMeshPart("BlockyHead", "Head", "O_Head", "Head_2", new(0.0f, 0.5f, 0.0f), _blockyHeadMat);
		AddBlockyMeshPart("BlockyTorso", "Torso", "O_UpperTorso", "UpperTorso", new(0.0f, -0.6f, 0.0f), _torsoMat);
		AddBlockyMeshPart("BlockyLeftArm", "Arm_L", "O_UpperArm_L", "UpperArm.L", new(0.0f, -1.5f, 0.0f), _leftArmMat);
		AddBlockyMeshPart("BlockyRightArm", "Arm_R", "O_UpperArm_R", "UpperArm.R", new(0.0f, -1.5f, 0.0f), _rightArmMat);
		AddBlockyMeshPart("BlockyLeftLeg", "Leg_L", "O_UpperLeg_L", "UpperLeg.L", new(0.0f, -1.5f, 0.0f), _leftLegMat);
		AddBlockyMeshPart("BlockyRightLeg", "Leg_R", "O_UpperLeg_R", "UpperLeg.R", new(0.0f, -1.5f, 0.0f), _rightLegMat);
	}

	private void AddBlockyMeshPart(string name, string objPartName, string attachmentPath, string boneName, Vector3 uprightOffset, StandardMaterial3D material)
	{
		ArrayMesh? partMesh = LoadBlockyAvatarObjPart(objPartName);
		if (partMesh == null)
		{
			PT.PrintErr("Blocky avatar mesh part not found: ", objPartName);
			return;
		}

		MakeOpaque(material);

		Transform3D boneRest = GetBoneGlobalRest(boneName);
		Basis invBasis = boneRest.Basis.Inverse();

		MeshInstance3D mesh = new()
		{
			Name = name,
			Mesh = partMesh,
			MaterialOverride = material,
			Transform = new Transform3D(invBasis, invBasis * uprightOffset),
			Visible = false
		};
		// Apply scale after setting transform so it rescales the basis without affecting the origin offset.
		mesh.Scale = BlockyAvatarScale;

		Skeleton.GetNode<Node3D>(attachmentPath).AddChild(mesh);
		_blockyBodyParts.Add(mesh);
	}

	private static ArrayMesh? LoadBlockyAvatarObjPart(string objectName)
	{
		if (_blockyAvatarMeshCache.TryGetValue(objectName, out ArrayMesh? cached))
		{
			return cached;
		}

		if (!FileAccess.FileExists(BlockyAvatarObjPath))
		{
			PT.PrintErr("Blocky avatar OBJ missing: ", BlockyAvatarObjPath);
			return null;
		}

		using FileAccess file = FileAccess.Open(BlockyAvatarObjPath, FileAccess.ModeFlags.Read);
		string[] lines = file.GetAsText().Split('\n');
		List<Vector3> sourceVertices = [];
		List<Vector3> sourceNormals = [];
		List<Vector2> sourceUvs = [];
		List<Vector3> vertices = [];
		List<Vector3> normals = [];
		List<Vector2> uvs = [];
		List<int> indices = [];
		bool inTargetObject = false;

		foreach (string rawLine in lines)
		{
			string line = rawLine.Trim();
			if (line.Length == 0 || line.StartsWith('#')) continue;

			if (line.StartsWith("v ", StringComparison.Ordinal))
			{
				string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 4)
				{
					sourceVertices.Add(new(ParseObjFloat(parts[1]), ParseObjFloat(parts[2]), ParseObjFloat(parts[3])));
				}
			}
			else if (line.StartsWith("vt ", StringComparison.Ordinal))
			{
				string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 3)
				{
					sourceUvs.Add(new(ParseObjFloat(parts[1]), 1f - ParseObjFloat(parts[2])));
				}
			}
			else if (line.StartsWith("vn ", StringComparison.Ordinal))
			{
				string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length >= 4)
				{
					sourceNormals.Add(new(ParseObjFloat(parts[1]), ParseObjFloat(parts[2]), ParseObjFloat(parts[3])));
				}
			}
			else if (line.StartsWith("o ", StringComparison.Ordinal) || line.StartsWith("g ", StringComparison.Ordinal))
			{
				string currentName = line[2..].Trim();
				inTargetObject = string.Equals(currentName, objectName, StringComparison.OrdinalIgnoreCase);
			}
			else if (inTargetObject && line.StartsWith("f ", StringComparison.Ordinal))
			{
				string[] face = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				if (face.Length < 4) continue;

				int[] faceIndices = new int[face.Length - 1];
				for (int i = 1; i < face.Length; i++)
				{
					faceIndices[i - 1] = AddObjVertex(face[i], sourceVertices, sourceNormals, sourceUvs, vertices, normals, uvs);
				}

				for (int i = 1; i < faceIndices.Length - 1; i++)
				{
					indices.Add(faceIndices[0]);
					indices.Add(faceIndices[i]);
					indices.Add(faceIndices[i + 1]);
				}
			}
		}

		if (vertices.Count == 0 || indices.Count == 0)
		{
			return null;
		}

		CenterMeshVertices(vertices);
		ArrayMesh mesh = CreateStaticMesh(vertices, normals, uvs, indices);
		_blockyAvatarMeshCache[objectName] = mesh;
		return mesh;
	}

	private static void MakeOpaque(StandardMaterial3D material)
	{
		Color color = material.AlbedoColor;
		color.A = 1f;
		material.AlbedoColor = color;
		material.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
		material.NoDepthTest = false;
		material.DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.OpaqueOnly;
	}

	private void ForceBlockyBodyOpaque()
	{
		MakeOpaque(_blockyHeadMat);
		MakeOpaque(_torsoMat);
		MakeOpaque(_leftArmMat);
		MakeOpaque(_rightArmMat);
		MakeOpaque(_leftLegMat);
		MakeOpaque(_rightLegMat);
	}

	private static ArrayMesh CreateStaticMesh(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> indices)
	{
		Godot.Collections.Array arrays = [];
		arrays.Resize((int)Godot.Mesh.ArrayType.Max);
		arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices.ToArray();
		arrays[(int)Godot.Mesh.ArrayType.Normal] = normals.ToArray();
		arrays[(int)Godot.Mesh.ArrayType.TexUV] = uvs.ToArray();
		arrays[(int)Godot.Mesh.ArrayType.Index] = indices.ToArray();

		ArrayMesh mesh = new();
		mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}

	private static void MoveMeshVertices(List<Vector3> vertices, Vector3 offset)
	{
		for (int i = 0; i < vertices.Count; i++)
		{
			vertices[i] += offset;
		}
	}

	private static void CenterMeshVertices(List<Vector3> vertices)
	{
		if (vertices.Count == 0) return;

		Vector3 min = vertices[0];
		Vector3 max = vertices[0];
		foreach (Vector3 vertex in vertices)
		{
			min = new(
				Mathf.Min(min.X, vertex.X),
				Mathf.Min(min.Y, vertex.Y),
				Mathf.Min(min.Z, vertex.Z));
			max = new(
				Mathf.Max(max.X, vertex.X),
				Mathf.Max(max.Y, vertex.Y),
				Mathf.Max(max.Z, vertex.Z));
		}

		Vector3 center = (min + max) * 0.5f;
		for (int i = 0; i < vertices.Count; i++)
		{
			vertices[i] -= center;
		}
	}

	private static int AddObjVertex(string token, List<Vector3> sourceVertices, List<Vector3> sourceNormals, List<Vector2> sourceUvs, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs)
	{
		string[] fields = token.Split('/');
		int vertexIndex = ParseObjIndex(fields[0], sourceVertices.Count);
		int uvIndex = fields.Length > 1 && fields[1].Length > 0 ? ParseObjIndex(fields[1], sourceUvs.Count) : -1;
		int normalIndex = fields.Length > 2 && fields[2].Length > 0 ? ParseObjIndex(fields[2], sourceNormals.Count) : -1;

		vertices.Add(sourceVertices[vertexIndex]);
		uvs.Add(uvIndex >= 0 ? sourceUvs[uvIndex] : Vector2.Zero);
		normals.Add(normalIndex >= 0 ? sourceNormals[normalIndex] : Vector3.Up);
		return vertices.Count - 1;
	}

	private static int ParseObjIndex(string value, int count)
	{
		int parsed = int.Parse(value, CultureInfo.InvariantCulture);
		return parsed > 0 ? parsed - 1 : count + parsed;
	}

	private static float ParseObjFloat(string value)
	{
		return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
	}

	private static void ApplyBodyPart(Node source, MeshInstance3D target, string sourceName)
	{
		if (source.GetNodeOrNull($"Poly/Skeleton3D/{sourceName}") is MeshInstance3D m3d)
		{
			target.Mesh = m3d.Mesh;
		}
		else
		{
			throw new Exception("Invalid Body Mesh");
		}
	}

	[ScriptMethod]
	public void StartRagdoll(Vector3? force = null)
	{
		force ??= Vector3.Zero;
		Rpc(nameof(NetStartRagdoll), force.Value);
	}

	[ScriptMethod]
	public void StopRagdoll()
	{
		Rpc(nameof(NetStopRagdoll));
	}

	[NetRpc(AuthorityMode.Authority, CallLocal = true, TransferMode = TransferMode.Reliable)]
	private async void NetStartRagdoll(Vector3 force)
	{
		if (_lastPhysicalBoneSim != null) return;

		// need duplicates cuz godot won't adapt dynamically to bones
		PhysicalBoneSimulator3D s = (PhysicalBoneSimulator3D)_ragdollBoneSim.Duplicate();

		VelocityPhysicalBone = s.GetNode<PhysicalBone3D>("Physical Bone UpperTorso");

		Skeleton.AddChild(s);

		s.Active = true;
		s.PhysicalBonesStartSimulation();

		_lastPhysicalBoneSim = s;

		VelocityPhysicalBone.LinearVelocity = force / VelocityPhysicalBone.GravityScale;
		Ragdolling = true;
		RagdollStarted.Invoke();
	}

	[NetRpc(AuthorityMode.Authority, CallLocal = true, TransferMode = TransferMode.Reliable)]
	private void NetStopRagdoll()
	{
		if (_lastPhysicalBoneSim == null) return;

		_lastPhysicalBoneSim.PhysicalBonesStopSimulation();
		_lastPhysicalBoneSim.Active = false;
		_lastPhysicalBoneSim.QueueFree();
		_lastPhysicalBoneSim = null;

		Ragdolling = false;
		RagdollStopped.Invoke();
	}

	[ScriptMethod]
	public override Dynamic GetAttachment(CharacterAttachmentEnum attachmentEnum)
	{
		if (!_attachmentEnumToDyn.TryGetValue(attachmentEnum, out Dynamic? dyn))
		{
			Node3D a = GetNode3DAttachment(attachmentEnum);
			dyn = New<Dynamic>();
			dyn.OverrideGDNode(a);
		}

		return dyn;
	}

	public Node3D GetNode3DAttachment(CharacterAttachmentEnum attachmentEnum)
	{
		Node3D result = attachmentEnum switch
		{
			CharacterAttachmentEnum.Head => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_Head/HeadAttachment"),
			CharacterAttachmentEnum.UpperTorso => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_UpperTorso/UpperTorsoAttachment"),
			CharacterAttachmentEnum.LowerTorso => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_LowerTorso/LowerTorsoAttachment"),
			CharacterAttachmentEnum.ShoulderLeft => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_UpperArm_L/ShoulderLeftAttachment"),
			CharacterAttachmentEnum.ShoulderRight => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_UpperArm_R/RightShoulderAttachment"),
			CharacterAttachmentEnum.ElbowLeft => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_LowerArm_L/LeftElbowAttachment"),
			CharacterAttachmentEnum.ElbowRight => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_LowerArm_R/RightElbowAttachment"),
			CharacterAttachmentEnum.HandLeft => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_Hand_L/LeftHandAttachment"),
			CharacterAttachmentEnum.HandRight => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_Hand_R/RightHandAttachment"),
			CharacterAttachmentEnum.LegLeft => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_UpperLeg_L/LeftLegAttachment"),
			CharacterAttachmentEnum.LegRight => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_UpperLeg_R/RightLegAttachment"),
			CharacterAttachmentEnum.KneeLeft => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_LowerLeg_L/LeftKneeAttachment"),
			CharacterAttachmentEnum.KneeRight => GDNode.GetNode<Node3D>("Character/Poly/Skeleton3D/O_LowerLeg_R/RightKneeAttachment"),
			_ => throw new NotImplementedException(),
		};

		return result;
	}

	public override void RecvBlendValue(CharacterModelBlendEnum blendName, float blendValue)
	{
		string propName = "";
		switch (blendName)
		{
			case CharacterModelBlendEnum.Sitting:
				propName = "parameters/Sit/blend_amount";
				break;
			case CharacterModelBlendEnum.ToolHoldLeft:
				propName = "parameters/GearHold_L/blend_amount";
				break;
			case CharacterModelBlendEnum.ToolHoldRight:
				propName = "parameters/GearHold_R/blend_amount";
				break;
			case CharacterModelBlendEnum.LookX:
				propName = "parameters/LookXAdd/add_amount";
				break;
			case CharacterModelBlendEnum.LookY:
				propName = "parameters/LookYAdd/add_amount";
				break;
		}

		if (propName != "")
		{
			_blendTargets[propName] = blendValue;
		}
	}

	public override void RecvSpeedValue(float speedValue)
	{
		if (AnimTree == null) return;
		AnimTree.Set("parameters/TimeScale/scale", speedValue);
	}

	public override void ApplyCameraModifier(Camera camera)
	{
		Camera3D cam3D = camera.Camera3D;
		Transform3D camTransform = cam3D.GlobalTransform;
		Transform3D charTransform = GetGlobalTransform();

		Vector3 camForward = -camTransform.Basis.Z.Normalized();

		Vector3 localForward = charTransform.Basis.Inverse() * camForward;
		localForward = localForward.Normalized();

		float lookY = Mathf.Clamp(localForward.Y, -1f, 1f);
		float lookX = -localForward.X;

		if (lookX != _lastLookBlendX)
		{
			_lastLookBlendX = lookX;
		}

		if (lookY != _lastLookBlendY)
		{
			_lastLookBlendY = lookY;
		}

		NetRecvLookBlend(lookY, lookX);

		if (Time.GetTicksMsec() / 1000.0 >= _lastNetUpdateTime + NetLookBlendUpdateInterval)
		{
			_lastNetUpdateTime = Time.GetTicksMsec() / 1000.0;
			Rpc(nameof(NetRecvLookBlend), lookY, lookX);
		}
	}

	[NetRpc(AuthorityMode.Authority, TransferMode = TransferMode.UnreliableOrdered)]
	private void NetRecvLookBlend(float lookYBlend, float lookXBlend)
	{
		RecvBlendValue(CharacterModelBlendEnum.LookX, lookXBlend);
		RecvBlendValue(CharacterModelBlendEnum.LookY, lookYBlend);
	}

	[ScriptMethod]
	public void LoadAppearance(int userID, bool loadTool = true)
	{
		ClearAppearance();
		_ = LoadAppearanceSafe(userID, loadTool);
	}

	private async Task LoadAppearanceSafe(int userID, bool loadTool)
	{
		try
		{
			await InternalLoadAppearance(userID, loadTool);
		}
		catch (OperationCanceledException)
		{
			// Appearance reloads can cancel each other during joins and respawns.
		}
		catch (Exception ex)
		{
			PT.PrintErr("Avatar appearance load failed: ", ex);
		}
	}

	[ScriptMethod]
	public void ClearAppearance()
	{
		HeadColor = Color.FromString(DefaultBodyColor, new Color());
		TorsoColor = Color.FromString(DefaultBodyColor, new Color());
		LeftArmColor = Color.FromString(DefaultBodyColor, new Color());
		RightArmColor = Color.FromString(DefaultBodyColor, new Color());
		LeftLegColor = Color.FromString(DefaultBodyColor, new Color());
		RightLegColor = Color.FromString(DefaultBodyColor, new Color());
		FaceImage = null;
		_faceOverrided = false;
		_bodyOverrided = false;

		foreach (Instance item in GetChildren())
		{
			if (item is Accessory or Clothing)
			{
				item.Delete();
			}
		}
	}

	private static void MatApplyAlpha(StandardMaterial3D m, Color a)
	{
		m.Transparency = a.A == 1 ? BaseMaterial3D.TransparencyEnum.Disabled : BaseMaterial3D.TransparencyEnum.Alpha;
	}

	internal async Task<AvatarLoadResponse> InternalLoadAppearance(int userID, bool loadTool = false, bool loadToolNpc = false)
	{
		_loadAppearanceCount++;

		// Prevent reloading
		int myCount = _loadAppearanceCount;

		APIAvatarResponse avatarData = await PolyAPI.GetUserAvatarFromID(userID);
		if (myCount != _loadAppearanceCount) throw new OperationCanceledException("The avatar is cancelled");

		if (IsDeleted)
		{
			throw new OperationCanceledException("The avatar is deleted");
		}

		// Apply body color
		HeadColor = Color.FromString(avatarData.Colors.Head, new Color());
		TorsoColor = Color.FromString(avatarData.Colors.Torso, new Color());
		LeftArmColor = Color.FromString(avatarData.Colors.LeftArm, new Color());
		RightArmColor = Color.FromString(avatarData.Colors.RightArm, new Color());
		LeftLegColor = Color.FromString(avatarData.Colors.LeftLeg, new Color());
		RightLegColor = Color.FromString(avatarData.Colors.RightLeg, new Color());
		string resolvedBodyStyle = ResolveAvatarBodyStyle(avatarData);
		ShirtStyle = avatarData.ShirtStyle ?? "default";
		PT.Print("Avatar body style for user ", userID, ": ", resolvedBodyStyle, " (api: ", avatarData.BodyStyle, ")");
		ApplyBodyStyle(resolvedBodyStyle);
		_bodyOverrided = false;

		bool hasTool = false;

		foreach (APIAvatarAsset asset in avatarData.Assets)
		{
			if (asset.Type == "clothing")
			{
				PTImageAsset txt = New<PTImageAsset>();
				txt.ImageID = (uint)asset.ID;
				Clothing c = New<Clothing>();
				c.Name = asset.Name;
				c.Image = txt;
				c.Parent = this;
			}
			else if (asset.Type == "face")
			{
				if (_faceOverrided) continue;
				PTImageAsset face = New<PTImageAsset>();
				face.ImageID = (uint)asset.ID;
				FaceImage = face;
			}
			else if (asset.Type == "body")
			{
				if (_bodyOverrided) continue;
				var body = New<PTMeshAsset>();
				body.AssetID = (uint)asset.ID;
				BodyMesh = body;
			}
			else if (asset.Type == "hat")
			{
				try
				{
					Accessory? accessory = await Root.Insert.AccessoryAsync(asset.ID);
					if (myCount != _loadAppearanceCount) { accessory?.Delete(); throw new OperationCanceledException("The avatar is cancelled"); }
					if (IsDeleted)
					{
						accessory?.Delete();
						throw new OperationCanceledException("The avatar is deleted");
					}
					accessory?.Parent = this;
				}
				catch (Exception ex)
				{
					PT.PrintErr(ex);
				}
			}
			else if (asset.Type == "tool")
			{
				if (Parent is Player plr && loadTool)
				{
					hasTool = true;
					try
					{
						Tool? tool = await Root.Insert.ToolAsync(asset.ID);
						if (myCount != _loadAppearanceCount) { tool?.Delete(); throw new OperationCanceledException("The avatar is cancelled"); }
						if (IsDeleted)
						{
							tool?.Delete();
							throw new OperationCanceledException("The avatar is deleted");
						}
						tool?.Parent = plr.Inventory;
					}
					catch (Exception ex)
					{
						PT.PrintErr(ex);
					}
				}
				else if (Parent is NPC npc && loadToolNpc)
				{
					hasTool = true;
					try
					{
						Tool? tool = await Root.Insert.ToolAsync(asset.ID);
						if (myCount != _loadAppearanceCount) { tool?.Delete(); throw new OperationCanceledException("The avatar is cancelled"); }
						if (IsDeleted)
						{
							tool?.Delete();
							throw new OperationCanceledException("The avatar is deleted");
						}
						if (tool != null)
							npc.EquipTool(tool);
					}
					catch (Exception ex)
					{
						PT.PrintErr(ex);
					}
				}
			}
		}

		AssetLoadCheckout();

		return new() { HasTool = hasTool };
	}

	internal async Task WaitForAppearanceLoad()
	{
		if (FaceImage != null && !FaceImage.IsResourceLoaded)
		{
			await WaitForResourceLoad(FaceImage);
		}
		if (BodyMesh != null && !BodyMesh.IsResourceLoaded)
		{
			await WaitForResourceLoad(BodyMesh);
		}

		Instance checkOn = this;

		// Check on NPC for loading tools
		if (Parent is NPC)
		{
			checkOn = Parent;
		}

		foreach (var item in checkOn.GetDescendants())
		{
			if (item is Mesh m)
			{
				if (m.Loading)
				{
					await m.Loaded.Wait();
				}
			}
			else if (item is Clothing c)
			{
				if (c.Image != null && !c.Image.IsResourceLoaded)
				{
					await WaitForResourceLoad(c.Image);
				}
			}
		}
	}

	private static async Task WaitForResourceLoad(ResourceAsset asset)
	{
		await Task.WhenAny(asset.ResourceLoadedInternal.Wait(), Task.Delay(8000));
	}

	internal void QueueRenderCloth()
	{
		_updateClothDirty = true;
	}

	public void SetAnimationOverrideTo(bool to)
	{
		AnimTree.Active = !to;
	}

	internal struct AvatarLoadResponse()
	{
		public bool HasTool = false;
	}
}
