> Implements: Documents/design/audio.md §2 (ambience), §3 (diegetic SFX + occlusion), §4 (personal audio), §5 (music), §6 (alerts), §7 (volume categories)
> Touches systems: audio, area, electricity, health, stamina, main-hud, rounds-lobby, structural-destruction, furniture
> Status: in-progress (Phase 1 shipped; Phase 0, 2–5 pending)

# Audio foundation (Jul 2026)

Builds the audio domain [audio.md](../design/audio.md) formalizes — the system four other
design docs were already assuming (`area.md` §5 ambience, `main-hud.md` §5 heartbeat,
`stamina.md` §4 breathing, `virology.md` §5 symptom cue) without anyone specifying it.
Navigation map: [systems/audio.md](systems/audio.md) — split out of
[systems/chat-audio-screens.md](systems/chat-audio-screens.md) now that Phase 1 has shipped
(cold-start step 3, [SKILL.md](../SKILL.md)).

## What already exists (upstream legacy)

`Assets/Scripts/SS3D/Systems/Audio/` carries inherited, unwired audio code. This effort keeps the
good bones and replaces the parts that predate the fork's Area/electricity/health/occlusion systems:

| File | Verdict | Why |
|---|---|---|
| `AudioSubSystem.cs` | **Keep + extend** | Working networked SFX/music pool: `[Server] PlayAudioSource` → `[ObserversRpc] RpcPlayAudioSource` → pooled `AudioSource` with Unity 3D falloff (`minDistance`/`maxDistance`), clips via `Assets.Get<AudioClip>(AssetDatabases.Sounds, id)`. Server runs no pool (`#if !UNITY_SERVER`). This is the §3/§5 playback path — do not reinvent it. It has **no wall occlusion** — that is the Phase 1 seam. |
| `Boombox.cs` | **Keep** | Already the §5 diegetic music source: `MachinePowerConsumer`-gated jukebox playing Music through the pool at short range. |
| `NoisyCollision.cs` | **Keep** | A real §3 example (positional collision SFX, server-triggered). Gains occlusion for free once Phase 1 lands. |
| `ChangeMusicInteraction.cs`, `ListenerPosition.cs`, `AudioType.cs` | **Keep** | Music-swap interaction, listener transform holder, `Sfx`/`Music` enum. |
| `AmbienceHandler.cs` | **Replace (Phase 2)** | Legacy per-scene ambience with manual `_air`/`_windiness`/`_power` knobs (all `//TODO`), a global AudioMixer lowpass "muffle", and an animator. Not per-Area, not wired to `AreaSubSystem` or electricity. §2 replaces it with an Area-driven, crossfading, per-track power-gated controller. |

