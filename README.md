# dfs

[Coverage](https://app.codecov.io/gh/pppss102/dfs/tree/main)

## Development setup

Prerequisites

- Visual Studio 2022 with `ASP.NET and web development` and `.NET desktop development` workloads
- Node.js for UI (v22.x; works with v22.14.0)
- Python 3 for pre-commit

Preparation for build

1. .NET dependencies should've been installed with VS2022.
1. Run `pip install pre-commit` and `pre-commit install` to set up pre-commit hooks (mostly formatting).
   - If cmd can't find pre-commit, double-check if necessary Python package directories were added to PATH;
   - pre-commit will cause Git errors if it automatically reformats/modifies files; just ignore the reformat errors and commit again;
1. In the `dfs/electron` directory, run `npm install` to install required JS packages.
1. Run `dotnet restore dfs/dfs.sln` to install required .NET packages.

Now you should be able to run all tests (from VS's Test > Test Explorer menu) and projects (for now, UI isn't bundled with the client, and is run from a dev server).

## Debugging

Good luck (launch node & tracker from VS and launch electron forge from VS Code)
