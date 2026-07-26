# Azure DevOps cold-snapshot data extraction

**Run this in a session that has live network access to a real Azure DevOps
(ADO) organization.**

## What to produce

Pull real data from a real Azure DevOps organization/project and freeze it
as a set of raw JSON files on disk — a durable local dataset that doesn't
require live ADO access to use again afterward. Gather, don't build, don't
analyze:

**Do not:**
- Modify any application source code, or assume there is a codebase to look
  at — there may not be one available in this session, and none is needed
- Write or run tests
- Interpret, summarize, or judge the data in any way — don't compute rates,
  don't classify anything as consistent or inconsistent, don't decide
  whether something "looks right." Capture exactly what the API returns and
  stop there.

It's fine — expected, even — to have to chase things down: try an endpoint,
get a 404 or an unexpected shape, look up the actual field name, try again.
That kind of investigation is in scope, and you should do as much of it as
you need. The line is between *investigating how to get the data* (in
scope) and *interpreting what the data means once you have it* (out of
scope — leave it alone).

## Scope: only the `{{TARGET_NAME}}` subset

The organization this runs against has hundreds of repos, pipelines, and
environments. Almost none of them are relevant. Filter down before doing
anything else:

1. List everything at the org/project level first — every repo, every
   pipeline, every environment — via the plain listing endpoints (no filters
   needed yet, e.g. `_apis/git/repositories`, `_apis/pipelines`,
   `_apis/distributedtask/environments`).
2. Keep only the ones whose **name** contains `{{TARGET_NAME}}`
   (case-insensitive substring match — don't assume it's a prefix or an
   exact match, and don't assume it's spelled with consistent casing
   everywhere).
3. Work from that filtered list for everything below. If the filtered list
   for any category (repos, pipelines, environments) comes back empty,
   that's worth surfacing immediately rather than quietly proceeding with
   zero targets in that category — it likely means the name pattern doesn't
   match what was assumed, not that there's genuinely nothing there.

This filtering step matters for cost as much as correctness: don't fetch
commits/work-items/runs for the other hundreds of repos "just in case" —
that's wasted API budget and wasted output nobody asked for.

## What you need before starting

