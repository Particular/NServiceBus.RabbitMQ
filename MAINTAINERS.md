The current maintainers of this repo are [@adamralph](https://github.com/adamralph), [@bording](https://github.com/bording), and [@WojcikMike](https://github.com/WojcikMike).

The maintainers [watch](https://github.com/Particular/NServiceBus.RabbitMQ/watchers) this repo and undertake the following responsibilities:

- Ensure that the `develop` branch is always releasable. "Releasable" means that the software built from the latest commit can be released immediately simply by following the release procedure.
  - This does not imply that the latest commit should *always* be released, only that it *can* be, immediately after deciding to release.
- Release new versions of the software.
- Review and merge [pull requests](https://github.com/Particular/NServiceBus.RabbitMQ/pulls).
- Groom the [issue backlog](https://github.com/Particular/NServiceBus.RabbitMQ/issues), including the triage of new issues as soon as possible after they are created.
- Manage the repo settings (options, collaborators & teams, branches, etc.).

## Merging pull requests

- A pull request must be approved by two maintainers before it is merged.
  - A pull request created by a maintainer is implicitly approved by that maintainer.
  - Approval is given by submitting a review and choosing the **Approve** option
  - For some pull requests, it may be appropriate to require a third maintainer to give approval before the pull request is merged. This may be requested by either of the current approvers based on their assessment of factors such as the impact or risk of the changes.
- A pull request created by a maintainer must be merged by *another* maintainer. No "self-merges".
- The pull request must be made from a branch which is a straight line of commits from `develop`. There must be no merges in the branch history since the commit on `develop`.
  - The branch does not have to be based on the latest commit in `develop` but this is preferable, where practical.

## RabbitMQ.Client Updates

### Smoke testing

When a new version of RabbitMQ.Client is released, we smoke test it to gain some confidence that it hasn't broken anything relied upon by NServiceBus.RabbitMQ. Smoke testing is performed by updating to the new version of RabbitMQ.Client in the appropriate projects in the following places:

- [Samples](https://github.com/Particular/docs.particular.net/tree/master/samples/rabbitmq): run all samples after updating.
- [Snippets](https://github.com/Particular/docs.particular.net/tree/master/Snippets/Rabbit): compile the solution after updating.
- This repo. Use the branch (`master` or `support-x.y`) which is relevant to the new version of RabbitMQ.Client. Compile the solution and run all tests after updating.

### Breaking changes

In order to reduce the burden of backporting patches to many versions of the package, when a new version of RabbitMQ.Client is released with breaking changes, whenever possible, we will update to that version in a patch release (hotfix on `master`).
