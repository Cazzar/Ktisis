using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

using Ktisis.Common.Extensions;

using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using CSCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

using Ktisis.Editor.Characters.State;
using Ktisis.Scene.Decor;
using Ktisis.Scene.Entities.Character;
using Ktisis.Scene.Factory.Builders;
using Ktisis.Scene.Modules.Actors;
using Ktisis.Scene.Types;

namespace Ktisis.Scene.Entities.Game;

public class ActorEntity : CharaEntity, IDeletable {
	public readonly IGameObject Actor;
	
	public bool IsManaged { get; set; }

	public override bool IsValid => base.IsValid && this.Actor.IsValid();

	public ActorEntity(
		ISceneManager scene,
		IPoseBuilder pose,
		IGameObject actor
	) : base(scene, pose) {
		this.Type = EntityType.Actor;
		this.Actor = actor;
	}
	
	// Update handler

	public override void Update() {
		if (!this.IsObjectValid) return;
		this.UpdateChara();
		base.Update();
	}

	private unsafe void UpdateChara() {
		var chara = this.CharacterBaseEx;
		
		var address = (nint)chara;
		if (this.Address != address)
			this.Address = address;

		if (chara != null && this.Appearance.Wetness is { } wetness)
			chara->Wetness = wetness;
	}
	
	// Appearance
	
	public AppearanceState Appearance { get; } = new();

	private unsafe CustomizeData* GetCustomize() {
		var human = this.GetHuman();
		if (human != null) return &human->Customize;
		var chara = this.Character;
		if (chara != null) return &chara->DrawData.CustomizeData;
		return null;
	}

	public unsafe byte GetCustomizeValue(CustomizeIndex index) {
		if (this.Appearance.Customize.IsSet(index))
			return this.Appearance.Customize[index];

		var chara = this.GetHuman();
		return chara != null ? chara->Customize[(byte)index] : (byte)0;
	}
	
	// Viera ear handling

	public bool IsViera() => this.GetCustomizeValue(CustomizeIndex.Race) == 8;

	public bool TryGetEarId(out byte id) {
		if (!this.IsViera()) {
			id = 0;
			return false;
		}
		id = this.GetCustomizeValue(CustomizeIndex.RaceFeatureType);
		return true;
	}
	
	public bool TryGetEarIdAsChar(out char id) {
		var result = this.TryGetEarId(out var num);
		id = ((char)(96 + num));
		return result;
	}
	
	// GameObject
	
	public unsafe CSGameObject* CsGameObject => (CSGameObject*)this.Actor.Address;

	public unsafe CSCharacter* Character => this.CsGameObject != null && this.CsGameObject->IsCharacter() ? (CSCharacter*)this.CsGameObject : null;
	
	// CharacterBase

	public unsafe override Object* GetObject()
		=> this.CsGameObject != null ? (Object*)this.CsGameObject->DrawObject : null;

	public unsafe override CharacterBase* GetCharacter() {
		if (!this.IsObjectValid) return null;
		var ptr = this.CsGameObject != null ? this.CsGameObject->DrawObject : null;
		if (ptr == null || ptr->Object.GetObjectType() != ObjectType.CharacterBase)
			return null;
		return (CharacterBase*)ptr;
	}

	public unsafe Human* GetHuman() {
		var chara = this.GetCharacter();
		if (chara != null && chara->GetModelType() == CharacterBase.ModelType.Human)
			return (Human*)chara;
		return null;
	}

	public void Redraw() => this.Actor.Redraw();
	 
	// Deletable

	public bool Delete() {
		this.Scene.GetModule<ActorModule>().Delete(this);
		return false;
	}
}
