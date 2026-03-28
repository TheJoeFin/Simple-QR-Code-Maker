---
name: pdm
description: Interact with ProDuckMap's live data through its markdown API using the bin/pdm CLI. Use this skill whenever the user wants to browse, explore, list, view, create, update, or delete ProDuckMap resources — types, UI elements, story maps, activities, cards, or any other app data. Also trigger when the user asks about their data model, wants to understand entity relationships, or says things like "show me my types", "what UI elements do we have", "add a card to the story map", "look at the app data", "what's in the database", or "explore the model". If they want to see or change live app data without writing Rails code, this is the right skill. Do NOT trigger for code changes to the Rails app itself — only for interacting with the data the app manages.
allowed-tools: Bash(bin/pdm:*)
---

ProDuckMap has a self-documenting markdown API accessed through `bin/pdm`. Every response includes YAML frontmatter (metadata), a readable body (content and relationships), and an API footer (available mutations with exact parameter names). Follow links between resources to discover the data graph.

**IMPORTANT: Only use `bin/pdm` to interact with app data. Never use `bin/rails runner` or any other method. If something cannot be done via `bin/pdm`, that is a gap to report to the user — not a reason to fall back to Rails.**

## Installation

Download the CLI and make it executable:

```bash
curl -o bin/pdm http://127.0.0.1:3000/cli/pdm
chmod +x bin/pdm
```

Or install system-wide:

```bash
curl -o /usr/local/bin/pdm http://127.0.0.1:3000/cli/pdm
chmod +x /usr/local/bin/pdm
```

## Setup (once per session)

Run `bin/pdm auth status` to check if already logged in — this also displays the token's **name**, which you must use when leaving comments (see Comments section). If not logged in, ask the user to provide their API token and host, then run:

```bash
bin/pdm auth login   # prompts for host and token interactively
```

Or non-interactively if the user provides the values:

```bash
bin/pdm auth login <<EOF
http://127.0.0.1:3000
<token>
EOF
```

After login, run `bin/pdm auth status` again to get the token name.

## Reading Data

```bash
bin/pdm type list                  # List all types (paginated, 20/page)
bin/pdm type view <id>             # View a specific type (properties, relationships, usage)
bin/pdm ui-element list            # List all UI elements
bin/pdm ui-element view <id>       # View a UI element (fields, children, interactions)
bin/pdm story-map list             # List all story maps
bin/pdm story-map view <id>        # View a story map (activities, cards, comments)
bin/pdm persona list               # List all personas (shows ID and name)
bin/pdm persona view <id>          # View a specific persona
bin/pdm card view <sm_id> <act_id> <card_id>   # View a card (notes, position, comments)
```

For pagination or other nested resources, use `bin/pdm api`:

```bash
bin/pdm api GET /types.md?page=2   # Page 2 of types
```

## Comments

Story maps, activities, and cards all support comments. Comments appear in the `## Comments` section of each `.md` detail view (with author name and comment id).

```bash
bin/pdm comment add story-map <id> "<body>"   # Comment on a story map
bin/pdm comment add activity <id> "<body>"    # Comment on an activity
bin/pdm comment add card <id> "<body>"        # Comment on a specific card
bin/pdm comment delete <comment-id>           # Delete a comment (shown as id: in the list)
```

**Always read existing comments before adding one.** Every `.md` detail view includes a `## Comments` section — read it before posting. If the point you intend to make is already covered (by you or the user), skip it or add to that thread by referencing the existing comment id instead of creating a duplicate.

**Comment attribution**: Comments are attributed to the token used to authenticate — the token's name (shown by `bin/pdm auth status`), not the user's name. Write comment bodies in your own voice as Claude; never write as if the comment is coming from the user. The token handles authorship automatically.

### When to comment on individual cards

Card-level comments are the right tool when a specific card — rather than the whole activity — needs attention. Leave a comment on a card when:

- **The title is ambiguous or too brief** — it's unclear what the card actually represents or what work it implies
- **The card seems misplaced** — it belongs in a different activity, or its position within the activity doesn't reflect a logical sequence
- **The card needs more detail** — the title alone doesn't capture important nuance (edge cases, open questions, dependencies)
- **A card appears to be a duplicate** — same concept already covered elsewhere in the map

### Finding card IDs

Card IDs are visible in the activity listing — each card is shown as a markdown link and the ID is the last segment of the URL:

