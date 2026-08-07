# Contributing to Microsoft Foundry Samples

> [!IMPORTANT]
> **Transition draft:** These direct-public instructions describe the target contribution path after the public-first cutover. Until this draft pull request is merged, the instructions on the `main` branch remain current. Maintainers must remove this notice only after the cutover gates in the pull request are proven.

This repository contains official Microsoft Foundry documentation samples. Changes are submitted as pull requests directly to this repository.

## Reporting Issues

If you find a bug, have a question, or want to suggest an improvement to an existing sample, please [open an issue](https://github.com/microsoft-foundry/foundry-samples/issues/new) on this repository. We welcome feedback from everyone!

Before starting a substantial change, check for an existing issue. Open one when discussion or design agreement would help avoid duplicate work.

## Contributing Changes

1. **Create a branch.** Contributors with write access can create a branch in this repository. Other contributors should fork the repository and create a branch in their fork.
2. **Make a focused change.** Keep each pull request scoped to one sample, fix, or related set of updates. Follow the conventions in the surrounding sample.
3. **Respect file ownership.** Review [CODEOWNERS](.github/CODEOWNERS) before editing. The listed owners will be requested when their files are changed.
4. **Validate locally.** Run the setup, build, test, or sample-specific validation documented by the affected sample. Never commit credentials, local environment files, or generated secrets.
5. **Open a pull request against `main`.** Explain what changed, why it changed, and the validation you ran. Link the relevant issue when one exists.

### Pull request checks

Pull requests run repository validation automatically:

- Supported changed samples under `samples/` are detected and validated to the repository's supported build-readiness level.
- Pull requests from forks run without repository credentials. The required `trusted` check runs and fails until a maintainer promotes the exact head commit to a same-repository branch for trusted validation.
- Documentation-only pull requests from same-repository branches satisfy the required check without running sample validation.

Do not add credentials to a pull request or ask for secrets to be exposed to a fork. A maintainer will handle trusted validation when promotion is required.

### Temporary workflow-dependent contributions

Direct public pull requests are the normal contribution path. During the transition, a maintainer may direct a Microsoft contributor to temporary staging for a workflow-dependent change. Use that path only when explicitly requested.

If temporary promotion fails, maintainers use an exact manual public pull request as the fallback. Contributors should not create duplicate publication attempts or alternate versions of the change.

## Contributor License Agreement

This project requires a Contributor License Agreement (CLA). When you submit a pull request, a CLA bot will check whether you need to sign one and guide you through the process. You only need to do this once across all Microsoft repos. For details, visit <https://cla.opensource.microsoft.com>.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information, see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com).
