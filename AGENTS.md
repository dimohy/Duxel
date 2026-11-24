# Duxel repository guide

- Start with [README.md](README.md) or [README.ko.md](README.ko.md) for platform support and runnable examples. [docs/README.md](docs/README.md) links the public guides. [llms.txt](llms.txt) indexes the same sources.
- Consult the [API and source map](docs/duxel-agent-reference.md), then verify the current signature in source. Use [Gallery examples](samples/Duxel.Gallery) and [file-based apps](samples/fba) as working references.
- Build with the .NET SDK selected in `global.json`. Use `dotnet build Duxel.slnx -c Release` after project changes. For a user-facing Gallery change, update `GalleryRevision` and run the Gallery normally. Reserve the default full `scripts/Test-WidgetContracts.ps1` run for final acceptance after the implementation is in place.
- Retain paired English and Korean user guides. Use versioned, verified NuGet packages in public FBA launchers.
- Keep credentials, session handoffs, generated output, private working notes, and unpublished announcements in ignored paths. Publish product behavior and reproducible examples.
