# Architecture bridge documentation

This repository (`diegofernandes-dev/poc-teams-approval`) is the **architecture bridge** between:

- the implementation agent working against Azure DevOps;
- the external architect/reviewer;
- cross-project architecture discussions (Teams Approval Gateway POC + Backstage IDP GMUD).

It is **not** an application repository, mirror, or backup of the Backstage IDP.

## Repository authority

| Repository | Role |
|---|---|
| **ADO** `platform-devops-developer-portal` | Sole implementation source of truth — application code, plugins, tests, pipelines, app-config, operational runbooks |
| **GitHub** `poc-teams-approval` (this repo) | Architecture bridge — ADRs, normative UI contracts, implementation handoff, POC findings, review screenshots |
| **GitHub** `platform-devops-developer-portal` | **Deprecated accidental mirror** — do not use for development; archive after extraction |

## Intended workflow

```text
Architect
    |
    | architecture / review
    v
poc-teams-approval (this repo)
    |
    | implementation instruction
    v
ADO platform-devops-developer-portal
    |
    | code + tests
    v
Implementation
    |
    | concise implementation handoff
    v
poc-teams-approval
    |
    v
Architect review
```

## Directory map

| Path | Contents |
|---|---|
| [`adr/`](./adr/) | Normative architecture decisions (e.g. ADR-002 Backstage onramp) |
| [`backstage/`](./backstage/) | IDP current state + implementation progress handoffs |
| [`ui/`](./ui/) | Normative GMUD UI contracts and review screenshots |
| [`architect-decision-teams-approval-identity.md`](./architect-decision-teams-approval-identity.md) | Teams Approval Gateway identity decision brief |
| [`future-gmud-context-enrichment.md`](./future-gmud-context-enrichment.md) | Future GMUD context on Teams approval cards |
| [`hands-on-progress.md`](./hands-on-progress.md) | Approval Gateway POC execution log |

## Handoff contract

After every meaningful implementation checkpoint in ADO, update:

[`backstage/implementation-progress.md`](./backstage/implementation-progress.md)

The handoff must include: checkpoint name, ADO paths changed, architecture decisions, domain/model changes, tests executed, visual validation, deviations, unresolved questions, and proposed next slice.

It may contain small pseudocode or interface shapes. It must **not** reproduce proprietary source files.

## Source-of-truth rules

When implementation and bridge documentation disagree:

- **ADO source code** → what **is** implemented
- **Bridge ADRs and normative contracts** → what **should** be implemented

Report the difference as a **deviation**. Do not silently alter architecture documentation to justify existing code.

## What does NOT belong here

Do not copy from ADO:

- `plugins/`, `packages/`, `app-config*`
- `package.json`, `yarn.lock`
- Application tests, pipelines, implementation scripts
- Full application source trees
