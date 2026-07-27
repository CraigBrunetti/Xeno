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
  stop there. (Plain literal-string matching against raw text — e.g.
  recording every place a specific word appears — is still just capture,
  not interpretation, and is in scope; the line is judgment about what a
  match *means*, not the mechanical act of finding it.)

It's fine — expected, even — to have to chase things down: try an endpoint,
get a 404 or an unexpected shape, look up the actual field name, try again.
That kind of investigation is in scope, and you should do as much of it as
you need. The line is between *investigating how to get the data* (in
scope) and *interpreting what the data means once you have it* (out of
scope — leave it alone).

**Capture everything, not a sample.** For every endpoint that returns a
list — commits, PRs, work items, pipeline runs, environment deployment
records, anything else — pull the complete result set by paging through it
to the end, not just a first page or a "representative" handful. This
session may be the only shot at live access, so the priority is getting the
full raw data down while it's reachable; narrowing or filtering it down to
what's actually needed can happen later, offline, against the captured
files. A partial capture can't be turned back into a complete one after the
fact, so default to pulling everything even where a smaller sample would
technically answer today's question.

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

## Implementation notes

- Write this as Python. Whatever HTTP client is on hand is fine (`requests`,
  `httpx`, etc.) — just keep the PAT handling described above: read it once,
  keep it in memory, never let it land in a file, log line, or filename.
- Parallelize the ADO calls wherever the work is actually independent — e.g.
  fetching commits/PRs across many repos at once, fetching diff content for
  many PRs at once, fetching revision history across many work item ids at
  once. These have no ordering dependency between them, so there's no reason
  to run them one at a time; given how much this document now asks to
  capture in full, serial calls would make this take far longer than it
  needs to.
- ADO's REST API does rate-limit (expect occasional 429 responses, some with
  a `Retry-After` header). Parallelize aggressively, but back off and retry
  the specific call that got throttled rather than letting one 429 abort
  the whole run — losing a big parallel batch to a single throttled request
  would defeat the point of parallelizing in the first place.

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
response looks like what's expected). Where the category returns a list,
also note how it paginates (continuation token, `$top`/`$skip`, a
`nextLink` — whatever the real response actually carries): Phase 2 pulls
every one of these categories to completion, not a representative sample,
so knowing the pagination shape up front avoids re-discovering it
mid-capture. Build a checklist as you go:

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
| Run's built commit (under the same run-detail response — confirm the field exists, don't assume the path) | `.../pipelines/{id}/runs/{runId}` | | |
| Environment deployment records (confirm the pagination mechanism here too — this one needs to be pulled exhaustively in Phase 2, not sampled) | `.../distributedtask/environments/{id}/environmentdeploymentrecords` | | |
| PR iterations | `.../pullRequests/{id}/iterations` | | |
| PR iteration diff/changes | `.../pullRequests/{id}/iterations/{iterationId}/changes` | | |
| PR file content fetch | (unexplored — see below) | | |
| Work item comments | `.../wit/workItems/{id}/comments` | | |
| Commit-range diff between two commits | (unexplored — see below) | | |
| Environment check configurations | (unexplored — see below) | | |
| Approval-instance history | (unexplored — see below) | | |

For the three "unexplored" rows, this is exactly where chase-it-down
investigation applies: try the documented-sounding URLs, follow whatever the
ADO REST API reference says, expect some trial-and-error. Unlike the other
rows, the exact REST shape isn't something to assume from memory or
documentation alone:

- **Approvals/Checks and environment check configurations** — this surface
  has changed across API versions and is still partly in preview.
- **PR file content fetch** — the PR iteration/changes endpoints above give
  you *which* files changed and an object identifier for each, but not the
  file's actual text. Try fetching the blob directly by that identifier
  (something like `.../repositories/{repositoryId}/blobs/{objectId}` with
  `$format=text` or an `Accept: text/plain` header), and if that doesn't pan
  out, try the item-content endpoint instead
  (`.../repositories/{repositoryId}/items?path=<path>&versionDescriptor.version=<commit>&includeContent=true`).
  Confirm which one actually works against a real response before
  committing to it for Phase 2.
