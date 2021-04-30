## Features
- Versioning done with GitVersion.
- Can build via:
  - AppVeyor
  - Azure Devops Pipelines
  - GitHub Actions
 
- All projects will be SourceLinked to github thanks to `directory.props` file.
- Version numbering produced via GitVersion
- dotnet-format checks formatting errors
  - If building via GitHub Actions, the workflow will auto fix and push formatting fixes without failing the build.

# [Getting Started]
- Clone this repo, then push to your own origin.
- Create your solution (.sln) and projects in the `/src` directory.
- Make sure global.json has the right version of the .net sdk that you require.
- If you want to run the `dotnet-format` and `gitversion` tools (that are used as part of the CI builds) locally, then install them by running the following command in the repo root directory:
    `dotnet tool restore`
- For AppVeyor builds, update AppVeyor.yml:
    - dotnet sdk version (currently set to install latest pre-release).
    - Now you can add to AppVeyor.
- For Azure Devops builds:
    - Import pipelines yaml file into Azure Devops pipeline.
- For GitHub Actions - the workflow file is detected automatically when you push up and should be run.