```
- [Card title](http://127.0.0.1:3000/story_maps/6/activities/13/cards/28.md)
                                                                                ^^
                                                                            card ID = 28
```

View and comment on a card:
```bash
bin/pdm card view <story_map_id> <activity_id> <card_id>   # View full card detail
bin/pdm comment add card <card_id> "<body>"                # Leave a comment on it
```

Example:
```bash
bin/pdm card view 6 13 28
bin/pdm comment add card 28 "Title is ambiguous — 'From searching the MSFT store' could mean the user found it or is still looking. Consider rewording."
```

The API footer in each detail view also lists the exact `POST /comments.json` and `DELETE /comments/:id` endpoints with required params.

## Writing Data

Read a resource first — its API footer documents exact endpoints and parameter names. Then use `bin/pdm api`:

```bash
# Create (POST to .json path with JSON body)
bin/pdm api POST /types.json -H "Content-Type: application/json" \
  -d '{"type": {"name": "Order", "details": "A customer order"}}'

# Update (PATCH)
bin/pdm api PATCH /types/5.json -H "Content-Type: application/json" \
  -d '{"type": {"name": "Updated Name"}}'

# Delete
bin/pdm api DELETE /types/5.json
```

Always confirm mutations with the user before executing them.

## Response Format

Every `.md` response has three sections:

**1. YAML Frontmatter** (between `---` markers) — Index pages include `page`, `pages`, `total`, and pagination links (`self`, `next`, `prev`). Detail pages include `self`, `type`, `id`, `index` (back to list), and `html` (web UI link).

**2. Markdown Body** — Resource name as H1, attributes in a `| Detail | Value |` table, related collections under H2 headings. Names are `[linked](url.md)` to their detail views — follow these to explore relationships.

**3. API Footer** — After a `---` rule, starts with `## API`. Lists available mutations as `` `METHOD url` — Description `` with parameter names below each. This is the source of truth for how to modify any resource.

## Assigning UI Elements

UI elements can be attached at two levels — **activity** and **card** — and the right kind of element differs at each level.

### Activity → View-level elements

An activity column represents *where* in the app the user is. Attach a **view or screen** UI element to an activity: something like "Home View", "First Run View", "Settings Page", "New Action View". These map to the major screens a user navigates through.

```bash
bin/pdm api PATCH /story_maps/6/activities/13.json \
  -H "Content-Type: application/json" \
  -d '{"story_map_activity": {"ui_element_id": 1069251492}}'   # e.g. "Home View"
```

### Card → Elemental/widget-level elements

A card represents a specific *interaction* the user performs. Attach an **atomic UI component** to a card — the actual control being interacted with. Examples:

- `Button` — a tap/click action
- `Dropdown` — selecting from a list
- `Radio button group` — choosing one of several options
- `Search input` — typing to filter or find
- `Tokenized textbox` — tagging or multi-value entry
- `Slider` — adjusting a continuous value
- `Toggle` — on/off switch
- `Checkbox` — a binary selection within a form

```bash
bin/pdm api PATCH /story_maps/6/activities/13/cards/28.json \
  -H "Content-Type: application/json" \
  -d '{"story_map_card": {"ui_element_id": <widget_element_id>}}'
```

### Finding UI element IDs

```bash
bin/pdm ui-element list            # Browse all UI elements
bin/pdm ui-element view <id>       # Inspect a specific element
```

If the right element doesn't exist yet, that's a gap to note — either leave a comment or flag it to the user. Don't assign a view-level element to a card or vice versa.

## Assigning Personas to Activity Columns

Use `bin/pdm persona list` to find persona IDs, then assign with the dedicated shorthand:

```bash
bin/pdm persona list                                      # Find persona IDs
bin/pdm activity set-persona <story_map_id> <activity_id> <persona_id>
```

Example — assign persona 5 to activity 13 in story map 6:
```bash
bin/pdm activity set-persona 6 13 5
```

Or use the generic API command:
```bash
bin/pdm api PATCH /story_maps/6/activities/13.json \
  -H "Content-Type: application/json" \
  -d '{"story_map_activity": {"persona_id": 5}}'
```

## Story Map Review / Brainstorm

When the user asks to **review**, **brainstorm**, or **have a conversation about** a story map, run through the following four phases in order. Work autonomously through what's obvious; surface questions for the user on anything that requires judgement.

