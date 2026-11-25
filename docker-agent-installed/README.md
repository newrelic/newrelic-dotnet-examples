# New Relic .NET Agent Samples - Docker - Agent Installed

This folder contains a set of sample applications that run in Docker containers for various Linux distros. These samples
demonstrate how to install the New Relic .NET Agent from a linux package manager or from the release tarball and are
appropriate for use cases where engineers are unable to add the New Relic .NET Agent NuGet packages to their application.

Refer to the README for each of the samples for further details and instructions.

## Verifying the agent download

The Alpine and CentOS samples download the agent from the New Relic download site. To make sure that the file that gets
installed is the one New Relic built, each `Dockerfile` pins the agent version and the SHA-256 hash of the downloaded
file, and the build fails if they do not match. To use another agent version, change both `NEW_RELIC_AGENT_VERSION` and
`NEW_RELIC_AGENT_SHA256` in the `Dockerfile`. The hash of every file of every release is listed in
[release-data/download-site-hashes.json](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/release-data/download-site-hashes.json) in the .NET agent repository.
For agent version 10.58.0 and higher you can also verify a file's GitHub attestation. See
[How to verify .NET agent downloads](https://docs.newrelic.com/docs/apm/agents/net-agent/other-installation/how-verify-checksum-net-agent-downloads/).

The Ubuntu sample installs the agent with `apt`, which checks the signature of the package repository.
