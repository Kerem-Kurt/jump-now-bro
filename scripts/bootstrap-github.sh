#!/usr/bin/env bash
# scripts/bootstrap-github.sh
# Provision labels, milestones, and issues for JumpNowBro Phase 1.
# Idempotent: safe to re-run.

set -euo pipefail

gh auth status >/dev/null
REPO=$(gh repo view --json nameWithOwner -q .nameWithOwner)
echo "Bootstrapping GitHub setup for $REPO"

# --- Labels ------------------------------------------------------------------
create_label() {
  local name="$1" color="$2" desc="$3"
  gh label create "$name" --color "$color" --description "$desc" 2>/dev/null \
    || gh label edit "$name" --color "$color" --description "$desc" >/dev/null
}

echo "» Labels"
create_label "type: feat"     "0e8a16" "New feature or capability"
create_label "type: fix"      "d73a4a" "Bug fix"
create_label "type: refactor" "1d76db" "Internal restructuring; no observable behavior change"
create_label "type: chore"    "cccccc" "Tooling, repo config, no production code change"
create_label "type: test"     "5319e7" "Test scaffolding or new tests"
create_label "type: docs"     "0075ca" "Documentation only"
create_label "type: polish"   "ff77ee" "Game feel, juice, particles, SFX"

for area in player input gameplay util hud camera level tooling networking; do
  create_label "area: $area" "006b75" "Touches the $area subsystem"
done

create_label "phase: 1"        "fbca04" "Phase 1 — single-player local"
create_label "phase: 2"        "d93f0b" "Phase 2 — networking"
create_label "phase: post-mvp" "bfd4f2" "Deferred (wall jump, 8-way dash, etc.)"

create_label "blocker"          "b60205" "Must close before the next milestone tag"
create_label "good-first-task"  "7057ff" "Reserved for Phase 2 teammate onboarding"

# --- Milestones --------------------------------------------------------------
create_milestone() {
  local title="$1" desc="$2"
  if gh api "repos/$REPO/milestones?state=all" --jq '.[].title' | grep -qxF "$title"; then
    echo "  = $title"
  else
    gh api -X POST "repos/$REPO/milestones" -f title="$title" -f description="$desc" >/dev/null
    echo "  + $title"
  fi
}

echo "» Milestones"
create_milestone "v0.0-scaffolding" "Repo hygiene: asmdefs, layers, physics, P1/P2 input split, Bootstrap rename. No gameplay code yet."
create_milestone "v0.1-movement"    "Run, jump (variable + coyote + buffer), dash with one-charge refund. Feel locked at DESIGN §4."
create_milestone "v0.2-controlmap"  "Two input sources + ControlMapStore + SwapTrigger. EditMode tests green."
create_milestone "v0.3-level1"      "Level 1 'Hello, Partner' end-to-end: tilemap, camera, checkpoint, goal, one swap."
create_milestone "v0.4-mvp"         "All three levels + death/respawn + polish. Phase 1 complete."

# --- Issues ------------------------------------------------------------------
file_issue() {
  local title="$1" milestone="$2" labels="$3" body="$4"
  local existing
  existing=$(gh issue list --search "in:title \"$title\"" --state all --json number,title \
             --jq ".[] | select(.title==\"$title\") | .number" | head -n1)
  if [[ -n "$existing" ]]; then
    echo "  = #$existing  $title"
    return
  fi
  local url num
  url=$(gh issue create --title "$title" --milestone "$milestone" --label "$labels" --body "$body")
  num="${url##*/}"
  echo "  + #$num  $title"
}

# ============================================================================
# v0.0-scaffolding — 7 issues
# ============================================================================
echo "» Issues — v0.0-scaffolding"

file_issue "chore(physics): set 2D gravity to -50 and confirm fixed timestep 0.02" \
  "v0.0-scaffolding" "type: chore,area: tooling,phase: 1" \
"Set 2D physics gravity to (0, -50) in \`ProjectSettings/Physics2D.asset\` so the character falls at the rate DESIGN.md §4 specifies (50 units/s²). Confirm \`ProjectSettings/TimeManager.asset\` has Fixed Timestep = 0.02 (50 Hz) — should already be the Unity default, no change needed if so.

