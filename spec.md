# 🩹 AutoPatch Framework - API Specification

## Übersicht

Das AutoPatch Framework ermöglicht automatische Real-Time Synchronisation von Objekten zwischen Server und Client über SignalR und JsonPatch. Das Framework ist generisch und transparent - einmal aktiviert werden Objekte automatisch aktuell gehalten ohne weiteren Code-Aufwand.

## Konzept

- **Server-driven Updates**: Server ist Single Source of Truth
- **Automatisches Patching**: JsonPatch aktualisiert lokale Objekte transparent
- **UI Integration**: Über INotifyPropertyChanged werden UI-Updates automatisch getriggert
- **Minimale API**: Nur Subscribe/Unsubscribe, der Rest passiert automatisch
- **Bidirektionale Sync**: Optional können Clients auch Änderungen zurück zum Server senden
- **Intelligent Throttling**: Batch-Queue System für Performance-Optimierung

## Use Cases

- Live-Dashboards (Sensoren, Alarmanlagen)
- Real-Time Tracking (Personen, Fahrzeuge, Geräte)
- Status-Monitoring (System Health, Prozesse)
- Live-Feeds (Events, Notifications)

---

## Server API

### Services registrieren

```csharp
services.AddAutoPatch(options => 
{
    options.DefaultThrottleInterval = TimeSpan.FromMilliseconds(100);
    options.MaxBatchSize = 50; // Optional: Maximale Batch-Größe
});
```

### Objekt-Typen registrieren

```csharp
services.AddAutoPatch()
    .AddObjectType<SensorStatus>(config => 
    {
        config.KeyProperty = x => x.Id;
        config.ExcludeProperties = new[] { "InternalId", "TempData" };
        config.ClientChangePolicy = ClientChangePolicy.Auto; // Auto | RequireConfirmation | Reject
        config.ThrottleInterval = TimeSpan.FromMilliseconds(50); // Override per Type
    })
    .AddObjectType<PersonLocation>(config =>
    {
        config.KeyProperty = x => x.PersonId;
        config.ClientChangePolicy = ClientChangePolicy.Reject; // Read-only für Clients
        config.ThrottleInterval = TimeSpan.FromMilliseconds(200); // Langsamere Updates
    })
    .AddChangeHandler<SensorStatus, SensorChangeHandler>()
    .AddChangeHandler<PersonLocation, PersonChangeHandler>();
```

**ClientChangePolicy Optionen:**
- **Auto**: Client-Änderungen werden sofort übernommen und gebroadcastet
- **RequireConfirmation**: Server validiert Client-Änderungen vor Übernahme
- **Reject**: Server lehnt alle Client-Änderungen ab (Read-only Mode)

**Change Handler:**
- **IChangeHandler<T>**: Interface für Validation und Apply Logic
- **ValidateAsync**: Wird nur bei RequireConfirmation Policy aufgerufen
- **ApplyAsync**: Wird immer aufgerufen bei erfolgreicher Validation

**Throttling Configuration:**
- **ThrottleInterval**: Minimaler Abstand zwischen Batch-Sends
- **MaxBatchSize**: Maximale Anzahl Updates pro Batch (verhindert Memory-Issues)

### SignalR Integration

```csharp
services.AddSignalR()
    .AddAutoPatch();  // Registriert automatisch die Hubs
```

### Usage

```csharp
// Single Operations
_autoPatchService.NotifyChanged(sensorObject);
_autoPatchService.NotifyDeleted<SensorStatus>(sensorId);
_autoPatchService.NotifyAdded(newSensor);

// Bulk Operations
_autoPatchService.NotifyChanged(sensorObjects); // IEnumerable<T>
_autoPatchService.NotifyDeleted<SensorStatus>(sensorIds); // IEnumerable<TKey>
_autoPatchService.NotifyAdded(newSensors); // IEnumerable<T>

// Batch Operations (mixed)
_autoPatchService.NotifyBatch(batch => 
{
    batch.Changed(sensorObjects);
    batch.Deleted<SensorStatus>(sensorIds);
    batch.Added(newSensors);
});
```

### Throttling Verhalten

```csharp
// Beispiel: Hochfrequente Sensor-Updates
0ms:  _autoPatchService.NotifyChanged(sensor); // Temp = 25
      // → Queue: [Patch1], Timer start (50ms für SensorStatus)
      
10ms: _autoPatchService.NotifyChanged(sensor); // Humidity = 80
      // → Queue: [Patch1, Patch2]
      
30ms: _autoPatchService.NotifyChanged(sensor); // Status = "Alert"
      // → Queue: [Patch1, Patch2, Patch3]
      
50ms: Timer expires
      // → Send Batch: [Patch1, Patch2, Patch3] via SignalR
      // → Queue = []
```

