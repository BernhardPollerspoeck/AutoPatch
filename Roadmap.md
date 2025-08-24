# AutoPatch Framework - Entwicklungsplan (TDD)

## Phase 1: Foundation
| Component | Beschreibung | Tasks | Aufwand |
|-----------|--------------|-------|---------|
| **Projekt-Setup** | Solution, 3 Projekte, Dependencies | Solution erstellen, Server/Client/Shared Projekte, NuGet refs, Test-Projekte | 3h |
| **Core Models** | Change-Tracking, JsonPatch-Models | Tests + ChangeType enum, ObjectChange<T>, PatchOperation models | 4h |
| **Configuration** | Options-Pattern, ObjectType-Registry | Tests + AutoPatchOptions, ObjectTypeConfig<T>, ServiceCollection extensions | 8h |
| **IAutoPatchService** | Basic Interface + Implementation | Tests + Interface + AutoPatchService Klasse + DI setup | 6h |

**Phase 1 Gesamt: 21h**

## Phase 2: Basic Sync
| Component | Beschreibung | Tasks | Aufwand |
|-----------|--------------|-------|---------|
| **SignalR Hub** | Basic Hub ohne Throttling | Tests + AutoPatchHub, Subscribe/Unsubscribe methods, Group management | 5h |
| **JsonPatch Gen** | Object → JsonPatch Operations | Tests + Object comparison, property diff, JsonPatch creation, key matching | 12h |
| **Client Core** | IAutoPatchClient, Subscribe/Unsubscribe | Tests + Interface + HubConnection wrapper, connection mgmt, subscription tracking | 10h |
| **Patch Application** | JsonPatch → Object Updates | Tests + Patch parsing, object lookup by key, property updates, error handling | 10h |

**Phase 2 Gesamt: 37h**

## Phase 3: Throttling
| Component | Beschreibung | Tasks | Aufwand |
|-----------|--------------|-------|---------|
| **Queue System** | Per-Type Queues + Timer | Tests + ConcurrentQueue per type, Timer management, thread safety | 12h |
| **Batch Processing** | Queue → Batch → SignalR Send | Tests + Queue drain, batch creation, SignalR group send, error recovery | 6h |
| **Performance** | Memory-Management, Bulk-Ops | Tests + Queue limits, cleanup strategies, bulk NotifyChanged optimization | 6h |

**Phase 3 Gesamt: 24h**

## Phase 4: Policies
| Component | Beschreibung | Tasks | Aufwand |
|-----------|--------------|-------|---------|
| **ClientChangePolicy** | Auto/RequireConfirmation/Reject | Tests + Enum, config integration, hub method routing | 4h |
| **ChangeHandler** | IChangeHandler<T>, Validation | Tests + Interface, DI registration, validation pipeline, error responses | 8h |
| **Bidirectional** | Client→Server Changes | Tests + INotifyPropertyChanged tracking, CommitChanges, server validation | 12h |
| **SubscribeResult** | Policy-Info für UI | Tests + Result model, policy info, initial data + metadata response | 3h |

**Phase 4 Gesamt: 27h**

## Phase 5: Production
| Component | Beschreibung | Tasks | Aufwand |
|-----------|--------------|-------|---------|
| **Error Handling** | Events, Reconnection, Resilience | Tests + Event definitions, auto-reconnect, subscription restore, error propagation | 8h |
| **Hosted Service** | Auto-Start für Non-MAUI | Tests + IHostedService implementation, lifecycle management | 3h |
| **Logging/Metrics** | Observability | Tests + ILogger integration, performance counters, debug info | 5h |

**Phase 5 Gesamt: 16h**

## Phase 6: Polish
| Component | Beschreibung | Tasks | Aufwand |
|-----------|--------------|-------|---------|
| **Documentation** | XML-Docs, Samples | XML comments, README samples, usage examples | 8h |
| **NuGet** | Packaging, Publishing | .csproj config, package metadata, CI/CD pipeline | 3h |

**Phase 6 Gesamt: 11h**

---

## Zusammenfassung

| Phase | Beschreibung | Aufwand | Kumulativ |
|-------|--------------|---------|-----------|
| **Phase 1** | Foundation | 21h | 21h |
| **Phase 2** | Basic Sync | 37h | 58h |
| **Phase 3** | Throttling | 24h | 82h |
| **Phase 4** | Policies | 27h | 109h |
| **Phase 5** | Production | 16h | 125h |
| **Phase 6** | Polish | 11h | **136h** |

**Gesamtaufwand: 136 Stunden (~17 Arbeitstage)**

## Meilensteine

- **MVP (Phase 1-2)**: 58h - Grundfunktionalität ohne Throttling
- **Production Ready (Phase 1-4)**: 109h - Mit Performance-Optimierung und Policies  
- **Enterprise Ready (Phase 1-6)**: 136h - Vollständig mit Dokumentation und NuGet

## TDD-Approach

Jede Komponente wird entwickelt mit:
1. **Interface Design** - Klare API Definition
2. **Failing Tests** - Komplette Test-Suite vor Implementation
3. **Implementation** - Code bis alle Tests grün sind
4. **Refactoring** - Cleanup und Optimierung

Alle Zeiten beinhalten bereits den TDD-Overhead für Tests, Mocks und Test-Infrastructure.
