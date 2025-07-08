# ObsidianGitMirror
🚀 GitToObsidianSync – Download & Verwendung
📦 Download
Navigiere zur Releases-Seite dieses Repos.

Lade die ZIP-Datei für dein Betriebssystem herunter (z. B. GitToObsidianSync-win-x64.zip).

Entpacke den Ordner an einen beliebigen Ort auf deinem Rechner (z. B. C:\Tools\GitToObsidianSync\).

🖥️ Starten
Doppelklicke auf die Datei:

bash
Kopieren
Bearbeiten
GitToObsidianSync.exe
⚠️ Keine Installation nötig. Die .NET Runtime ist bereits integriert.

⚙️ Konfiguration
Beim Start des Programms wird dir ein interaktives Setup-Menü angezeigt, in dem du folgende Pfade festlegen kannst:

🗂 Git Repository Path – der lokale Pfad zu deinem Git-Projekt
(z. B. C:\Users\Du\Repos\MyProject)

📓 Vault Output Path – der Pfad zu deinem Obsidian Vault
(z. B. C:\Users\Du\ObsidianVault)

💡 Diese Pfade musst du nur beim ersten Start setzen, solange sich dein Setup nicht ändert.
Die Einstellungen werden dauerhaft in der config.json gespeichert.

Du kannst sie aber jederzeit erneut ändern, indem du das Programm neu startest und im Menü entsprechende Optionen auswählst.

🔄 Funktionsweise
Das Programm überwacht dein Git-Repository und reagiert automatisch auf Commits.

Alternativ kannst du manuell synchronisieren:
Drücke Ctrl + K und dann S im Konsolenfenster.

Änderungen an unterstützten Dateien (z. B. .cs, .py, .docx, .xlsx) werden ins Markdown-Format konvertiert und strukturiert in dein Obsidian-Vault geschrieben.

🧼 Beenden
Beende das Programm mit
Ctrl + K und dann Q

📁 Unterstützte Dateiformate
Code: .cs, .py, .js, .ts, .java, .cpp, .c, .json, .html, .css

Text: .txt, .md

Office: .docx, .xlsx
(Excel-Zeilen werden in einzelne Markdown-Dateien mit YAML-Frontmatter konvertiert)
