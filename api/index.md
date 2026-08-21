# API reference

Every public type in the nine published packages, reflected out of the assemblies that ship on
nuget.org - so nothing here is API you cannot install.

The most-visited namespaces carry a note saying which contract layer they belong to - the consumer-first
API most test authors need, the extension API for package authors, or the builder scaffolding that is
public only because the fluent chain is composed from it - and a link to the page that explains it.
Namespaces without such a note have not been labelled yet.

Signatures are documented for `net8.0`. The packages also target `net10.0`; the surfaces are
identical, so one reference covers both.
