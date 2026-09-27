# The hosted table (ADR 0080, ADR 0081): the CLI's `table` command, its pages, and the content it plays,
# built from the repository and nothing else.
#
#   docker build -t downfall-table --build-arg ENGINE_COMMIT="$(git rev-parse --short=12 HEAD)" .
#   docker run --rm -p 8080:8080 downfall-table
#
# The default command opens a lobby on port 8080 and prints the operator's token to the log. The container
# app in infra/ replaces it with the platform's sign-in and a blob container to record into.

# The base images are arguments so a machine behind a proxy with its own certificate authority can build on
# an image that trusts it; the defaults are Microsoft's, and CI builds on those.
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/runtime:10.0

FROM ${SDK_IMAGE} AS build

# The engine version a recording is stamped with (ADR 0013). The build reads it from git, which the image
# does not carry, so it is handed in; without it every session says it was played on an unknown engine.
ARG ENGINE_COMMIT=unknown

WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props DownfallArena.slnx .editorconfig ./
COPY src/ src/
COPY tools/ tools/
COPY data/ data/

RUN dotnet publish src/DownfallArena.Cli --configuration Release --output /out/bin \
      -p:EngineVersionFromGit=false -p:SourceRevisionId="${ENGINE_COMMIT}" \
 && dotnet run --project tools/DownfallArena.DataBuilder --configuration Release \
      -p:EngineVersionFromGit=false -- data /out/data/dst

FROM ${RUNTIME_IMAGE}

# The host reads everything relative to where it runs, as it does from the repository root on a laptop:
# the pages, the viewer the session page is drawn with, the built content, the rule set, and the weights
# and policies an agent spec may name.
WORKDIR /app
COPY --from=build /out/bin/ bin/
COPY --from=build /out/data/dst/ data/dst/
COPY table/ table/
COPY viewer/index.html viewer/viewer.css viewer/
COPY docs/tabletop/playtest.rules.json docs/tabletop/
COPY learning/weights/ learning/weights/
COPY models/ models/

# The .NET images ship an unprivileged user for this; the host binds 8080, which needs no privilege.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "bin/DownfallArena.Cli.dll"]
CMD ["table", "--lobby", "--bind", "0.0.0.0", "--port", "8080", "--rules", "docs/tabletop/playtest.rules.json", "--no-record"]