## Acceptance criteria
- [ ] \`Physics2D.gravity.y\` reads \`-50\`
- [ ] \`Fixed Timestep\` is \`0.02\`
- [ ] Diff is settings-only; no \`Assets/\` changes

DESIGN.md reference: §4 (tuning constants table)."

file_issue "chore(layers): add custom layers Player/Ground/OneWayPlatform/Hazard/Checkpoint/SwapTrigger/LevelGoal" \
  "v0.0-scaffolding" "type: chore,area: tooling,phase: 1" \
"Define seven custom layers in \`ProjectSettings/TagManager.asset\`:

| Slot | Layer |
|---|---|
| 6 | Player |
| 7 | Ground |
| 8 | OneWayPlatform |
| 9 | Hazard |
| 10 | Checkpoint |
| 11 | SwapTrigger |
| 12 | LevelGoal |

These are referenced by every collider/trigger placed in level scenes from v0.3 onward. Slot numbers matter — the collision matrix (next issue) is authored against these positions.

## Acceptance criteria
- [ ] All seven layers exist at the slot numbers above
- [ ] No layers renamed or removed from the Unity defaults"

file_issue "chore(physics): configure layer collision matrix" \
  "v0.0-scaffolding" "type: chore,area: tooling,phase: 1" \
"Edit the Physics2D layer collision matrix so Player interacts as designed:

- Player ↔ Ground: **collide**
- Player ↔ OneWayPlatform: **collide** (one-way behavior comes from \`PlatformEffector2D\` on the OneWayPlatform colliders, not from the matrix)
- Player ↔ Hazard, Checkpoint, SwapTrigger, LevelGoal: **trigger only** (those colliders set \`isTrigger = true\`)
- Every non-Player ↔ non-Player pairing: **ignore** (these layers never need to collide with each other)

## Acceptance criteria
- [ ] Matrix matches the rules above
- [ ] No accidental enables (Default-vs-custom-layer pairings should stay default)"

file_issue "chore(structure): add Assets folder skeleton with .gitkeep" \
  "v0.0-scaffolding" "type: chore,area: tooling,phase: 1" \
"Create the empty folder tree we'll fill across Phase 1, with \`.gitkeep\` files so git tracks them:

\`\`\`
Assets/Scripts/Gameplay/
Assets/Scripts/Util/
Assets/Scripts/Tests/EditMode/
Assets/Scripts/Networking/    ← must stay empty until Phase 2 (DESIGN §9 forbids pre-optimization)
Assets/Tilemaps/
Assets/Sprites/
Assets/Prefabs/
Assets/Audio/
Assets/Settings/Tuning/
\`\`\`

## Acceptance criteria
- [ ] All nine directories exist and are committed (via .gitkeep)
- [ ] \`Assets/Scripts/Networking/\` contains only \`.gitkeep\`
- [ ] No source files yet — only structure"

file_issue "chore(input): split InputSystem_Actions into Player1 and Player2 maps" \
  "v0.0-scaffolding" "type: chore,area: input,phase: 1" \
"Refactor \`Assets/InputSystem_Actions.inputactions\` so the default Unity template is replaced by two independent action maps — one per local input owner. Resolves DESIGN.md §12 Q6.

**Player1 map** — bindings:
- Move (1D composite): A (left) / D (right)
- Jump (button): Left Shift
- Dash (button): Left Ctrl

**Player2 map** — bindings:
- Move (1D composite): ← / →
- Jump (button): Space
- Dash (button): Right Shift

Each \`KeyboardInputSource_PX\` (added in v0.1) will \`Enable()\` only its own map so device-ownership is explicit. No key is shared across P1 and P2.

## Acceptance criteria
- [ ] Two action maps named exactly \`Player1\` and \`Player2\`
- [ ] Each has the three actions (Move, Jump, Dash) with bindings as above
- [ ] Default \`UI\` map removed (or kept empty if Unity won't allow deletion)
- [ ] No binding overlaps between P1 and P2 keys"

file_issue "chore(scene): rename SampleScene.unity to Bootstrap.unity via git mv" \
  "v0.0-scaffolding" "type: chore,area: tooling,phase: 1" \
"Rename the default scene from \`SampleScene.unity\` to \`Bootstrap.unity\`. **Use \`git mv\` on both the scene file and its \`.meta\`** to preserve the asset GUID:

\`\`\`bash
git mv Assets/Scenes/SampleScene.unity Assets/Scenes/Bootstrap.unity
git mv Assets/Scenes/SampleScene.unity.meta Assets/Scenes/Bootstrap.unity.meta
\`\`\`

Losing the GUID silently breaks every Inspector reference that points at this scene. Per DESIGN.md §10, Bootstrap is the entry scene holding persistent managers; level scenes load additively into it later.

## Acceptance criteria
- [ ] Scene file is named \`Bootstrap.unity\`
- [ ] \`.meta\` file's \`guid:\` value is unchanged (verify via \`git diff --no-index\` on the meta or by inspecting the rename in \`git log -p\`)
- [ ] Scene opens cleanly in Unity Editor"

file_issue "chore(build): set Bootstrap as scene 0 in build settings" \
  "v0.0-scaffolding" "type: chore,area: tooling,phase: 1" \
"Update \`ProjectSettings/EditorBuildSettings.asset\` so \`Assets/Scenes/Bootstrap.unity\` is at index 0 and is the only scene in the build list at this point. Level scenes will be added as they're authored (v0.3 onward).

Easiest path: File → Build Profiles → drag Bootstrap into the Scenes In Build list, confirm it's at index 0.

## Acceptance criteria
- [ ] \`Bootstrap.unity\` is at index 0
- [ ] No other scenes in the build list yet
- [ ] Pressing Play from any context loads Bootstrap"

# ============================================================================
# v0.1-movement — 11 issues
# ============================================================================
echo "» Issues — v0.1-movement"

file_issue "chore(scripts): add Util/Gameplay/Tests asmdefs" \
  "v0.1-movement" "type: chore,area: tooling,phase: 1" \
"Create three Assembly Definition (asmdef) files that establish compile-time boundaries:

- \`Assets/Scripts/Util/JumpNowBro.Util.asmdef\` — **zero references** (no UnityEngine, no UnityEditor). Pure C# only.
- \`Assets/Scripts/Gameplay/JumpNowBro.Gameplay.asmdef\` — references \`JumpNowBro.Util\` and \`Unity.InputSystem\`.
- \`Assets/Scripts/Tests/EditMode/JumpNowBro.Tests.asmdef\` — references \`JumpNowBro.Util\`, \`UnityEngine.TestRunner\`, \`UnityEditor.TestRunner\`. Set \`includePlatforms: [\"Editor\"]\` and \`defineConstraints: [\"UNITY_INCLUDE_TESTS\"]\`.

The Util-without-UnityEngine rule is load-bearing: Phase 2's wire serializer must convert types like \`ControlMap\` to bytes without ever calling Unity APIs. Splitting Util enforces that boundary at compile time, not by convention.

## Acceptance criteria
- [ ] All three asmdefs compile cleanly
- [ ] Util.asmdef has empty \`references\` array
- [ ] Adding \`using UnityEngine;\` to a Util file causes a compile error
- [ ] Test asmdef is Editor-only"

file_issue "feat(util): add PlayerAction, InputOwner, IInputSource" \
  "v0.1-movement" "type: feat,area: util,phase: 1" \
"Add three pure-C# types in \`Assets/Scripts/Util/\` (no \`using UnityEngine;\` allowed):

**\`PlayerAction.cs\`** — \`[Serializable] public enum PlayerAction { MoveHorizontal, Jump, Dash }\`

**\`InputOwner.cs\`** — \`[Serializable] public enum InputOwner { P1, P2 }\`

**\`IInputSource.cs\`** — interface that v0.1's KeyboardInputSource_PX and Phase 2's NetworkInputSource both implement:

\`\`\`csharp
public interface IInputSource {
    bool MoveLeft  { get; }
    bool MoveRight { get; }
    bool JumpPressed { get; }   // edge-triggered, valid for one tick
    bool JumpHeld    { get; }
    bool DashPressed { get; }   // edge-triggered
    void Tick();                // called once per FixedUpdate; consumes edge flags
}
\`\`\`

This is the seam that lets Phase 2 swap local keyboard input for network input without changing the PlayerController.

## Acceptance criteria
- [ ] All three types compile inside \`JumpNowBro.Util\`
- [ ] No \`using UnityEngine\` in any file
- [ ] Enums marked \`[Serializable]\` (Phase 2 will serialize them over the wire)"

file_issue "feat(util): add ControlMap struct with default and WithSwap" \
  "v0.1-movement" "type: feat,area: util,phase: 1" \
"Add \`Assets/Scripts/Util/ControlMap.cs\` — a \`[Serializable] public struct\` with three fields:

\`\`\`csharp
public InputOwner moveOwner;
public InputOwner jumpOwner;
public InputOwner dashOwner;
\`\`\`

Plus:
- \`public static ControlMap Default\` — all three owners = P1
- \`public static ControlMap WithSwap(ControlMap current, PlayerAction action)\` — pure function returning a new map with the named action's owner flipped (P1↔P2). Does NOT mutate the input.

Struct value semantics + static-pure swap means tests verify behavior without shared mutable state, and Phase 2 can serialize the map to ≤3 bytes for the SWAP EVENT payload.

## Acceptance criteria
- [ ] \`Default\` returns a map with all owners = P1
- [ ] \`WithSwap(default, Jump).jumpOwner == P2\` and \`.moveOwner == P1\`
- [ ] Calling WithSwap twice on the same action restores the original
- [ ] Lives in \`JumpNowBro.Util\`; no UnityEngine reference"

file_issue "feat(gameplay): add PlayerTuning ScriptableObject and default asset" \
  "v0.1-movement" "type: feat,area: player,phase: 1" \
"Add \`Assets/Scripts/Gameplay/PlayerTuning.cs\` — a \`ScriptableObject\` with \`[CreateAssetMenu(menuName = \"JumpNowBro/Player Tuning\")]\` and these public fields (defaults per DESIGN.md §4):

| Field | Default |
|---|---|
| runSpeed | 9 |
| jumpVelocity | 16 |
| gravity | 50 |
| coyoteTime | 0.1 |
| jumpBufferTime | 0.1 |
| dashDistance | 4 |
| dashDuration | 0.15 |
| dashFreezeFrameDuration | 0.05 |
| dashInvulnerabilityDuration | 0.15 |
| airControlMultiplier | 0.85 |
| variableJumpCutMultiplier | 0.5 |

Create the default asset at \`Assets/Settings/Tuning/PlayerTuning.asset\` (right-click in Project view → Create → JumpNowBro → Player Tuning).

**Why ScriptableObject, not serialized fields on the controller:** field edits on a Play-mode MonoBehaviour are silently discarded on Stop. ScriptableObject asset edits in the Project view survive Play-mode exit — this matters because DESIGN.md §4 says \"expect to iterate\" on these values.

## Acceptance criteria
- [ ] \`PlayerTuning.cs\` compiles with all 11 public fields
- [ ] Asset exists at \`Assets/Settings/Tuning/PlayerTuning.asset\` with default values
- [ ] Asset is referenceable from the Inspector"

file_issue "feat(gameplay): add KeyboardInputSource_P1 and KeyboardInputSource_P2" \
  "v0.1-movement" "type: feat,area: input,phase: 1" \
"Add two MonoBehaviours that implement \`IInputSource\` by wrapping the New Input System:

**\`KeyboardInputSource_P1.cs\`** — \`Enable()\`s the \`Player1\` action map only. Reads Move (composite axis), Jump (button), Dash (button) into \`IInputSource\` properties. Edge-triggered fields (\`JumpPressed\`, \`DashPressed\`) flip true on the InputAction's \`performed\` event and are cleared by \`Tick()\` (which the controller calls at the end of FixedUpdate).

**\`KeyboardInputSource_P2.cs\`** — identical structure, but enables the \`Player2\` action map.

Both live in \`Assets/Scripts/Gameplay/\`. Together they're the only place in the entire codebase that touches Unity's input APIs — every other consumer goes through \`IInputSource\`.

## Acceptance criteria
- [ ] Both classes implement \`IInputSource\`
- [ ] Each \`Enable()\`s only its own map (no cross-talk)
- [ ] Edge-triggered flags reset in \`Tick()\`
- [ ] No \`Input.GetKey\` or direct \`Keyboard.current\` access elsewhere in Gameplay"

file_issue "feat(player): add PlayerController with horizontal movement" \
  "v0.1-movement" "type: feat,area: player,phase: 1" \
"Add \`Assets/Scripts/Gameplay/PlayerController.cs\` — the single MonoBehaviour driving the character. At this commit, only horizontal movement and gravity exist.

Skeleton:

\`\`\`
PlayerController : MonoBehaviour
  [SerializeField] PlayerTuning tuning;
  Rigidbody2D rb;
  IInputSource p1, p2;
  MoveState state;                  // enum { Grounded, Jumping, Falling, Dashing }

  void Awake();                     // grab rb, default state to Falling
  void Update();                    // (no-op at this commit; later: timer decrements, edge detection)
  void FixedUpdate();               // read p1/p2.MoveLeft/MoveRight, apply velocity; ground check; gravity
  public void Inject(IInputSource p1, IInputSource p2);
\`\`\`

Rigidbody2D settings on the prefab (configured in the next-to-last issue): \`Body Type: Dynamic\`, \`Gravity Scale: 0\` (we apply gravity manually so it scales with PlayerTuning), \`Interpolation: Interpolate\`, \`Collision Detection: Continuous\`, \`Constraints: Freeze Rotation Z\`.

Movement: read \`(p1.MoveRight ? 1 : 0) - (p1.MoveLeft ? 1 : 0)\` (only P1 at this commit — ControlMap routing arrives in v0.2). Set \`rb.linearVelocity.x\` to \`signedInput * tuning.runSpeed\` with \`airControlMultiplier\` when not grounded. Apply gravity each FixedUpdate: \`rb.linearVelocity.y -= tuning.gravity * Time.fixedDeltaTime\`.

## Acceptance criteria
- [ ] Player runs at ±9 units/s on flat ground (P1's WASD)
- [ ] Falls at gravity 50 when airborne
- [ ] All physics writes are in \`FixedUpdate\`, not \`Update\`
- [ ] State enum present; values transition Grounded ↔ Falling correctly"

file_issue "feat(player): add jump with variable height cut on release" \
  "v0.1-movement" "type: feat,area: player,phase: 1" \
"Extend \`PlayerController\` with jump:

- **On grounded + JumpPressed**: set \`rb.linearVelocity.y = tuning.jumpVelocity\`, transition to \`Jumping\`.
- **Variable height**: while \`state == Jumping\` and \`rb.linearVelocity.y > 0\` and \`!JumpHeld\`: scale \`linearVelocity.y\` by \`tuning.variableJumpCutMultiplier\` (0.5) — once per release.
- **Jumping → Falling** when \`linearVelocity.y <= 0\`.
- **Falling → Grounded** when ground contact detected (raycast or 2D circle cast from feet).

Held jump should reach \`jumpVelocity²/(2·gravity) = 16²/100 = 2.56 units\`. Tapped jump (released within ~0.1s) should arc to roughly half that.

## Acceptance criteria
- [ ] Held jump apex ≈ 2.56 u
- [ ] Tapped (released during ascent) jump arcs visibly shorter
- [ ] State transitions Grounded → Jumping → Falling → Grounded cleanly
- [ ] No double-jump (single press only fires once per ground contact)"

file_issue "feat(player): add coyote time and jump buffer" \
  "v0.1-movement" "type: feat,area: player,phase: 1" \
"Add the two Celeste forgiveness windows that make jumps feel fair:

- **Coyote time**: when leaving Grounded (Grounded → Falling), start \`coyoteTimer = tuning.coyoteTime\` (0.1s). While \`coyoteTimer > 0\`, a Jump press still launches even though we're airborne.
- **Jump buffer**: when JumpPressed fires while airborne, set \`jumpBufferTimer = tuning.jumpBufferTime\` (0.1s). On the next Grounded landing, if \`jumpBufferTimer > 0\`, fire a jump immediately.

Both timers decrement in \`Update\` (frame-time, not fixed-time — feels more responsive).

**Extract the eligibility test** to \`Assets/Scripts/Util/InputForgiveness.cs\` so it's unit-testable in EditMode without spinning up Play mode:

\`\`\`csharp
public static class InputForgiveness {
    public static bool CanJump(float coyoteRemaining, float bufferRemaining,
                                bool isGrounded, bool jumpJustPressed)
        => (jumpJustPressed || bufferRemaining > 0f)
        && (isGrounded || coyoteRemaining > 0f);
}
\`\`\`

PlayerController calls \`InputForgiveness.CanJump(...)\` before launching.

## Acceptance criteria
- [ ] Walk off a ledge → press Jump within 0.1s → still launches
- [ ] Press Jump 0.1s before landing → fires automatically on touchdown
- [ ] \`InputForgiveness.cs\` lives in \`JumpNowBro.Util\` (no UnityEngine)
- [ ] No double-firing (consuming a buffered jump clears the timer)"

file_issue "feat(player): add horizontal dash with one-charge refund on landing" \
  "v0.1-movement" "type: feat,area: player,phase: 1" \
"Add the dash action. Horizontal-only at MVP (resolves DESIGN.md §12 Q1 — 8-way is parked post-MVP).

Behavior:
- **On DashPressed + \`dashChargeAvailable\`**: compute facing from last non-zero horizontal input; set \`rb.linearVelocity = (facing * tuning.dashDistance / tuning.dashDuration, 0)\` (yields ~26.7 u/s for the defaults); transition to \`Dashing\`; start \`dashTimer = tuning.dashDuration\` (0.15s); set \`dashChargeAvailable = false\`.
- **During Dashing**: skip gravity application; hold the dash velocity (don't let movement input override it).
- **Dashing → Falling** when \`dashTimer <= 0\`.
- **On Grounded entry**: \`dashChargeAvailable = true\` (refund).

The high peak velocity is why Rigidbody2D Collision Detection must be Continuous (else dashes tunnel through thin walls).

## Acceptance criteria
- [ ] Dash covers ~4 units in 0.15s
- [ ] One charge per ground contact (chain-dashing in air is impossible)
- [ ] Refunded on landing
- [ ] Dash works from Grounded, Jumping, and Falling; suppressed during Dashing
- [ ] Rigidbody2D Collision Detection set to Continuous (prevents tunneling)"

file_issue "feat(player): add dash freeze-frame and invulnerability window" \
  "v0.1-movement" "type: feat,area: player,phase: 1" \
"Polish the dash with two feel features:

**Freeze-frame.** On dash activation, briefly halt simulation for \`tuning.dashFreezeFrameDuration\` (0.05s ≈ 3 frames at 60fps). Implement via a controller-local boolean \`simulationPaused\` that gates physics writes in \`FixedUpdate\` — **do NOT use \`Time.timeScale = 0\`**. The global timescale approach freezes coroutines, breaks Phase 2 tick determinism, and infects every other component in the scene.

**Invulnerability.** When dash starts, set \`invulnTimer = tuning.dashInvulnerabilityDuration\` (0.15s). While \`invulnTimer > 0\`, ignore \`Hazard\` collisions (the Hazard component handles this by checking \`PlayerController.IsInvulnerable\` before calling \`Die()\`).

## Acceptance criteria
- [ ] Visible micro-pause at dash start (~3 frames)
- [ ] \`Time.timeScale\` is never touched anywhere
- [ ] Spike collisions during the 0.15s post-dash window don't kill the player
- [ ] Both behaviors driven entirely by PlayerTuning constants"

file_issue "chore(scene): wire Bootstrap test setup with Player prefab and floor box" \
  "v0.1-movement" "type: chore,area: tooling,phase: 1" \
"Populate \`Assets/Scenes/Bootstrap.unity\` with the minimum needed to verify movement feel:

1. **Main Camera** at (0, 0, -10), orthographic, size ~6.
2. **Floor** — empty GameObject at (0, -3, 0) with a BoxCollider2D (size 30×1, layer Ground).
3. **Player prefab** at \`Assets/Prefabs/Player.prefab\` with these components: Rigidbody2D (Dynamic, Gravity Scale 0, Interpolate, Continuous, Freeze Rotation Z), BoxCollider2D (size 1×2, layer Player), \`PlayerController\` (tuning slot → PlayerTuning.asset), \`KeyboardInputSource_P1\`, \`KeyboardInputSource_P2\`. Instance the prefab in the scene at (0, 0, 0).
4. **Manager GameObject** with a \`PlayerSpawner\` stub or a startup script that calls \`PlayerController.Inject(p1, p2)\` in Awake.

No level loading yet — that's v0.3.

## Acceptance criteria
- [ ] Scene contains Main Camera, Floor, Player, Manager
- [ ] Player prefab saved with all components wired
- [ ] Pressing Play renders the character standing on the floor, gravity holds it down
- [ ] WASD + LeftShift + LeftCtrl all respond"

# ============================================================================
# v0.2-controlmap — 6 issues
# ============================================================================
echo "» Issues — v0.2-controlmap"

file_issue "feat(gameplay): add ControlMapStore singleton with Apply choke point" \
  "v0.2-controlmap" "type: feat,area: gameplay,phase: 1" \
"Add \`Assets/Scripts/Gameplay/ControlMapStore.cs\` — the runtime owner of the current ControlMap. MonoBehaviour singleton attached to the Bootstrap scene's Manager GameObject.

\`\`\`csharp
public class ControlMapStore : MonoBehaviour {
    public static ControlMapStore Instance { get; private set; }
    public ControlMap Current { get; private set; } = ControlMap.Default;
    public event Action<ControlMap> OnChanged;

    void Awake() { Instance = this; DontDestroyOnLoad(gameObject); }
    public void Apply(ControlMap newMap) {
        Current = newMap;
        OnChanged?.Invoke(newMap);
    }
}
\`\`\`

\`Apply\` is the **single choke point** for all ControlMap mutations. Nothing else writes \`Current\` (setter is private). This matters for Phase 2: the host's \`Apply\` will additionally enqueue a reliable SWAP EVENT to the client — because every Phase 1 mutation already routes through \`Apply\`, no leakage to clean up later.

## Acceptance criteria
- [ ] Singleton pattern in place; survives scene loads
- [ ] \`Current\` setter is private
- [ ] \`OnChanged\` event fires every time \`Apply\` is called
- [ ] \`Apply\` is the only public mutation API"

file_issue "refactor(player): route input through ControlMap" \
  "v0.2-controlmap" "type: refactor,area: player,area: input,phase: 1" \
"Refactor \`PlayerController\` so it reads input through \`ControlMapStore.Instance.Current\` instead of hardcoding \`p1\` for everything.

Today (v0.1): \`PlayerController\` reads only \`p1.MoveLeft/MoveRight/JumpPressed/DashPressed\`.

After (v0.2): each FixedUpdate, look up the owner per action:

\`\`\`csharp
var map = ControlMapStore.Instance.Current;
var moveSrc = map.moveOwner == InputOwner.P1 ? p1 : p2;
var jumpSrc = map.jumpOwner == InputOwner.P1 ? p1 : p2;
var dashSrc = map.dashOwner == InputOwner.P1 ? p1 : p2;

// then read moveSrc.MoveLeft, jumpSrc.JumpPressed, dashSrc.DashPressed, etc.
\`\`\`

After this commit, BOTH keyboards drive the character based on the current ControlMap — but with the default map (all P1), behavior matches v0.1 exactly. Real swap behavior arrives with SwapTrigger (next issue).

## Acceptance criteria
- [ ] PlayerController no longer references \`p1\` directly for input reads
- [ ] With \`ControlMap.Default\`, behavior is identical to v0.1 (P1 controls everything)
- [ ] No regression in coyote/buffer/dash"

file_issue "feat(gameplay): add SwapTrigger component" \
  "v0.2-controlmap" "type: feat,area: gameplay,phase: 1" \
"Add \`Assets/Scripts/Gameplay/SwapTrigger.cs\` — a MonoBehaviour placed on a Collider2D set to \`isTrigger = true\`, on the SwapTrigger layer.

\`\`\`csharp
public class SwapTrigger : MonoBehaviour {
    [SerializeField] PlayerAction actionToSwap;
    bool fired;

    void OnTriggerEnter2D(Collider2D other) {
        if (fired) return;
        if (!other.TryGetComponent<PlayerController>(out _)) return;
        fired = true;
        var store = ControlMapStore.Instance;
        store.Apply(ControlMap.WithSwap(store.Current, actionToSwap));
    }
}
\`\`\`

The \`fired\` guard prevents re-entry (walking back through the trigger doesn't re-swap). One trigger = one swap event, ever.

## Acceptance criteria
- [ ] Walking through a SwapTrigger flips the owner of the configured action
- [ ] Re-entering the same trigger does nothing (guard works)
- [ ] Component is the ONLY caller of \`ControlMapStore.Apply\` in level scenes
- [ ] Inspector field \`actionToSwap\` is the only configuration"

file_issue "feat(hud): add 'who owns what' debug overlay" \
  "v0.2-controlmap" "type: feat,area: hud,phase: 1" \
"Add a minimal HUD that surfaces the current ControlMap, so it's obvious which keyboard owns which action.

Recommended: three TextMeshPro \`TMP_Text\` labels in a horizontal row at top-left of the Bootstrap UI canvas. A \`ControlMapDebugHud\` MonoBehaviour subscribes to \`ControlMapStore.OnChanged\` and updates the labels to read e.g.:

\`\`\`
P1: MOVE   P1: JUMP   P1: DASH
\`\`\`

After walking through a Jump-swap trigger:

\`\`\`
P1: MOVE   P2: JUMP   P1: DASH
\`\`\`

This is a debug HUD, not the final UI — \`v0.4-mvp\` replaces it with a polished two-icon strip. Live with the ugly three-label version for now.

## Acceptance criteria
- [ ] Three labels visible during Play
- [ ] Labels update immediately on every \`OnChanged\` event
- [ ] HUD survives between additive scene loads"

file_issue "test(util): add EditMode tests for ControlMap.WithSwap" \
  "v0.2-controlmap" "type: test,area: util,phase: 1" \
"Add \`Assets/Scripts/Tests/EditMode/ControlMapTests.cs\` with at least these test cases:

1. \`WithSwap_DefaultMap_FlipsOnlyJumpOwner_WhenActionIsJump\` — calling \`WithSwap(Default, Jump)\` changes \`jumpOwner\` to P2 but leaves \`moveOwner\` and \`dashOwner\` on P1.
2. \`WithSwap_DoubleSwap_RestoresOriginal\` — \`WithSwap(WithSwap(Default, Jump), Jump)\` equals \`Default\`.
3. \`WithSwap_DoesNotMutateInput\` — call \`WithSwap(map, Jump)\` then assert \`map\` itself is unchanged (struct value semantics).
4. \`Default_HasAllActionsOnP1\` — sanity test on \`ControlMap.Default\`.

Tests live in the \`JumpNowBro.Tests\` asmdef from v0.1. Run with Test Runner → EditMode.

## Acceptance criteria
- [ ] All four tests green
- [ ] Tests do not depend on Play mode, MonoBehaviour, or Unity's API surface beyond NUnit attributes"

file_issue "chore(scene): add SwapTrigger smoke scene" \
  "v0.2-controlmap" "type: chore,area: level,phase: 1" \
"Build a throwaway test scene at \`Assets/Scenes/_smoke_swap.unity\` (note leading underscore — by convention, scenes prefixed \`_\` are not added to Build Settings):

- Floor (same as Bootstrap's test floor)
- Player prefab instance
- Manager + ControlMapStore + ControlMapDebugHud
- Two SwapTrigger volumes placed in the corridor — first one set to swap Jump, second one set to swap Dash

Acts as a manual regression scene for the swap mechanic. Not part of the shipped build.

## Acceptance criteria
- [ ] Scene plays standalone (without going through Bootstrap)
- [ ] Walking through trigger 1 flips Jump ownership → debug HUD updates → Space now jumps, LeftShift doesn't
- [ ] Walking through trigger 2 flips Dash ownership → RightShift now dashes
- [ ] Scene is NOT in Build Settings"

# ============================================================================
# v0.3-level1 — 8 issues
# ============================================================================
echo "» Issues — v0.3-level1"

file_issue "feat(gameplay): add LevelManager singleton with additive scene flow" \
  "v0.3-level1" "type: feat,area: gameplay,phase: 1" \
"Add \`Assets/Scripts/Gameplay/LevelManager.cs\` — MonoBehaviour singleton on the Bootstrap scene's Manager GameObject.

Fields:
- \`[SerializeField] string[] levelSceneNames\` — ordered list (\`Level_01\`, \`Level_02\`, \`Level_03\`)
- \`int currentLevelIndex\`

API:
- \`void LoadFirst()\` — called from Bootstrap's Start, loads \`levelSceneNames[0]\` additively
- \`void LoadNext()\` — unloads current level, loads \`levelSceneNames[currentLevelIndex + 1]\` additively. If past the end, raises \`OnAllLevelsComplete\` event.
- \`event Action OnAllLevelsComplete\`

Loads must be additive (\`SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive)\`) so Bootstrap's persistent managers survive.

## Acceptance criteria
- [ ] Bootstrap calls \`LoadFirst()\` in Start
- [ ] \`LoadNext()\` unloads the current and loads the next additively
- [ ] End-of-list raises the event (handled by a complete screen in v0.4)
- [ ] Bootstrap GameObjects survive level transitions"

file_issue "feat(gameplay): add PlayerSpawner that wires input sources at scene load" \
  "v0.3-level1" "type: feat,area: gameplay,phase: 1" \
"Add \`Assets/Scripts/Gameplay/PlayerSpawner.cs\` — listens for Unity's \`SceneManager.sceneLoaded\` event and on each load:

1. Finds the loaded scene's \`PlayerSpawnPoint\` marker (a new component, see below)
2. Instantiates the Player prefab at that position
3. Calls \`playerController.Inject(p1, p2)\` with the two KeyboardInputSource components on the Manager GameObject (or instantiates them as the player's children — pick one and document)
4. Calls \`playerController.SetCheckpoint(spawnPoint.position)\`

Also add \`Assets/Scripts/Gameplay/PlayerSpawnPoint.cs\` — a one-line marker component (no fields, no methods) placed on an empty GameObject in each level scene at the desired spawn location.

## Acceptance criteria
- [ ] Loading a level scene auto-spawns the player at its PlayerSpawnPoint
- [ ] Spawned Player has both input sources wired
- [ ] Initial checkpoint = spawn position
- [ ] Old player (from previous level) is removed before respawn"

file_issue "feat(camera): add CameraFollow with deadzone and smooth damp" \
  "v0.3-level1" "type: feat,area: camera,phase: 1" \
"Add \`Assets/Scripts/Gameplay/CameraFollow.cs\` — attached to Main Camera in Bootstrap. ~40 lines, pixel-perfect deadzone follow. Resolves DESIGN.md §12 Q4 (deadzone over room-snap).

\`\`\`csharp
public class CameraFollow : MonoBehaviour {
    [SerializeField] Transform target;             // assigned by PlayerSpawner when player spawns
    [SerializeField] Vector2 deadzoneHalfExtents = new(2f, 1f);
    [SerializeField] float smoothTime = 0.15f;
    Vector3 velocity;

    void LateUpdate() {
        if (!target) return;
        Vector3 delta = target.position - transform.position;
        float dx = Mathf.Max(0, Mathf.Abs(delta.x) - deadzoneHalfExtents.x) * Mathf.Sign(delta.x);
        float dy = Mathf.Max(0, Mathf.Abs(delta.y) - deadzoneHalfExtents.y) * Mathf.Sign(delta.y);
        Vector3 desired = transform.position + new Vector3(dx, dy, 0);
        desired.z = transform.position.z;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
    }

    public void SetTarget(Transform t) => target = t;
}
\`\`\`

PlayerSpawner calls \`cameraFollow.SetTarget(player.transform)\` after instantiating the player.

## Acceptance criteria
- [ ] Camera follows the player with a visible deadzone window
- [ ] No jitter at any framerate (smooth-damp interpolates frame-rate-independently)
- [ ] No Cinemachine dependency added"

file_issue "feat(gameplay): add Checkpoint component" \
  "v0.3-level1" "type: feat,area: gameplay,phase: 1" \
"Add \`Assets/Scripts/Gameplay/Checkpoint.cs\` — MonoBehaviour on a trigger Collider2D, on the Checkpoint layer.

\`\`\`csharp
public class Checkpoint : MonoBehaviour {
    [SerializeField] Transform respawnPoint;       // child Transform — where the player respawns
    void OnTriggerEnter2D(Collider2D other) {
        if (other.TryGetComponent<PlayerController>(out var p))
            p.SetCheckpoint(respawnPoint.position);
    }
}
\`\`\`

PlayerController also needs a public \`SetCheckpoint(Vector2 pos)\` method that stores the position into a field. The actual respawn behavior arrives with v0.4's \`Die()\`.

The \`respawnPoint\` field allows offsetting the respawn from the trigger collider (so the player doesn't immediately re-trigger the same checkpoint).

## Acceptance criteria
- [ ] Walking through a Checkpoint updates the stored respawn position
- [ ] No respawn-trigger loop (re-entering the same checkpoint is a no-op effectively)
- [ ] PlayerController.SetCheckpoint is public"

file_issue "feat(gameplay): add LevelGoal component" \
  "v0.3-level1" "type: feat,area: gameplay,phase: 1" \
"Add \`Assets/Scripts/Gameplay/LevelGoal.cs\` — MonoBehaviour on a trigger Collider2D, on the LevelGoal layer.

\`\`\`csharp
public class LevelGoal : MonoBehaviour {
    bool fired;
    void OnTriggerEnter2D(Collider2D other) {
        if (fired) return;
        if (!other.TryGetComponent<PlayerController>(out _)) return;
        fired = true;
        LevelManager.Instance.LoadNext();
    }
}
\`\`\`

The \`fired\` guard prevents double-firing if the player straddles the trigger during the scene transition.

## Acceptance criteria
- [ ] Reaching the LevelGoal in Level_01 transitions to Level_02 (or fires OnAllLevelsComplete after Level_03)
- [ ] Single trigger fires only once
- [ ] LevelGoal is the only caller of LevelManager.LoadNext"

file_issue "chore(scene): wire Bootstrap with managers and main camera" \
  "v0.3-level1" "type: chore,area: tooling,phase: 1" \
"Populate \`Bootstrap.unity\` with the full persistent setup needed for v0.3:

- **Main Camera** with the \`CameraFollow\` component (no target yet — assigned at spawn).
- **UI Canvas** (Screen Space – Overlay) containing the \`ControlMapDebugHud\` from v0.2 and a placeholder for the death-counter HUD (v0.4 fills this).
- **Manager GameObject** with: \`GameManager\` (placeholder MonoBehaviour for now, used in v0.4 to glue things together), \`LevelManager\`, \`ControlMapStore\`, \`PlayerSpawner\`, \`KeyboardInputSource_P1\`, \`KeyboardInputSource_P2\`.
- Remove the v0.1 floor and inline Player — they live in level scenes now.

Bootstrap's Start: \`LevelManager.Instance.LoadFirst()\` → loads \`Level_01\` additively.

## Acceptance criteria
- [ ] Bootstrap has no gameplay geometry — only managers, camera, canvas
- [ ] Pressing Play loads Level_01 additively (verify in Hierarchy)
- [ ] All manager singletons survive level transitions (use \`DontDestroyOnLoad\` in their Awake)"

file_issue "feat(level): build Level_01 'Hello, Partner' tilemap and layout" \
  "v0.3-level1" "type: feat,area: level,phase: 1" \
"Build \`Assets/Scenes/Level_01.unity\` — the introductory level per DESIGN.md §6 'Hello, Partner'.

**Teaches.** Basic movement, the control split exists, swap triggers exist.

**Layout.** Mostly horizontal corridor with 2–3 platforms. **One** SwapTrigger about halfway through (recommend swapping \`MoveHorizontal\` — feels weird immediately, no death stakes since no hazards). No dash required, no spikes. LevelGoal at the right end.

Scene contents:
- Tilemap with \`Ground\` layer geometry (use Tilemap Extras → Rule Tile or hand-place sprite tiles)
- One Checkpoint volume in the middle, before the swap trigger
- One SwapTrigger volume, \`actionToSwap = MoveHorizontal\`
- Player spawn point at the left
- LevelGoal volume at the right
- Scene background (placeholder solid color is fine)

Add \`Level_01\` to Build Settings (this is also the next issue's job — fine to do here and let that one verify).

## Acceptance criteria
- [ ] Level loads additively from Bootstrap with no errors
- [ ] Player can complete the level without dying (no hazards)
- [ ] Swap trigger fires exactly once mid-corridor
- [ ] LevelGoal transitions out (currently to OnAllLevelsComplete since Level_02/03 don't exist yet)"

file_issue "chore(build): add Bootstrap and Level_01 to build settings" \
  "v0.3-level1" "type: chore,area: tooling,phase: 1" \
"Update \`ProjectSettings/EditorBuildSettings.asset\` so both \`Bootstrap.unity\` (index 0) and \`Level_01.unity\` (index 1) are included.

Levels 2 and 3 are added in v0.4 when their scene files exist.

## Acceptance criteria
- [ ] Bootstrap at index 0
- [ ] Level_01 at index 1
- [ ] A built standalone (File → Build) launches and completes Level_01"

# ============================================================================
# v0.4-mvp — 12 issues
# ============================================================================
echo "» Issues — v0.4-mvp"

file_issue "feat(gameplay): add Hazard component with instant kill" \
  "v0.4-mvp" "type: feat,area: gameplay,phase: 1" \
"Add \`Assets/Scripts/Gameplay/Hazard.cs\` — MonoBehaviour on a trigger Collider2D, on the Hazard layer (typically attached to spike tiles or hazard volumes).

\`\`\`csharp
public class Hazard : MonoBehaviour {
    void OnTriggerEnter2D(Collider2D other) {
        if (!other.TryGetComponent<PlayerController>(out var p)) return;
        if (p.IsInvulnerable) return;
        p.Die();
    }
}
\`\`\`

\`PlayerController.IsInvulnerable\` returns \`invulnTimer > 0\` (set during dash, from v0.1's dash-invuln issue). \`PlayerController.Die\` is implemented in the next issue.

## Acceptance criteria
- [ ] Touching a Hazard with no invuln calls \`Die()\`
- [ ] Touching a Hazard during a dash's invuln window does nothing
- [ ] Hazard is dumb — no level-flow logic, no death counter writes, just calls Die"

file_issue "feat(player): add Die() with respawn at last checkpoint" \
  "v0.4-mvp" "type: feat,area: player,phase: 1" \
"Implement \`PlayerController.Die()\`. The respawn sequence:

1. Disable input (set internal \`isDead\` flag that short-circuits FixedUpdate)
2. Trigger a screen flash via a UI overlay (white panel, fade from alpha 1 → 0 over ~0.2s)
3. Wait ~0.4s total
4. Use \`Rigidbody2D.MovePosition(checkpointPosition)\` to teleport (NOT \`transform.position =\`, which skips physics broadphase)
5. Zero \`rb.linearVelocity\`
6. Refund the dash charge (\`dashChargeAvailable = true\`)
7. Re-enable input

The teleport-via-MovePosition keeps OnTriggerEnter2D firing correctly on the next physics step.

## Acceptance criteria
- [ ] Player respawns at the last-touched Checkpoint within ~0.4s
- [ ] Velocity is zero on respawn (no fall-through after a death-on-spikes-mid-air)
- [ ] Dash charge refunded
- [ ] Screen flash visible during the respawn window
- [ ] No \`transform.position = ...\` writes"

file_issue "feat(player): track per-level death counter" \
  "v0.4-mvp" "type: feat,area: player,phase: 1" \
"Add a public death counter to \`PlayerController\` that increments on each \`Die()\` call. Reset on level load (LevelManager raises an event or the spawner just calls \`player.ResetDeathCount()\` after instantiation).

\`\`\`csharp
public int DeathCount { get; private set; }
public void ResetDeathCount() => DeathCount = 0;
// Die() body: DeathCount++; ...
\`\`\`

GameManager (or LevelManager) accumulates totals across levels for the v0.4 complete screen — exposed via \`GameManager.TotalDeaths\` reading per-level counts as they reset.

## Acceptance criteria
- [ ] DeathCount increments on every Die call
- [ ] DeathCount resets at the start of each level
- [ ] Cross-level total tracked somewhere accessible to the complete screen"

file_issue "feat(hud): add death counter and level-name overlay" \
  "v0.4-mvp" "type: feat,area: hud,phase: 1" \
"Add a HUD that shows the current level name (e.g. \"Hot Potato\") and per-level death count at top-right of the UI Canvas. Subscribes to \`PlayerController.OnDeath\` (raise an event in Die) and to level-load events.

Two TextMeshPro labels:
- \`Level: Hot Potato\` (top, smaller text)
- \`Deaths: 7\` (below, slightly larger)

Survives between levels (UI Canvas already lives on Bootstrap).

## Acceptance criteria
- [ ] Level name shown on every level load
- [ ] Death count updates immediately when the player dies
- [ ] HUD repositions/scales cleanly at common aspect ratios (16:9 baseline)"

file_issue "feat(level): build Level_02 'Hot Potato'" \
  "v0.4-mvp" "type: feat,area: level,phase: 1" \
"Build \`Assets/Scenes/Level_02.unity\` per DESIGN.md §6 'Hot Potato'.

**Teaches.** Dash, spikes, multiple swaps in a single level.

**Layout.** Vertical climb. **Three** SwapTriggers (swapping different actions to keep both players guessing). **One** spike gap that requires a dash to clear — placed AFTER a swap that puts Dash on the other player. **One** Checkpoint halfway up.

Scene contents:
- Vertical tilemap geometry on Ground/OneWayPlatform layers (use one-way platforms where the climb needs them)
- Spike prefab (Hazard on a 1×1 sprite, spike art placeholder fine)
- Three SwapTriggers (suggested swap order: Dash, Move, Jump)
- One Checkpoint at the midpoint
- One LevelGoal at the top
- PlayerSpawnPoint at the bottom

Add \`Level_02\` to Build Settings at index 2.

## Acceptance criteria
- [ ] Level plays from Level_01 LevelGoal → into Level_02
- [ ] At least one spike gap is genuinely unclearable without dash
- [ ] Spike contact kills and respawns at the midpoint checkpoint
- [ ] Three swap triggers fire in sequence"

file_issue "feat(level): build Level_03 'Trust Falls'" \
  "v0.4-mvp" "type: feat,area: level,phase: 1" \
"Build \`Assets/Scenes/Level_03.unity\` per DESIGN.md §6 'Trust Falls'.

**Teaches.** Full combination — sequenced swaps rotating who owns what across all three actions. Death-by-coordination-failure.

**Layout.** Short tower or cavern with 3–4 staged rooms. Each room: clean \"land here, swap fires, prepare\" beat, then the next obstacle. **Final room demands one swap in the air or just before a dash window** — this is the moment of peak coordination difficulty.

Recommendation for the swap sequence (designed to rotate ownership of all three actions across the level):
1. Swap Jump (P1 → P2)
2. Swap Move (P1 → P2)
3. Swap Dash (P1 → P2)
4. Optional fourth swap mid-air in the final room

Add \`Level_03\` to Build Settings at index 3.

## Acceptance criteria
- [ ] Each of the 3–4 rooms has the land/swap/prepare/obstacle beat
- [ ] Final room demands a swap during airtime
- [ ] All three actions get swapped at least once across the level
- [ ] LevelGoal at the end fires OnAllLevelsComplete"

file_issue "feat(polish): screen shake on death" \
  "v0.4-mvp" "type: polish,area: camera,phase: 1" \
"Add screen shake to \`CameraFollow\` (or a sibling \`CameraShake\` component) that fires on player death.

Suggested approach: a coroutine that runs for ~0.15s, jittering the camera offset by ±0.3 units in X and Y per frame, damping to zero. Fires on the same event PlayerController raises in \`Die()\`.

\`\`\`csharp
public void Shake(float duration = 0.15f, float magnitude = 0.3f);
\`\`\`

## Acceptance criteria
- [ ] Visible kick on every death
- [ ] Shake damps to zero (no permanent offset)
- [ ] Camera still tracks the player correctly during and after the shake"

file_issue "feat(polish): dash particles and trail" \
  "v0.4-mvp" "type: polish,area: player,phase: 1" \
"Add particle effects to the dash:

1. **Burst at start** — a Unity \`ParticleSystem\` child on the player, played once via \`particles.Play()\` at dash start. Short-lived (0.2s), faces opposite the dash direction (so it looks like dust kicking back).
2. **Trail during dash** — a \`TrailRenderer\` on the player, enabled only during \`Dashing\` state.

Both are visual-only — no gameplay implications.

## Acceptance criteria
- [ ] Dash start emits a visible particle burst
- [ ] A trail follows the player during the 0.15s dash window
- [ ] Trail disappears cleanly when dash ends
- [ ] No GC allocations per dash (use a single pooled ParticleSystem)"

file_issue "feat(polish): swap-trigger floor banner and audio sting" \
  "v0.4-mvp" "type: polish,area: gameplay,phase: 1" \
"Make SwapTriggers visible so the player can anticipate them. Resolves DESIGN.md §12 Q2.

Add to each SwapTrigger placement:
1. **Floor banner** — a sprite child rendered below the player layer, showing which action is about to swap (icon for Move/Jump/Dash). Renders as a flat colored stripe on the ground, 2–3 tiles wide.
2. **Audio sting** — an \`AudioSource\` child playing a short clip on \`OnTriggerEnter2D\`. Even a placeholder bell ping is enough for MVP.

The SwapTrigger component itself remains responsible only for the ControlMap.Apply call — visuals/audio are sibling components or children, kept separate.

## Acceptance criteria
- [ ] Approaching a swap trigger is visually obvious from ~3 tiles away
- [ ] Crossing the trigger plays an audio cue
- [ ] Three distinct visuals (Move/Jump/Dash) so the player knows what's about to swap"

file_issue "feat(polish): jump and land SFX" \
  "v0.4-mvp" "type: polish,area: player,phase: 1" \
"Add audio hooks to PlayerController for jump and landing:

- **Jump SFX** — play on the frame the jump fires (after \`InputForgiveness.CanJump\` returns true). One-shot AudioSource on the player.
- **Land SFX** — play on Falling → Grounded transition, but only if velocity.y was below some threshold (don't play on tiny micro-bounces from one-way platforms).

Placeholder \`.wav\` files in \`Assets/Audio/\` are fine for MVP. The point is presence, not polish.

## Acceptance criteria
- [ ] Audible cue on every jump (including coyote/buffered jumps)
- [ ] Audible cue on every meaningful landing
- [ ] No spam from rapid grounding/un-grounding states"

file_issue "feat(hud): level-complete summary screen" \
  "v0.4-mvp" "type: feat,area: hud,phase: 1" \
"Add a complete screen that shows when \`LevelManager.OnAllLevelsComplete\` fires (i.e. after Level_03's goal).

Screen contents:
- \"You did it.\" headline (or similar)
- Total death count across all three levels
- Per-level death breakdown (optional)
- A \"Restart\" button that reloads Bootstrap (or quits to it)

Lives in the Bootstrap UI Canvas as an initially-disabled panel. Enabled by a script that listens to LevelManager's event.

## Acceptance criteria
- [ ] Panel appears after Level_03 goal
- [ ] Total death count matches sum of per-level counts
- [ ] Restart button resets state and reloads Level_01"

file_issue "docs(readme): update controls and playable-build instructions" \
  "v0.4-mvp" "type: docs,area: tooling,phase: 1" \
"Update \`README.md\` to reflect the playable MVP:

1. **Controls section** — document the P1 (WASD + LeftShift + LeftCtrl) and P2 (arrows + Space + RightShift) mappings.
2. **Build instructions** — confirm the Unity version, what scene to open (Bootstrap), and how to make a standalone build (File → Build Profiles → build target).
3. **Status section** — bump from \"Phase 1 in progress\" to \"v0.4-mvp tagged — playable single-player vertical slice complete.\"
4. **Repo Layout** — line 38 currently references a project-level \`CLAUDE.md\` that doesn't exist. Either remove that bullet or replace with a pointer to \`CLAUDE.local.md\` (gitignored).

## Acceptance criteria
- [ ] Controls clearly documented
- [ ] Build instructions step-by-step
- [ ] Status section reflects v0.4-mvp
- [ ] No stale references to nonexistent files"

# --- Summary -----------------------------------------------------------------
echo ""
echo "==="
echo "Issue summary by milestone:"
for ms in v0.0-scaffolding v0.1-movement v0.2-controlmap v0.3-level1 v0.4-mvp; do
  echo ""
  echo "» $ms"
  gh issue list --milestone "$ms" --state all --limit 100 \
                --json number,title --jq '.[] | "  #\(.number)  \(.title)"'
done

echo ""
echo "Bootstrap complete."