- **Commit-range diff between two commits** — needed to get the exact set
  of commits between one deployment and the next to the same environment.
  Try the commits-batch endpoint:
  `POST .../repositories/{repositoryId}/commitsBatch?api-version=7.1`
  body: `{"itemVersion": {"versionType": "commit", "version": "<newer-sha>"}, "compareVersion": {"versionType": "commit", "version": "<older-sha>"}}`
  — this should return the commits reachable from the newer SHA but not the
  older one, i.e. exactly what landed between the two. Confirm the
  request/response shape against a real call before relying on it; if it
  doesn't work as documented, the GET-based
  `.../commits?searchCriteria.itemVersion.version=...&searchCriteria.compareVersion.version=...`
  form is the fallback to try.

The goal of Phase 1 isn't a full capture of this data yet — it's answering
*can this be reached at all, and if so, from where*.

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

**Pause between categories — this should not run unattended end to end.**
Phase 1 was one atomic check-then-report step; Phase 2 isn't meant to be a
single atomic run of everything below. Before starting each of the three
numbered categories, first do a quick count-only pass — how many
repos/PRs/work items/pipelines/runs actually matched — and report that
count before running the real capture for that category. After the
category's capture finishes, report what actually got pulled (counts, any
errors, anything that behaved differently than Phase 1 suggested) and pause
again before starting the next one. The volume some of these categories
reach isn't knowable in advance — a `{{TARGET_NAME}}`-filtered set can still
mean thousands of PRs, and pulling full diff content plus a TODO/FIXME scan
for every file in every one of them is exactly the kind of thing that can
quietly turn into a very long, very expensive run if it's kicked off blind.
A short pause per category costs little and gives a chance to notice "that's
way more than expected" before time/API budget is sunk into finishing it.

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
  the **full commit history**, not a page. This endpoint paginates via
  `searchCriteria.$top`/`$skip` (confirm the exact mechanism against a real
  response, same as everywhere else in this document); keep paging until an
  empty/short page comes back, not just until the first page looks
  reasonable.
- `GET .../git/repositories/{repo}/pullrequests?searchCriteria.status=all`
  — same pagination requirement: page through the full result set for every
  matched repo, not just the first batch.
- For **every** PR returned above (not a sample), also capture:
  `GET .../git/repositories/{repo}/pullRequests/{pullRequestId}/workitems`
  — this is the *native* work-item link (as opposed to a reference parsed
  out of free-text commit messages), and it's the reason this endpoint is
  worth capturing on its own rather than skipping straight to commit text.
- Before starting the diff-content/TODO-FIXME step below, report the count
  of PRs about to be walked (across all matched repos) — this is one of the
  two steepest volume multipliers in this document (full file content for
  every changed file in every PR), so it's worth a specific pause here even
  within this category.
- **For every PR, also capture its diff content:**
  1. `GET .../pullRequests/{pullRequestId}/iterations` — list of iterations;
     use the latest one (the final diff as merged/closed).
  2. `GET .../pullRequests/{pullRequestId}/iterations/{iterationId}/changes`
     — the changed files (add/edit/delete) for that iteration, each with an
     object identifier for its content.
  3. For each changed file, fetch its raw content using whichever endpoint
     Phase 1 confirmed works (see the PR file content fetch row above), and
     save it as its own output file.
- **Also capture every line, across those files, that contains the literal
  substring `TODO` or `FIXME` (case-insensitive)**, with a couple of lines
  of surrounding context — plain string matching only, no judgment about
  what a match means or whether it matters. Save these as a simple list
  (file, line number, matched line and its neighbors) alongside the file
  captures, so they don't have to be re-derived from the full file dump
  later. These are the two terms asked for; since the full file content is
  captured regardless, scanning for other markers later (`HACK`, `XXX`,
  etc.) is just a rerun of this same string match against data already on
  disk, not a new live capture.

#### 2. Work items and their history

- WIQL query to get **every** work item id in scope — don't time-window it:
  `POST .../wit/wiql`
  body: `{"query": "SELECT [System.Id] FROM WorkItems WHERE [System.AreaPath] UNDER '<area path>' ORDER BY [System.ChangedDate] ASC"}`
  — if work items aren't independently taggable by `{{TARGET_NAME}}` the way
  repos/pipelines/environments are, scope this to whatever area path or
  iteration path the matched project actually uses; ask if it's not obvious
  from the WIQL results. Only fall back to a date-bounded query (e.g. last
  90 days) if the full result set turns out to be too large to fetch in
  this session — and if it comes to that, record the cutoff actually used
  in the manifest rather than silently narrowing the capture.
