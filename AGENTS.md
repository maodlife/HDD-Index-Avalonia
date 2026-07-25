# Repository Instructions

## Branch workflow

- Unless the user explicitly requests a different branch or workflow, make all changes directly on the existing `dev` branch.
- Do not create a task-specific branch for ordinary changes.
- After the changes are ready, commit them locally on `dev`.
- Do not automatically push `dev` to `origin` after committing. In the final response, report the commit hash and tell the user that the commit is ready and waiting for them to push to `origin`.
- Do not automatically open a merge request (pull request) from `dev` into `master` after each ordinary change. Keep accumulating changes on `dev` until the user explicitly requests a merge request or asks to prepare accumulated changes for `master`, such as for a release.
- An explicit instruction from the user overrides this default workflow.
