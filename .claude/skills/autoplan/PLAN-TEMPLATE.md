# Plan template

The plan follows `Documents/plans/` conventions ([Documents/SKILL.md](../../../Documents/SKILL.md) Plans): YAML frontmatter with `name`, `overview` and `todos`, then the body. Each todo `id` is a kebab-case slug, and its `content` names the lane it runs in.

## Body sections, in this order

1. **Implements**: the milestone slice and catalog row, the design sections, and an explicit "deviates from `<doc>` §N because …" for every deviation.
2. **Starting state**: what exists today, with file paths, in a few lines.
3. **Owner questions**: only the `taste` and `low-confidence` decisions. Each is a question, the recommendation the build follows, and the todo ids it touches. An empty list says "None".
4. **Proposed design rules**: rules no design doc states yet, each with its SS14 source and the design doc and section it would belong in. The owner promotes them; until then they're the plan's assumptions.
5. **Decision log**: one table, `# | Decision | Tag | Source`, for every answer from the self-grill, including the owner questions.
6. **Phases**: the todos grouped by **lane**:
   - **blind**: code, data, docs, and tests that run without a running game (EditMode). Buildable in a cloud session with no Unity.
   - **rig**: prefab and scene wiring (tier-B recipes, never hand-edited YAML), PlayMode, multiplayer (MPPM), scenarios and smoke, visual checks. Needs the Unity editor on the owner's machine.
   Order the phases so all blind work comes first and the rig visit is one batch.
7. **Done when**: the slice's acceptance scenario, broken into checks, each with its lane and test tier (EditMode, PlayMode, MPPM, scenario, hand test). A hand test is listed only when no automated tier can prove the behaviour.
8. **Risks**: up to five, each with what would show it early.

## Checks before you report

- Every decision-log row has a tag and a source; `SS14` rows cite a path.
- Every owner question names the todos it affects.
- Every todo has a lane, and no blind todo needs a running game.
- Every **Done when** check maps to at least one todo.
- The plan is linked from its milestone slice, and `python3 Tools/generate_roadmap.py --check` passes.