- An Azure DevOps organization name (and project name, if `{{TARGET_NAME}}`
  doesn't span the whole org)
- A Personal Access Token (PAT) with at least: **Code (Read)**, **Work Items
  (Read)**, and read access to **Pipelines**/**Environments**. Ask for one
  scoped no wider than that if one doesn't already exist.

You do not need to hand-pick specific repo/pipeline/environment names ahead
of time — the name-filter above does that automatically. Only go back to ask
if the filter turns up nothing, or turns up so much that it's clearly
matching more than intended.

Never print the PAT back into any output file, log, or filename. Treat it
like any other credential — read it once, use it in-memory for the HTTP
calls, and don't let it end up captured in whatever gets saved to disk.

## Authentication

Every call uses HTTP Basic auth: empty username, the PAT as the password,
base64-encoded into the `Authorization` header:

```
Authorization: Basic base64(":" + PAT)
```

Append `api-version=7.1` (or `7.1-preview.1` where noted below — some
endpoints haven't graduated out of preview) as a query parameter on every
call.

This runs against **YAML pipelines with Environments + Checks** — not
classic Release Management. Target the endpoints below directly; no need to
detect which model is in use first.

## Do this in two phases, not one

Don't interleave discovery and bulk capture. Confirm every category of data
below is actually reachable *first*, across the whole `{{TARGET_NAME}}`
subset — then, and only then, spend the effort pulling the full data. The
reason: some of these endpoints (the approvals/checks ones especially) are
genuinely uncertain territory. If one of them turns out to be a dead end,
that needs to surface *before* time gets sunk into bulk-capturing everything
else, not after — a report that's 90% complete with one silent gap is worse
than a short pause up front to decide how to handle that gap on purpose.

### Phase 1 — confirm everything is reachable

For each category below, make **one minimal validation call** against a
single representative `{{TARGET_NAME}}`-matched target (not the full
paginated pull yet — just enough to confirm the call succeeds and the
response looks like what's expected). Build a checklist as you go:

| Category | Endpoint | Reachable? | Notes |
|---|---|---|---|
| Commits | `.../git/repositories/{repo}/commits` | | |
| PRs | `.../git/repositories/{repo}/pullrequests` | | |
| PR→work-item links | `.../pullRequests/{id}/workitems` | | |
| WIQL query | `POST .../wit/wiql` | | |
| Work item batch fetch | `.../wit/workitems?ids=...` | | |
| Work item revision history | `.../wit/workitems/{id}/updates` | | |
| Pipeline list | `.../pipelines` | | |
| Pipeline run detail | `.../pipelines/{id}/runs/{runId}` | | |
| Environment deployment records | `.../distributedtask/environments/{id}/environmentdeploymentrecords` | | |
| Environment check configurations | (unexplored — see below) | | |
| Approval-instance history | (unexplored — see below) | | |

For the two "unexplored" rows, this is exactly where chase-it-down
investigation applies: try the documented-sounding URLs, follow whatever the
ADO REST API reference says under "Checks," expect some trial-and-error.
Unlike the other rows, the exact REST shape for "who approved this specific
environment deployment" isn't something to assume from memory or
documentation alone — this surface has changed across API versions and is
still partly in preview. The goal of Phase 1 isn't a full capture of this
data yet — it's answering *can this be reached at all, and if so, from
where*.

**When Phase 1 is done, report the checklist before moving on.** If every
row is reachable, say so plainly and proceed to Phase 2. If anything failed
— a permission error, a 404 that couldn't be worked around, an endpoint that
doesn't exist for this ADO tier — stop and report exactly what failed and
what was tried, then wait: fix the PAT scope and retry that row, accept the
gap and proceed to Phase 2 without it, or investigate further before
continuing. Don't decide this unilaterally and don't quietly start Phase 2
with a known hole in it.

### Phase 2 — full capture

Once Phase 1 confirms (or a gap has been explicitly accepted for) every
category, pull the real data across the whole `{{TARGET_NAME}}`-matched set.

Organize the output as one raw JSON file per API response, named clearly
after the endpoint and any parameters (e.g.
`work-items-batch-ids-101-102-103.json`, `commits-repo-<name>.json`). Keep a
short `manifest.json` or `README.md` alongside them noting: which org/project
this came from, the date captured, which real endpoint produced each file,
and which Phase 1 categories (if any) were skipped or incomplete and why —
so a future reader can tell a stale snapshot from a fresh one, and knows
exactly what to re-run if the shape of any endpoint ever changes.

#### 1. Commits and PRs

- `GET .../git/repositories/{repo}/commits` for each matched repo — capture
  at least one page (50-100 commits is plenty) per repo
- `GET .../git/repositories/{repo}/pullrequests?searchCriteria.status=all`
- For a handful of the PRs returned above, also capture:
  `GET .../git/repositories/{repo}/pullRequests/{pullRequestId}/workitems`
  — this is the *native* work-item link (as opposed to a reference parsed
  out of free-text commit messages), and it's the reason this endpoint is
  worth capturing on its own rather than skipping straight to commit text.

#### 2. Work items and their history

- WIQL query to get a set of work item ids, e.g. everything changed in the
  last 90 days:
  `POST .../wit/wiql`
  body: `{"query": "SELECT [System.Id] FROM WorkItems WHERE [System.ChangedDate] >= '<date>' ORDER BY [System.ChangedDate] ASC"}`
  — if work items aren't independently taggable by `{{TARGET_NAME}}` the way
  repos/pipelines/environments are, scope this to whatever area path or
  iteration path the matched project actually uses; ask if it's not obvious
  from the WIQL results.
- Batch-fetch full field data for those ids:
  `GET .../wit/workitems?ids=<comma-separated>`
  — capture at least one work item of each distinct type in use (e.g.
  Product Backlog Item, Bug, Task) since field sets differ by type
- For several individual work item ids — especially any that have visibly
  changed status more than once — capture their full revision history:
  `GET .../wit/workitems/{id}/updates`
  This is the endpoint that reveals whether `System.State` changes show up
  as `{oldValue, newValue}` pairs the way documented, and whether a work
  item's very first revision (creation) is itself a captured state
  transition or something that has to be inferred some other way. Just
  capture the raw response — don't reason about what the history "means."
- If any work item has real content in its Acceptance Criteria field
  (commonly `Microsoft.VSTS.Common.AcceptanceCriteria`), make sure at least
  one such item is in the batch-fetch capture — its raw shape (plain text
  vs. HTML, empty vs. populated by default) varies by process template
  (Agile / Scrum / CMMI / Basic). Capture it as-is; don't decide whether it
  looks structured or not.

#### 3. Pipeline runs and environment deployments

- `GET .../pipelines/{pipelineId}/runs` for each matched pipeline, and, for
  one specific real run, `GET .../pipelines/{pipelineId}/runs/{runId}` —
  capture whatever identity fields it carries for who triggered the run
  (look for `createdBy`, `requestedFor`, or similar under the run resource —
  the exact field name is one of the things this capture exists to nail
  down, so record what's actually there rather than assuming a name).
- Environment deployment history:
  `GET .../distributedtask/environments/{environmentId}/environmentdeploymentrecords?api-version=7.1-preview.1`
  — capture it for each matched environment.
- Approvals/Checks — by Phase 2, Phase 1 has already established whether
  this is reachable and from where. Pull the full history using whatever URL
  was confirmed working. If this was accepted as a known gap during Phase 1,
  don't re-attempt it here — just note in the manifest that it was
  deliberately skipped, with the reason already on record from Phase 1.

## When done

Hand back: the Phase 1 checklist (what was reachable, what wasn't, and any
decisions made about gaps), the directory of captured JSON files from Phase
2, and the manifest describing what each file is and when it was captured.
That's the deliverable — raw data and an honest account of what was and
wasn't reachable, nothing interpreted, nothing else needs to happen in this
session.