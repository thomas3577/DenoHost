# DenoProcess Klasse

Die `DenoProcess` Klasse erweitert die bestehende "fire and forget" Funktionalität des DenoHost-Projekts um die Möglichkeit, langlebige Deno-Prozesse zu starten, zu kontrollieren und zu beenden.

## Übersicht

Im Gegensatz zur statischen `Deno` Klasse, die Deno-Befehle ausführt und auf deren Beendigung wartet, ermöglicht die `DenoProcess` Klasse:

- **Langlebige Prozesse**: Starten und Verwalten von Deno-Prozessen, die über längere Zeit laufen
- **Bidirektionale Kommunikation**: Senden von Eingaben an den Prozess und Empfangen von Ausgaben
- **Prozess-Management**: Überwachung des Prozessstatus, Neustart und kontrollierten Stopp
- **Event-basierte Architektur**: Reagieren auf Prozess-Events wie Ausgaben oder Beendigung

## Hauptfunktionen

### Prozess-Lebenszyklus

- `StartAsync()`: Startet den Deno-Prozess
- `StopAsync()`: Stoppt den Prozess (graceful shutdown mit Timeout)
- `RestartAsync()`: Neustart des Prozesses
- `WaitForExitAsync()`: Wartet auf die natürliche Beendigung des Prozesses

### Kommunikation

- `SendInputAsync()`: Sendet Eingaben an den Prozess über stdin
- `OutputDataReceived` Event: Erhält Ausgaben von stdout
- `ErrorDataReceived` Event: Erhält Fehlermeldungen von stderr

### Überwachung

- `IsRunning`: Property zur Überprüfung des Prozessstatus
- `ProcessId`: Prozess-ID des laufenden Prozesses
- `ExitCode`: Exit-Code nach Beendigung
- `ProcessExited` Event: Benachrichtigung bei Prozessbeendigung

## Verwendungsbeispiele

### Einfache Verwendung

```csharp
using var denoProcess = new DenoProcess(
    command: "run",
    args: new[] { "--allow-read", "my-script.ts" },
    workingDirectory: "/path/to/scripts",
    logger: logger
);

// Events abonnieren
denoProcess.OutputDataReceived += (sender, e) => {
    Console.WriteLine($"Output: {e.Data}");
};

denoProcess.ErrorDataReceived += (sender, e) => {
    Console.WriteLine($"Error: {e.Data}");
};

// Prozess starten
await denoProcess.StartAsync();

// Eingaben senden (optional)
await denoProcess.SendInputAsync("some command");

// Warten oder später beenden
await Task.Delay(5000);
await denoProcess.StopAsync();
```

### Interaktive Kommunikation

```csharp
using var denoProcess = new DenoProcess("run", new[] { "interactive-script.ts" });

var responses = new List<string>();
denoProcess.OutputDataReceived += (sender, e) => {
    if (!string.IsNullOrEmpty(e.Data))
        responses.Add(e.Data);
};

await denoProcess.StartAsync();

// Befehle senden
await denoProcess.SendInputAsync("command1");
await Task.Delay(1000);
await denoProcess.SendInputAsync("command2");
await Task.Delay(1000);
await denoProcess.SendInputAsync("exit");

await denoProcess.WaitForExitAsync();
```

### Prozess-Überwachung

```csharp
using var denoProcess = new DenoProcess("run", new[] { "long-running-service.ts" });

denoProcess.ProcessExited += (sender, e) => {
    Console.WriteLine($"Process exited with code: {e.ExitCode}");
    
    if (e.ExitCode != 0) {
        // Prozess ist unerwartet beendet, Neustart?
    }
};

await denoProcess.StartAsync();

// Überwachung in separatem Task
_ = Task.Run(async () => {
    while (denoProcess.IsRunning) {
        Console.WriteLine($"Process {denoProcess.ProcessId} is still running");
        await Task.Delay(5000);
    }
});

// Hauptanwendung läuft weiter...
```

## Konstruktoren

### DenoProcess(string[] args, ...)

Erstellt eine neue Instanz mit direkten Deno-Argumenten.

```csharp
var process = new DenoProcess(
    args: new[] { "run", "--allow-read", "script.ts" },
    workingDirectory: "/path/to/scripts",
    logger: logger
);
```

### DenoProcess(string command, string[] args, ...)

Erstellt eine neue Instanz mit einem Deno-Befehl und zusätzlichen Argumenten.

```csharp
var process = new DenoProcess(
    command: "run",
    args: new[] { "--allow-read", "script.ts" },
    workingDirectory: "/path/to/scripts",
    logger: logger
);
```

## Error Handling

Die `DenoProcess` Klasse bietet robuste Fehlerbehandlung:

- **Startfehler**: `InvalidOperationException` wenn der Prozess nicht gestartet werden kann
- **Kommunikationsfehler**: Exceptions beim Senden von Eingaben an gestoppte Prozesse
- **Timeout-Behandlung**: Graceful shutdown mit konfigurierbarem Timeout, danach forciertes Beenden

```csharp
try {
    await denoProcess.StartAsync();
} catch (InvalidOperationException ex) {
    logger.LogError("Failed to start Deno process: {Message}", ex.Message);
}

try {
    await denoProcess.StopAsync(timeout: TimeSpan.FromSeconds(30));
} catch (Exception ex) {
    logger.LogError("Error during process shutdown: {Message}", ex.Message);
}
```

## Vergleich: Deno vs. DenoProcess

| Feature | Deno (statisch) | DenoProcess |
|---------|----------------|-------------|
| Ausführungsmodell | Fire-and-forget | Langlebiger Prozess |
| Kommunikation | Einweg (Eingabe → Ausgabe) | Bidirektional |
| Prozess-Kontrolle | Keine | Vollständig |
| Events | Keine | OutputReceived, ErrorReceived, ProcessExited |
| Lebensdauer | Bis Beendigung | Benutzer-kontrolliert |
| Verwendung | Einfache Skript-Ausführung | Interaktive/Service-artige Anwendungen |

## Anwendungsfälle

- **Entwicklungsserver**: Deno-basierte Entwicklungsserver mit Hot-Reload
- **Interaktive Tools**: REPLs oder interaktive Kommandozeilen-Tools
- **Langlebige Services**: Background-Services oder Daemons
- **Streaming-Verarbeitung**: Kontinuierliche Datenverarbeitung
- **Entwicklungstools**: Build-Watchers oder Test-Runner

Die `DenoProcess` Klasse erweitert das DenoHost-Projekt um mächtige Prozess-Management-Funktionen und macht es möglich, Deno nicht nur für einfache Skript-Ausführungen zu verwenden, sondern auch für komplexere, interaktive Anwendungen.
