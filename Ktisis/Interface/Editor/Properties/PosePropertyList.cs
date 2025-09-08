﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;

using GLib.Widgets;

using Ktisis.Common.Utility;
using Ktisis.Editor.Context.Types;
using Ktisis.Editor.Posing.Ik.TwoJoints;
using Ktisis.Editor.Posing.Ik.Types;
using Ktisis.Interface.Editor.Properties.Types;
using Ktisis.Localization;
using Ktisis.Scene.Decor.Ik;
using Ktisis.Scene.Entities;
using Ktisis.Scene.Entities.Game;
using Ktisis.Scene.Entities.Skeleton;
using Ktisis.Scene.Entities.Skeleton.Constraints;

namespace Ktisis.Interface.Editor.Properties;

public class PosePropertyList : ObjectPropertyList {
	private readonly IEditorContext _ctx;
	private readonly LocaleManager _locale;
	
	public PosePropertyList(
		IEditorContext ctx,
		LocaleManager locale
	) {
		this._ctx = ctx;
		this._locale = locale;
	}

	private const string IkCfgPopup = "##IkCfgPopup";
	
	public override void Invoke(IPropertyListBuilder builder, SceneEntity entity) {
		if (!TryGetEntityPose(entity, out var pose))
			return;
		
		builder.AddHeader("Pose", () => this.DrawPoseTab(pose), priority: 1);
		if (pose.IkController.GroupCount > 0)
			builder.AddHeader("Inverse Kinematics", () => this.DrawConstraintsTab(pose), priority: 2);
	}

	private void DrawPoseTab(EntityPose pose) {
		var spacing = ImGui.GetStyle().ItemInnerSpacing.X;
		
		// Parenting toggle
		ImGui.Checkbox(this._locale.Translate("transform_edit.transforms.parenting"), ref this._ctx.Config.Gizmo.ParentBones);
		
		// Import/export
		
		if (pose.Parent is not ActorEntity actor) return;
		ImGui.Spacing();
		
		if (ImGui.Button("Import"))
			this._ctx.Interface.OpenPoseImport(actor);
		ImGui.SameLine(0, spacing);
		if (ImGui.Button("Export"))
			this._ctx.Interface.OpenPoseExport(pose);
	}
	
	// Inverse Kinematics

	private void DrawConstraintsTab(EntityPose pose) {
		var style = ImGui.GetStyle();
		var spacing = style.ItemInnerSpacing.X;
		
		foreach (var (name, group) in pose.IkController.GetGroups()) {
			if (!TryGetGroupEndNode(pose, group, out var node))
				continue;

			using var _ = ImRaii.PushId($"IkProp_{name}");
			
			var enabled = group.IsEnabled;
			if (ImGui.Checkbox(" " + this._locale.Translate($"boneCategory.{name}"), ref enabled))
				group.IsEnabled = enabled;

			var btnSpace = Icons.CalcIconSize(FontAwesomeIcon.HandPointer).X
				+ Icons.CalcIconSize(FontAwesomeIcon.EllipsisH).X
				+ spacing * 3;

			ImGui.SameLine(0, spacing);
			ImGui.SameLine(0, ImGui.GetContentRegionAvail().X - btnSpace);

			using (ImRaii.PushColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive), node.IsSelected)) {
				var canSelect = !node.IsSelected || this._ctx.Selection.Count > 1;
				if (Buttons.IconButtonTooltip(FontAwesomeIcon.HandPointer, "Select", Vector2.Zero) && canSelect)
					node.Select(GuiHelpers.GetSelectMode());
			}

			ImGui.SameLine(0, spacing);

			if (Buttons.IconButtonTooltip(FontAwesomeIcon.EllipsisH, "Configure", Vector2.Zero))
				ImGui.OpenPopup(IkCfgPopup);

			if (!ImGui.IsPopupOpen(IkCfgPopup)) continue;
			
			using var popup = ImRaii.Popup(IkCfgPopup);
			if (popup.Success) this.DrawIkConfig(node);
		}
	}

	private void DrawIkConfig(IIkNode ik) {
		var isEnabled = ik.IsEnabled;
		if (ImGui.Checkbox("Enabled", ref isEnabled)) {
			if (isEnabled)
				ik.Enable();
			else
				ik.Disable();
		}
		
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		
		switch (ik) {
			case ICcdNode node:
				this.DrawCcd(node);
				break;
			case ITwoJointsNode node:
				this.DrawTwoJoints(node);
				break;
		}
	}
	
	// IK: CCD

	private void DrawCcd(ICcdNode node) {
		ImGui.SliderFloat(this._locale.Translate("transform_edit.ik.ccd.gain"), ref node.Group.Gain, 0.0f, 1.0f, "%.2f");
		ImGui.SliderInt(this._locale.Translate("transform_edit.ik.ccd.iterations"), ref node.Group.Iterations, 0, 60);
	}
	
	// IK: Two Joints

	private void DrawTwoJoints(ITwoJointsNode node) {
		ImGui.Checkbox(this._locale.Translate("transform_edit.ik.two_joints.enforce"), ref node.Group.EnforceRotation);
		
		ImGui.Spacing();
		
		ImGui.Text(this._locale.Translate("transform_edit.ik.two_joints.mode"));
		DrawIkMode(this._locale.Translate("transform_edit.ik.two_joints.fixed"), TwoJointsMode.Fixed, node.Group);
		DrawIkMode(this._locale.Translate("transform_edit.ik.two_joints.relative"), TwoJointsMode.Relative, node.Group);
		
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		
		ImGui.Text(this._locale.Translate("transform_edit.ik.two_joints.gain"));
		ImGui.SliderFloat("Shoulder##FirstWeight", ref node.Group.FirstBoneGain, 0.0f, 1.0f, "%.2f");
		ImGui.SliderFloat("Elbow##SecondWeight", ref node.Group.SecondBoneGain, 0.0f, 1.0f, "%.2f");
		ImGui.SliderFloat("Hand##HandWeight", ref node.Group.EndBoneGain, 0.0f, 1.0f, "%.2f");
		
		ImGui.Spacing();
		ImGui.Separator();
		ImGui.Spacing();
		
		ImGui.Text(this._locale.Translate("transform_edit.ik.two_joints.hinges"));
		ImGui.Spacing();
		ImGui.SliderFloat("Minimum", ref node.Group.MinHingeAngle, -1.0f, 1.0f, "%.2f");
		ImGui.SliderFloat("Maximum", ref node.Group.MaxHingeAngle, -1.0f, 1.0f, "%.2f");
		ImGui.SliderFloat3("Axis", ref node.Group.HingeAxis, -1.0f, 1.0f, "%.2f");
		
		ImGui.Spacing();
	}

	private static void DrawIkMode(string label, TwoJointsMode mode, TwoJointsGroup group) {
		var value = group.Mode == mode;
		if (ImGui.RadioButton(label, value))
			group.Mode = mode;
	}
	
	// Entity helpers

	private static bool TryGetEntityPose(SceneEntity entity, [NotNullWhen(true)] out EntityPose? result) {
		result = entity switch {
			ActorEntity actor => actor.Pose,
			BoneNode node => node.Pose,
			EntityPose pose => pose,
			_ => null
		};
		return result != null;
	}

	private static bool TryGetGroupEndNode(EntityPose pose, IIkGroup group, [NotNullWhen(true)] out IkEndNode? node) {
		node = pose.Recurse().FirstOrDefault(
			node => node is IkEndNode {
				Parent: IkNodeGroupBase grpNode
			} && grpNode.Group == group
		) as IkEndNode;

		return node != null;
	}
}