### Phase 1 — Load & Understand

Fetch everything before touching anything:

```bash
bin/pdm auth status                              # confirm token name for comments
bin/pdm story-map view <id>                      # read description and activity list
bin/pdm ui-element list                          # available UI elements to assign
bin/pdm persona list                             # available personas to assign
# then fetch every activity in parallel:
bin/pdm api GET /story_maps/<id>/activities/<act_id>.md   # repeat for each activity
```

Read each activity's cards, existing comments, current persona, and current UI element before making any changes. Summarise what you found before proceeding.

### Phase 2 — Enrich Obvious Details

Fill in missing details only where the answer is unambiguous. If it's unclear, flag it in Phase 3 instead of guessing.

**Activities → assign a view-level UI element** (the screen where this activity takes place — "Home View", "Settings Page", "First Run View"):
```bash
bin/pdm api PATCH /story_maps/<sm>/activities/<act>.json \
  -H "Content-Type: application/json" \
  -d '{"story_map_activity": {"ui_element_id": <id>}}'
```

**Activities → assign a persona** when the content clearly belongs to one:
```bash
bin/pdm activity set-persona <sm_id> <act_id> <persona_id>
```

**Cards → assign a widget-level UI element** (the atomic control being used — button, dropdown, slider, search input, radio button group, toggle, tokenized textbox, checkbox):
```bash
bin/pdm api PATCH /story_maps/<sm>/activities/<act>/cards/<card>.json \
  -H "Content-Type: application/json" \
  -d '{"story_map_card": {"ui_element_id": <id>}}'
```

If a needed widget doesn't exist in the UI element list, note it in a comment rather than assigning the wrong thing.

### Phase 3 — Comment on Unclear or Problematic Items

Always read existing comments before adding one — never duplicate. Comment at the most specific level possible (card over activity when the issue is scoped to one card).

Leave a comment when:
- A card title is ambiguous, too terse, or implies a different activity
- An activity overlaps with or should be split from another
- The persona or ordering seems wrong
- An edge case, error state, or permission prompt is implied but uncaptured
- A UI element couldn't be assigned because the right element is missing from the system

### Phase 4 — Add Gap-Filling Cards

Add cards only for genuine missing interactions. Common gaps:
- **Empty state** — what does the user see before any data exists?
- **Error / failure state** — what happens when an action fails?
- **Confirmation / feedback** — is there acknowledgement after completing an action?
- **Cancel / back path** — can the user abandon mid-flow?
- **Permission prompt** — does this require a system permission the user must grant first?

```bash
bin/pdm api POST /story_maps/<sm>/activities/<act>/cards.json \
  -H "Content-Type: application/json" \
  -d '{"story_map_card": {"title": "<title>", "notes": "<why this gap matters>"}}'
```

### Phase 5 — Summary

Report back with:
1. **What was assigned** — UI elements and personas added; any that couldn't be filled because the right element is missing
2. **Comments left** — issues flagged and where
3. **Cards added** — gaps filled and why
4. **Open questions** — things left for the user to decide, phrased as direct questions

## How to Work

- **Browsing**: Start at an index, scan the table, follow links. Summarize findings — don't dump raw markdown.
- **Exploring relationships**: Detail pages link to related resources. Types link to their UI elements, UI elements link to children/parents, story maps link to activities and cards.
- **Paginating**: Check `total` in frontmatter. Use `bin/pdm api GET /path.md?page=N`.
- **Discovering**: When unsure what exists, start with `bin/pdm type list`, `bin/pdm ui-element list`, and `bin/pdm story-map list`.
- **Mutating**: Read the resource first, check the API footer for exact params, confirm with the user, then execute.

## Parallel Requests — Avoid Quoted `echo` Separators

**Never chain `bin/pdm` calls using `&& echo "---SEPARATOR---" &&`** — the quoted string in `echo` triggers a shell safety guardrail that interrupts execution and forces the user to approve every step, defeating the purpose of the `bin/pdm` allow-list.

Instead, make **multiple independent Bash tool calls in the same response** (Claude Code supports parallel tool calls):

```
# Good — two parallel Bash tool calls in one response:
bin/pdm ui-element list
bin/pdm type list

# Bad — chained with quoted echo (triggers safety guardrail):
bin/pdm ui-element list && echo "---NEXT---" && bin/pdm type list
```

This keeps discovery fast without requiring user intervention at every step.