Ad-hoc `AudioSource` users outside the pool — `AirlockStateMachine`, `StructuralIntegrityPresenter`,
`BlastExplosionEffect`, `BikeHorn`, `VendingMachineController`, `FuelPowerGenerator` — are §3 sounds
that today bypass occlusion. Consolidate them onto `AudioSubSystem` as their phases come up (roster
is content, §10 / this doc's Deferred).

## Three governing decisions

1. **Occlusion and ambience are client-local, never server-authoritative.** A wall muffles a sound
   *for a given listener* — it depends on the local `AudioListener` position, so it cannot be baked
   into the server RPC (which only says "play clip X at position P"). The server stays the trigger
   authority; the muffle/attenuate and the ambience crossfade are per-client presentation, exactly
   like [screen-space-effects](2026-07_screen-space-effects.md) drives overlays from a synced
   `HealthSnapshot` client-side. The server pool guard (`#if !UNITY_SERVER`) already reflects this.
2. **Occlusion is the seventh consumer of `SS3D.Utils.LineOfSight`** (`audio.md` §1). Reuse
   `LineOfSight.HasLineOfSight(listener, source, occluderMask)` — the same helper Drop, combat cover,
   and comms occlusion use. Do **not** fork a private `Physics.Raycast` in audio (the
   [chat-audio-screens](systems/chat-audio-screens.md) pitfall already records this for comms).
3. **Personal audio (§4) and alerts (§6) mirror the snapshot-mapper pattern.** A client-local mapper
   on the local owner subscribes to a synced state (`HealthSnapshot`, stamina, alert-stack add) and
   drives a non-positional, occlusion-free 2D source. `HealthScreenEffectMapper` is the template.

## Phases

### Phase 0 — Foundation & mixer

- Establish four exposed mixer groups on `Assets/Content/Systems/Audio/MainMixer.mixer` matching §7's
  categories: **Ambience**, **SFX**, **Personal**, **Music** — each independently volume-adjustable.
  These become the settings surface in Phase 5; nothing here decides levels (§10 balancing).
- Route the `AudioSubSystem` SFX/Music pool prefabs (`SfxAudioSourcePrefab`, `MusicAudioSourcePrefab`)
  to the SFX / Music groups. Ambience and Personal controllers route to their own groups.
- Confirm the sound content path and `AssetDatabases.Sounds` clip lookup is the single addition point
  (aligns with the Addressables migration — do not add a second clip loader).

### Phase 1 — Diegetic SFX occlusion (§3) — highest leverage, do first — **shipped**

Delivers the design's core claim (real geometry occludes sound) and upgrades every existing pooled
SFX at once.

- ✅ `AudioSourceOcclusion` — a client-local component added at runtime to every pooled Sfx/Music
  `AudioSource` (`AudioSubSystem.CreateNewAudioSource`). While a positional source is playing, it
  throttle-samples (every 0.15s) `LineOfSight.HasLineOfSight(listener, source.transform.position,
  LayerMask.GetMask("Default"))` — the same occluder mask/layer convention Drop, combat LOS, and comms
  occlusion already use — and lerps an `AudioLowPassFilter` cutoff (22kHz ↔ 800Hz) plus a volume
  scale (1.0 ↔ 0.5) toward the occluded/clear target, so toggling reads as a muffle, not a pop.
  `AudioOcclusionState` holds the pure lerp math (unit-tested in `AudioOcclusionStateTests`,
  independent of any live scene). `PrepareForPlayback` resets the muffle state before every `Play()`
  so a reused pooled source doesn't inherit its previous clip's occlusion.
- ✅ Bounded raycast cost: sampling is throttled per-source, and sources beyond their own `maxDistance`
  skip the raycast/filter update entirely (Unity's own rolloff already silences them).
- ✅ `systems/audio.md` split out of `chat-audio-screens.md`, stamping the occlusion pitfalls.
- **Not yet done:** consolidating ad-hoc emitters (`AirlockStateMachine` door cycle,
  `BlastExplosionEffect`) onto `PlayAudioSource` so they gain occlusion — `NoisyCollision` already
  routes through the pool and got occlusion for free. Content-roster follow-up, not blocking.

### Phase 2 — Per-area ambience (§2)

- New **client-local** ambience controller: on the local player crossing an Area boundary — detected via
  the same one-tile lookup every Area consumer uses (`AreaSubSystem.TryGetAreaForTile` /
  `ITileQueryService.TryGetAreaId`) — crossfade two non-positional (2D) sources from the old track to the
  new. Track id reads `AreaRecord.AmbienceTrackId` (**field already exists** on `AreaRecord`).
- **Per-track power gating** (§2, "honestly power-dependent, not all of them"): a per-track "requires
  power" flag; when set, the hum stops if that area sheds. Clients already receive `AreaLightingState`
  (Normal/Emergency/Dark) via the BufferLast `RpcSyncAreaLighting` snapshot ([area](systems/area.md)) —
  gate power-dependent tracks off that signal (Dark ⇒ silence the hum), matching area.md §5's "lights out
  and hum off are the same event." Room-tone tracks with no implied machine are ungated.
- Retire `AmbienceHandler`; migrate the mixer-muffle behavior it owned (if kept at all) into the Phase 1
  per-source occlusion path — do **not** keep a global lowpass that muffles *everything*.
- Authoring of `AmbienceTrackId` per area (Map Editor / `AreaSubSystem.RenameArea`-style API) is thin;
  wire a minimal setter or defer to a content pass (§10 — which tracks, and which are power-dependent, is
  content).

### Phase 3 — Personal audio (§4)

- Client-local mapper on the local owner (pattern: `HealthScreenEffectMapper`):
  - **Heartbeat** — begins when brain function drops toward critical/dying; resumes on a successful defib;
    silenced entirely when brain function reaches zero (worked example C). Source `HealthSnapshot` on the
    owned body (`HumanHealthController` / `HealthSnapshot`), the same signal screen effects consume.
  - **Breathing** — heavier as stamina depletes (`stamina.md` §4), off `StaminaController`.
- Non-positional, no occlusion, **heard only by the affected player** (owner-only). Routes to the Personal
  mixer group. This is the reusable category virology's symptom cue (§4) later plugs into — build the seam,
  not the virology content.

### Phase 4 — Alert cues (§6)

- Short, restrained **one-shot** cue when a *new* chip is added to the alert stack (`AlertIconStack` /
  `MainHudSubSystem`) or a PDA notification — not a loop, debounced so a burst of chips is not a machine-gun.
  Personal tier (§4), owner-only, Personal mixer group. No new visual chrome (§7).

### Phase 5 — Music exception & volume settings (§5, §7)

- **Lobby/menu non-diegetic bed** — the one place a 2D score is allowed (`lobby.md` §1's existing meta
  carve-out): plays on lobby/menu scenes only, stops at round start. Enforce "no global in-round score" —
  in-round music is Boombox-sourced only (§5, already true).
- Bind four settings sliders to the Phase 0 mixer groups (Ambience / SFX / Personal / Music). Default
  values and mix balance are a balancing pass (§10), not decided here.

## Deferred (content / balancing, per audio.md §10)

- Full sound-effect roster per object/weapon/action (§10) — the per-emitter content pass.
- Exact volume levels, category mix, default slider values (§10).
- Music track roster / licensing (§10).
- Which specific ambience tracks are power-dependent (§2 — authored per-track).
- Any stealth/footstep mechanic — footsteps are ordinary §3 SFX; no stealth system exists to consume them.
- **Voice chat — explicitly out** (`comms.md` §10 territory, `audio.md` §10). Not extended here.

## Companion design edits (owner-only — do not make in this effort)

`audio.md` §9 lists design-doc cross-citations the **owner** should add (`area.md` §5, `main-hud.md` §5,
`stamina.md` §4, `death-cloning-respawn.md` §7, `virology.md` §5 each citing `audio.md`). Agents never edit
`Documents/design/`; noted here so the dependency is tracked, not actioned.
