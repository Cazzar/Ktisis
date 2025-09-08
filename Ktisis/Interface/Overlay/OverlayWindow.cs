using System.Diagnostics;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;

using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Common.Math;

using Ktisis.Editor.Context.Types;
using Ktisis.Editor.Transforms.Types;
using Ktisis.ImGuizmo;
using Ktisis.Interface.Types;
using Ktisis.Services.Game;

namespace Ktisis.Interface.Overlay;

public class OverlayWindow : KtisisWindow {
	private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground
		| ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus;

	private readonly IGameGui _gui;
	
	private readonly IEditorContext _ctx;
	private readonly IGizmo _gizmo;
	private readonly SceneDraw _sceneDraw;

	public OverlayWindow(
		IGameGui gui,
		IEditorContext ctx,
		IGizmo gizmo,
		SceneDraw draw
	) : base("##KtisisOverlay", WindowFlags) {
		this._gui = gui;
		this._ctx = ctx;
		this._gizmo = gizmo;
		this._sceneDraw = draw;
		this._sceneDraw.SetContext(ctx);
		this.PositionCondition = ImGuiCond.Always;
	}
	
	private ITransformMemento? Transform;

	public override void PreOpenCheck() {
		if (this._ctx.IsValid) return;
		Ktisis.Log.Verbose("Context for overlay window is stale, closing...");
		this.Close();
	}

	public override void PreDraw() {
		this.Size = ImGui.GetIO().DisplaySize;
		this.Position = Vector2.Zero;
	}
	
	// Main draw function

	public override void Draw() {
		if (!this._ctx.Config.Overlay.Visible) return;
		
		//var t = new Stopwatch();
		//t.Start();
		
		var gizmo = this.DrawGizmo();
		this._sceneDraw.DrawScene(gizmo: gizmo, gizmoIsEnded: this._gizmo.IsEnded);
		
		//t.Stop();
		//this.DrawDebug(t);
	}

	private bool DrawGizmo() {
		if (!this._ctx.Config.Gizmo.Visible)
			return false;
		
		var target = this._ctx.Transform.Target;
		var transform = target?.GetTransform();
		if (target == null || transform == null)
			return false;
		
		var view = CameraService.GetViewMatrix();
		var proj = CameraService.GetProjectionMatrix();
		if (view == null || proj == null || this.Size == null)
			return false;

		var size = this.Size.Value;
		this._gizmo.SetMatrix(view.Value, proj.Value);
		this._gizmo.BeginFrame(Vector2.Zero, size);

		var cfg = this._ctx.Config.Gizmo;
		this._gizmo.Mode = cfg.Mode;
		this._gizmo.Operation = cfg.Operation;
		this._gizmo.AllowAxisFlip = cfg.AllowAxisFlip;

		var matrix = transform.ComposeMatrix();
		var isManipulate = this._gizmo.Manipulate(ref matrix, out _);
		var isRaySnap = this.HandleShiftRaycast(ref matrix);
		if (isManipulate || isRaySnap) {
			this.Transform ??= this._ctx.Transform.Begin(target);
			this.Transform.SetMatrix(matrix);
		}

		this._gizmo.EndFrame();
		if (this._gizmo.IsEnded) {
			this.Transform?.Dispatch();
			this.Transform = null;
		}

		return true;
	}

	private bool HandleShiftRaycast(ref Matrix4x4 matrix) {
		if (!this._ctx.Config.Gizmo.AllowRaySnap)
			return false;
		
		if (!ImGui.IsKeyDown(ImGuiKey.ModShift) || !ImGuizmo.Gizmo.IsUsing || ImGuizmo.Gizmo.CurrentOperation != Operation.TRANSLATE)
			return false;

		if (!this._gui.ScreenToWorld(ImGui.GetMousePos(), out var hitPos))
			return false;

		matrix.Translation = hitPos;
		return true;
	}

	private void DrawDebug(Stopwatch t) {
		ImGui.SetCursorPosY(ImGui.GetStyle().WindowPadding.Y);
		for (var i = 0; i < 5; i++)
			ImGui.Spacing();
		ImGui.Text($"Context: {this._ctx.GetHashCode():X} ({this._ctx.IsValid})");
		ImGui.Text($"Scene: {this._ctx.Scene.GetHashCode():X} {this._ctx.Scene.UpdateTime:00.00}ms");
		ImGui.Text($"Overlay: {this.GetHashCode()} {t.Elapsed.TotalMilliseconds:00.00}ms");
		ImGui.Text($"Gizmo: {this._gizmo.GetHashCode():X} {this._gizmo.Id} ({this._gizmo.Operation}, {ImGuizmo.Gizmo.IsUsing})");
		var target = this._ctx.Transform.Target;
		ImGui.Text($"Target: {target?.GetHashCode() ?? 0:X7} {target?.GetType().Name ?? "NULL"} ({target?.Targets?.Count() ?? 0}, {target?.Primary?.Name ?? "NULL"})");
		var history = this._ctx.Actions.History;
		ImGui.Text($"History: {history.Count} ({history.CanUndo}, {history.CanRedo})");
	}
}
