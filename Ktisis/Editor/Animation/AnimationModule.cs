﻿using System.Numerics;

using Dalamud.Hooking;
using Dalamud.Utility.Signatures;

using Ktisis.Interop.Hooking;
using Ktisis.Scene.Entities.Game;
using Ktisis.Structs.Actors;

namespace Ktisis.Editor.Animation;

public class AnimationModule : HookModule {
	public AnimationModule(
		IHookMediator hook
	) : base(hook) { }
	
	public bool SpeedControlEnabled { get; set; }
	public bool PositionLockEnabled { get; set; }
	
	// Speed control

	public unsafe void SetTimelineSpeed(AnimationTimeline* timeline, uint slot, float speed)
		=> this.SetTimelineSpeedHook?.Original(timeline, slot, speed);

	[Signature("83 FA 0E 73 22", DetourName = nameof(SetTimelineSpeedDetour))]
	private Hook<SetTimelineSpeedDelegate>? SetTimelineSpeedHook = null;
	private unsafe delegate void SetTimelineSpeedDelegate(AnimationTimeline* timeline, uint slot, float speed);

	private unsafe void SetTimelineSpeedDetour(AnimationTimeline* timeline, uint slot, float speed) {
		const int offset = CharacterEx.AnimationOffset + AnimationContainer.TimelineOffset;

		if (this.SpeedControlEnabled) {
			var chara = (CharacterEx*)((nint)timeline - offset);
			if (chara->IsGPose) return;
		}

		this.SetTimelineSpeedHook!.Original(timeline, slot, speed);
	}
	
	// Position lock
	
	[Signature("E8 ?? ?? ?? ?? 84 DB 74 3A", DetourName = nameof(UpdatePosDetour))]
	private Hook<UpdatePosDelegate> UpdatePosHook = null!;
	private unsafe delegate void UpdatePosDelegate(CharacterEx* chara);

	private unsafe void UpdatePosDetour(CharacterEx* chara) {
		if (this.PositionLockEnabled && chara->IsGPose)
			return;
		
		this.UpdatePosHook.Original(chara);
	}
	
	// Poses

	public unsafe void SetPose(ActorEntity actor, PoseModeEnum poseMode, byte pose) {
		var emoteMode = poseMode switch {
			PoseModeEnum.Battle => EmoteModeEnum.Normal,
			PoseModeEnum.SitGround => EmoteModeEnum.SitGround,
			PoseModeEnum.SitChair => EmoteModeEnum.SitChair,
			PoseModeEnum.Sleeping => EmoteModeEnum.Sleeping,
			_ => EmoteModeEnum.Normal
		};
		
		var chara = actor.IsValid ? (CharacterEx*)actor.Character : null;
		if (chara == null) return;
		
		var isOffset = emoteMode == EmoteModeEnum.SitChair;
		
		Vector3 offset;
		Vector3 offsetCam;
		if (isOffset) {
			offset = chara->DrawObjectOffset;
			offsetCam = chara->CameraOffsetSmooth;
		} else {
			offset = Vector3.Zero;
			offsetCam = Vector3.Zero;
		}
		
		var prev = chara->EmoteController.Pose;
		if (pose == 0xFF) pose = prev != 0xFF ? prev : byte.MinValue;

		this.CancelTimeline(&chara->Animation, 0, 0);
		this.SetEmoteMode(&chara->EmoteController, emoteMode);
		chara->EmoteController.Mode = poseMode;
		chara->EmoteController.Pose = pose;
			
		if (isOffset) {
			chara->EmoteController.IsDrawObjectOffset = false;
			this.EmoteControllerUpdateDrawOffset(&chara->EmoteController);
			chara->DrawObjectOffset = offset;
			chara->CameraOffsetSmooth = offsetCam;
		}
	}
	
	// Timelines

	[Signature("E8 ?? ?? ?? ?? 88 45 68")]
	public PlayEmoteDelegate PlayEmote = null!;
	public unsafe delegate bool PlayEmoteDelegate(EmoteController* controller, nint id, nint option, nint chair);

	[Signature("E8 ?? ?? ?? ?? F6 46 10 01")]
	private SetEmoteModeDelegate SetEmoteMode = null!;
	private unsafe delegate bool SetEmoteModeDelegate(EmoteController* a1, EmoteModeEnum mode);

	[Signature("E8 ?? ?? ?? ?? 0F BE 53 20")]
	private EmoteControllerUpdateDrawOffsetDelegate EmoteControllerUpdateDrawOffset = null!;
	private unsafe delegate nint EmoteControllerUpdateDrawOffsetDelegate(EmoteController* a1);

	[Signature("E8 ?? ?? ?? ?? 80 7B 17 01")]
	private CancelTimelineDelegate CancelTimeline = null!;
	private unsafe delegate nint CancelTimelineDelegate(AnimationContainer* a1, nint a2, nint a3);
	
	[Signature("E8 ?? ?? ?? ?? 4C 8B BC 24 ?? ?? ?? ?? 4C 8D 9C 24 ?? ?? ?? ?? 49 8B 5B 40")]
	public SetTimelineIdDelegate SetTimelineId = null!;
	public unsafe delegate bool SetTimelineIdDelegate(AnimationTimeline* a1, ushort a2, nint a3);
}