---

## Client API

### Services registrieren

```csharp
services.AddAutoPatch();
```

### Objekt-Typen konfigurieren

```csharp
services.AddAutoPatch()
    .AddObjectType<SensorStatus>(config => 
    {
        config.KeyProperty = x => x.Id;  // Key Accessor für Object Matching
        config.ChangeTracking = ChangeTrackingMode.ManualCommit; // Disabled | ManualCommit | AutoCommit
    })
    .AddObjectType<PersonLocation>(config =>
    {
        config.KeyProperty = x => x.PersonId;
        config.ChangeTracking = ChangeTrackingMode.Disabled; // Default: Kein Tracking
    });
```

**ChangeTrackingMode Optionen:**
- **Disabled**: Kein automatisches Change Tracking (Default)
- **ManualCommit**: Automatic Tracking, Manual CommitChanges erforderlich  
- **AutoCommit**: Automatic Tracking + sofortiger Commit bei Änderungen

### Client verwenden

```csharp
IAutoPatchClient client = serviceProvider.GetService<IAutoPatchClient>();

// Connection Management
await client.StartAsync(cancellationToken); // Verbindet zu SignalR Hub
// ... Subscriptions sind nur bei aktiver Connection möglich
await client.StopAsync(cancellationToken);  // Trennt Verbindung, behält Subscriptions in Memory
```

**Optional: Automatischer Start mit HostedService** (nicht verfügbar in .NET MAUI)
```csharp
services.AddAutoPatch()
    .AddHostedService(); // Startet automatisch mit der App
```

### Subscriben

```csharp
// Subscribe mit Policy-Info Response
var sensors = new List<SensorStatus>();
var subscribeResult = await client.SubscribeAsync<SensorStatus>(sensors);

// Policy-Info für UI-Anpassungen nutzen
if (subscribeResult.CanEdit)
{
    editButton.IsEnabled = true;
    statusLabel.Text = "Live editing enabled";
}
else
{
    editButton.IsEnabled = false;
    statusLabel.Text = "Read-only mode";
}

// Oder spezifische Policy prüfen
switch (subscribeResult.ClientChangePolicy)
{
    case ClientChangePolicy.Auto:
        ShowInstantEditingUI();
        break;
    case ClientChangePolicy.RequireConfirmation:
        ShowConfirmationEditingUI();
        break;
    case ClientChangePolicy.Reject:
        ShowReadOnlyUI();
        break;
}
```

**SubscribeResult Properties:**
- `ClientChangePolicy`: Server-Policy für diesen Objekttyp
- `CanEdit`: Shortcut für Policy != Reject
- `RequiresConfirmation`: Shortcut für Policy == RequireConfirmation
- `InitialData`: Die initial geladenen Objekte

Das Framework:
1. Holt initial alle Objekte vom Server
2. Füllt die übergebene Liste
3. Gibt SubscribeResult mit Server-Policy zurück
4. Startet Live-Updates via SignalR
5. Wendet JsonPatch-Batches automatisch auf die Objekte an (matched via KeyProperty)

### Unsubscriben

```csharp
await client.Unsubscribe<SensorStatus>();
await client.Unsubscribe<PersonLocation>();
```

### Error Handling

```csharp
client.OnError += (exception) => HandleError(exception);
client.OnConnectionLost += () => ShowOfflineMode();
client.OnReconnected += () => 
{
    HideOfflineMode();
    // Alle Subscriptions werden automatisch reaktiviert
};
```

### Bidirektionale Sync (Optional)

**Automatisches Change Tracking:**
Framework subscribes auf INotifyPropertyChanged und detektiert Änderungen automatisch.

**Manual Commit mit direktem Result:**
CommitChanges gibt sofort das Server-Result zurück (via SignalR Hub Method).

**Success/Error Handling:**
Developer erhält direktes Feedback und kann entsprechend reagieren (UI Update, Error Messages, etc.).

---

## Throttling System

### Konzept

Das Throttling-System verhindert Performance-Probleme bei hochfrequenten Updates durch ein intelligentes Batch-Queue System.

### Funktionsweise

1. **Update kommt rein** → Add to Queue
2. **Timer-Check:** "Ist Send-Timer bereits aktiv?"
3. **Nein:** Starte Timer (ThrottleInterval)
4. **Timer expires:** Send komplette Queue als Batch, leere Queue

### Batch-Verarbeitung

**Server-seitig:**
- Updates werden in Type-spezifischen Queues gesammelt
- Timer pro Objekttyp verhindert zu häufige Sends
- Batch wird als Array von JsonPatch-Operationen gesendet