- Batch-fetch full field data for **every** id returned above (the batch
  endpoint caps how many ids it accepts per call — page through
  `ids=<comma-separated>` in chunks and combine the results):
  `GET .../wit/workitems?ids=<comma-separated>`
  While going through them, confirm at least one work item of each distinct
  type in use (e.g. Product Backlog Item, Bug, Task) actually got captured,
  since field sets differ by type — but the fetch itself covers all ids,
  not a representative subset.
- For **every** work item id from the batch above (not just the ones that
  visibly changed status more than once), capture full revision history:
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
- `GET .../wit/workItems/{id}/comments` for **every** work item id from the
  batch above — comments are a separate endpoint from the field data and
  revision history captured above. Capture the full raw response for each.
- **Also capture every literal occurrence of `TODO` or `FIXME`
  (case-insensitive) found in a work item's title, description, or
  comments** — same plain string-matching rule as the PR diff capture:
  record where the match was found (which field or which comment, on which
  work item id), not what it might mean.

#### 3. Pipeline runs and environment deployments

- `GET .../pipelines/{pipelineId}/runs` for each matched pipeline — the
  **full run history**, paginated exhaustively (same rule as everywhere
  else: confirm the real pagination mechanism, don't assume a page size).
- For **every** run returned above (not one representative run),
  `GET .../pipelines/{pipelineId}/runs/{runId}` — capture whatever identity
  fields it carries for who triggered the run (look for `createdBy`,
  `requestedFor`, or similar under the run resource — the exact field name
  is one of the things this capture exists to nail down, so record what's
  actually there rather than assuming a name).
- **Also capture which commit each run actually built from.** Look for a
  commit SHA or ref under the run's `resources` (commonly something like
  `resources.repositories.self.version`) — don't assume that exact path,
  confirm it against the real response. This matters independently of the
  triggering-identity question above: without knowing which commit a given
  run shipped, there's no way to later cross-reference "was this specific
  commit (and whatever it's linked to) actually part of what this run
  deployed" — record it plainly even if its purpose isn't obvious from this
  document alone.
- **Environment deployment history — capture this exhaustively, not as a
  sample.** Unlike commits or PR lists (where a page or two is enough),
  every deployment record for each matched environment needs to be pulled,
  following whatever pagination mechanism the live response actually uses
  (check for a continuation token or `$top`/`$skip` — don't assume which one
  applies without confirming against a real response, same as everything
  else marked unconfirmed in this document). The reason exhaustiveness
  matters here specifically: a later phase may need to identify *pairs* of
  related deployments to the same environment (e.g., one deployment followed
  some time later by another one to the same target) — a partial capture
  could easily contain one half of such a pair and silently omit the other,
  which defeats the purpose more than a partial capture would for almost any
  other endpoint in this document.
  `GET .../distributedtask/environments/{environmentId}/environmentdeploymentrecords?api-version=7.1-preview.1`
- Before starting the commit-range-diff step below, report the count of
  deployment pairs about to be walked across all matched environments —
  this is the other steep volume multiplier in this document, and
  compounds on top of the exhaustive deployment-history pull just above it.
- **For every consecutive pair of deployments to the same environment**
  (ordered by time, using the environment deployment history above and each
  deployment's associated run's built commit from the run-detail capture),
  **capture the full commit-range diff between the earlier deployment's
  commit and the later one's**, using whichever endpoint Phase 1 confirmed
  works. Do this as a direct live call rather than inferring the list
  afterward from the separately-captured full commit history: a real git
  history isn't guaranteed to be linear (merges, cherry-picks,
  force-pushes, and out-of-order commit dates all happen), so filtering the
  full commit list by timestamp between two points can silently include
  commits that were never actually part of that range, or miss ones that
  were. The compare endpoint walks the actual commit graph, so it's the
  authoritative source for what landed between two deployments — worth the
  extra calls rather than trusting a derived answer. Save one output file
  per deployment pair per environment, named clearly (e.g.
  `commit-range-env-<name>-<olderSha>-<newerSha>.json`). No separate live
  call is needed to get the PRs behind that range: the full PR list per
  repo is already captured above and each PR's raw payload carries its
  merge commit, so matching the diffed commit SHAs against already-captured
  PR data can happen later, offline, against the files already on disk.
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