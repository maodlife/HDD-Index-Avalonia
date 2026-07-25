# Repository Instructions

## Branch workflow

- Unless the user explicitly requests a different workflow, make every change on a new short-lived branch created from the latest `origin/master`.
- Never commit ordinary changes directly to `master`.
- Keep each branch and pull request focused on one cohesive task.
- Keep ordinary task branches independent. Do not base a new task on an unmerged task branch unless the user explicitly requests a stacked workflow.
- If the working tree contains existing changes, do not automatically stash, discard, or move them. Stop and ask the user how to proceed.

## Branch naming

- Name branches `<type>/<lowercase-kebab-case-description>`.
- Use an appropriate type such as `feature`, `fix`, `refactor`, `docs`, `test`, `chore`, or `ci`.
- Use a short English description. An existing GitHub issue number may be included but is not required.

## Commit workflow

- Organize work into meaningful atomic commits. Small tasks may use one commit; larger tasks may use multiple independently understandable commits.
- Each commit should ideally build and pass its relevant tests.
- Use English Conventional Commit titles such as `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `ci:`, or `chore:`.
- Do not retain temporary commits such as `WIP`, `fix typo`, or `try again` in the final pull request history.
- History rewriting is allowed only for commits created for the current task on its short-lived branch.
- Before the first push, those commits may be amended or interactively rebased.
- After a push, rewrite history only when the branch is known not to be shared, and use `git push --force-with-lease`, never `git push --force`.
- Never rewrite user-owned commits, shared branches, or `master`.

## Validation and pull requests

- Before creating a pull request, fetch `origin`, rebase the task branch onto the latest `origin/master`, resolve any conflicts, and rerun the relevant checks.
- For code, build, or dependency changes, run the checks corresponding to the repository CI workflow.
- For documentation-only or repository-process changes, run only the relevant checks.
- Do not create a Ready pull request when a relevant check fails because of the change.
- If a check cannot run because of an environment limitation, the pull request may still be created, but the missing check and reason must be disclosed.
- Push the task branch and create a Ready pull request targeting `master`.
- Use a Conventional Commit-style English pull request title. Include a concise summary and the validation actually performed; the pull request body may be written in Chinese.
- After creating the pull request, report its URL, branch, commits, and local validation results. Do not wait for remote CI unless the user explicitly requests it.
- Do not merge the pull request unless the user explicitly requests the merge.
- When explicitly asked to merge, use Rebase and merge only. Confirm required CI has passed, the branch has no conflicts, and review conversations are resolved.
- Never bypass repository rules or branch protection.

## Branch cleanup

- After a successful merge performed by the agent, confirm the merge, synchronize `master`, and delete only the corresponding merged local task branch.
- Rely on GitHub's automatic head-branch deletion for the remote branch.
- If the user merged the pull request, confirm that it was merged before cleaning up the local branch.
- Never delete an unmerged branch or a branch whose state is uncertain.

- An explicit instruction from the user overrides this default workflow.