**Client-seitig:**
- Empfängt Array von JsonPatches
- Wendet alle Patches sequenziell an
- Erhält alle Änderungen in korrekter Reihenfolge

### Beispiel-Szenario

```csharp
// Hochfrequente Sensor-Updates:
0ms:  sensor.Temperature = 25     → Queue: [TempPatch], Timer start (100ms)
10ms: sensor.Humidity = 80        → Queue: [TempPatch, HumidityPatch]
50ms: sensor.Status = "Alert"     → Queue: [TempPatch, HumidityPatch, StatusPatch]
60ms: sensor.Temperature = 26     → Queue: [TempPatch, HumidityPatch, StatusPatch, TempPatch2]
100ms: Timer expires → Send Batch mit 4 Patches

// Client wendet alle 4 Patches nacheinander an:
// 1. Temperature: 20 → 25
// 2. Humidity: 60 → 80  
// 3. Status: "OK" → "Alert"
// 4. Temperature: 25 → 26
```

### Vorteile

- **Performance**: Reduziert SignalR-Overhead erheblich
- **Vollständigkeit**: Keine Updates gehen verloren
- **Reihenfolge**: Alle Änderungen werden korrekt angewendet
- **Flexibilität**: ThrottleInterval pro Objekttyp konfigurierbar
- **Einfachheit**: Keine komplexe Aggregation oder State-Tracking erforderlich

### Konfiguration

```csharp
.AddObjectType<SensorStatus>(config => 
{
    config.ThrottleInterval = TimeSpan.FromMilliseconds(50);  // Schnelle Updates
})
.AddObjectType<PersonLocation>(config =>
{
    config.ThrottleInterval = TimeSpan.FromMilliseconds(500); // Langsamere Updates
})
.AddObjectType<SystemHealth>(config =>
{
    config.ThrottleInterval = TimeSpan.Zero; // Kein Throttling - sofortige Updates
});
```

---

## Technische Details

### Server-seitig
- SignalR Hub mit Gruppen pro Objekttyp
- Event-driven Change Detection mit Throttling-Queues
- Automatische JsonPatch Generierung
- Batch-Broadcast an subscribte Clients
- Timer-basierte Queue-Verarbeitung pro Objekttyp

### Client-seitig
- Automatisches Connection Management
- Batch JsonPatch Application auf lokale Objekte
- INotifyPropertyChanged für UI-Updates
- Transparente Reconnection
- Optional: Bidirektionale Sync mit automatischem Change Tracking

### Datenfluss

**Server → Client (Standard):**
1. **Initial**: Client subscribed → Server sendet komplette Objektliste
2. **Updates**: Server Events → Queue → Timer → Batch JsonPatch generiert → an Gruppe gesendet → Client wendet Patches an
3. **UI**: Objekte ändern sich → PropertyChanged → UI updated automatisch

**Client → Server (Bidirektional, Optional):**
1. **Change Detection**: Objekt ändert sich → PropertyChanged → Framework detects
2. **Dirty Tracking**: Objekt als dirty markiert (wenn AutoCommit = false)
3. **Commit**: Manual oder Auto → JsonPatch generiert → an Server gesendet
4. **Server Processing**: Validation nach ClientChangePolicy → Accept/Reject
5. **Broadcast**: Bei Accept → Patch an alle anderen Clients (mit Throttling)

### Throttling-Pipeline

```
Object Change → Queue Check → Timer Management → Batch Creation → SignalR Send
     ↓              ↓              ↓               ↓              ↓
 NotifyChanged → Add to Queue → Start/Reset → JsonPatch Array → Hub.SendToGroup
                     ↓         Timer (50ms)        ↓              ↓
               Track per Type      ↓         Serialize Batch → All Subscribers
                     ↓         On Expire          ↓              ↓
              {SensorStatus: []}    ↓      [{op:"replace",...}] → Client Apply
```

## Vorteile

- **Bandwidth-effizient**: Nur Deltas werden übertragen, gebatcht für minimalen Overhead
- **Developer-freundlich**: Minimale API, maximale Automation
- **UI-Integration**: Nahtlos mit Data Binding
- **Skalierbar**: Nutzt SignalR-Infrastruktur mit intelligentem Throttling
- **Robust**: Automatische Reconnection und Error Handling
- **Performance-optimiert**: Batch-System verhindert Flooding bei hochfrequenten Updates

## Erweiterungen (Zukunft)

- Filtering bei Subscribe
- Conditional Updates
- Custom Serialization
- Offline Support mit Sync beim Reconnect
- Custom Validation Handlers für ClientChangePolicy
- Conflict Resolution Strategies
- Priority-based Throttling
- Adaptive Throttling basierend auf Network Conditions
