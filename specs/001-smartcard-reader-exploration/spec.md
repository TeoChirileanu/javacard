# Feature Specification: Smart Card Hello World GUI (.NET 11 AOT + WinUI 3 + Barebones Applet)

**Feature Branch**: `001-smartcard-reader-exploration`

**Created**: 2026-09-28

**Updated**: 2026-09-28

**Status**: Active

**Input**: User pivot: "drop la python si mergem pe .net 11 cu AOT + winui3 (win-dev-skills v0.6.1) + flaui + UFO; vreau un hello world gui: apas Citeste -> gol, apas Scrie -> ok, apas Citeste -> 'Hello, World!'; refacem applet-ul Java Card sa fie cat mai simplist si barebones ca sa inteleg fiecare linie de cod."

---

## Clarifications

### Session 2026-09-28 (Initial)
- Q: Ce model de stocare si acces pe card? -> Initial s-a propus BER-TLV si protectie PIN.

### Session 2026-09-28 (Pivot la .NET 11 + WinUI 3 + Barebones Applet)
- **Host Stack**: Se elimina complet stratul Python CLI (`card_tool.py`, `tlv_tool.py`). Noul strat gazda este o aplicatie moderna **WinUI 3 (Windows App SDK) pe .NET 11 compilat Native AOT**, urmand principiile [win-dev-skills v0.6.1](https://github.com/microsoft/win-dev-skills/releases/tag/v0.6.1).
- **Testare Automata GUI**: Se integreaza **FlaUI (UIA3)** pentru validare automatizata a fluxului din interfata, compatibil cu scenarii de asistenta autonoma tip **UFO (UI-Focused Agent)**.
- **Java Card Applet**: S-a rescris de la zero intr-o varianta ultra-simplificata (barebones), fara parsare TLV si fara verificari PIN. Applet-ul aloca un buffer de memorie Flash (NVM) de 128 octeti si raspunde exclusiv la instructiuni standard ISO 7816-4:
  - `READ BINARY` (`00 B0 00 00 00`): Citeste octetii existenti (sau returneaza 0 octeti daca este gol).
  - `UPDATE BINARY` (`00 D6 00 00 Lc [Bytes]`): Salveaza octetii primiti in Flash.

### Session 2026-09-30
- Q: Ce text arată aplicația la card gol și după o scriere reușită? → A: `TxtOutput` este exact `(Gol)`; `TxtStatus` este exact `OK (9000)`.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Barebones "Hello World" Java Card Applet (Priority: P1)

Ca dezvoltator, vreau un applet Java Card minimal, curat si fara complexitate suplimentara, care sa stocheze un buffer de octeti in memoria Flash si sa permita scrierea si citirea acestuia conform standardului ISO 7816-4, astfel incat sa pot deschide fisierul `.java` si sa inteleg fiecare linie de cod in 2 minute.

**Why this priority**: Pentru invatare si depanare sigura, avem nevoie de cel mai mic numitor comun functional (barebones) inainte de a adauga straturi criptografice sau de securitate.

**Independent Test**: Se poate testa direct prin transmiterea secventiala de comenzi APDU (in simulator jCardSim sau pe card):
1. `00 A4 04 00 08 F0 73 70 65 63 01 01 00` (SELECT) -> Raspuns: `90 00`
2. `00 B0 00 00 00` (READ BINARY) -> Raspuns: date goale, status `90 00`
3. `00 D6 00 00 0D 48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21` (UPDATE BINARY cu `"Hello, World!"`) -> Raspuns: `90 00`
4. `00 B0 00 00 00` (READ BINARY) -> Raspuns: `48 65 6C 6C 6F 2C 20 57 6F 72 6C 64 21 90 00`

**Acceptance Scenarios**:
1. **Given** applet-ul instalat si selectat, **When** se trimite comanda `READ BINARY` inainte de orice scriere, **Then** cardul raspunde cu `9000` si 0 octeti de date (stare initiala: gol).
2. **Given** applet-ul selectat, **When** se trimite comanda `UPDATE BINARY` cu octetii corespunzatori textului `"Hello, World!"`, **Then** cardul salveaza octetii in memorie si returneaza `9000`.
3. **Given** un text scris anterior pe card, **When** se trimite din nou comanda `READ BINARY`, **Then** cardul returneaza exact octetii scrisi anterior si statusul `9000`.
4. **Given** cardul este deconectat de la cititor si reconectat (power cycle), **When** se selecteaza applet-ul si se trimite `READ BINARY`, **Then** textul `"Hello, World!"` este returnat intact din memoria Flash.

---

### User Story 2 - .NET 11 Native AOT SmartCard Service (Priority: P1)

Ca dezvoltator, vreau un serviciu PC/SC in .NET 11 compilat Native AOT (folosind `[LibraryImport("winscard.dll")]`) care sa interactioneze direct cu cititorul Identiv SPR332 si cardul inteligent fara runtime interpretat, fara dependinte greoaie si cu performanta maxima.

**Why this priority**: Inlocuirea completa a Python cu o biblioteca .NET 11 Native AOT ofera o fundatie robusta, nativa de Windows, cu timp de pornire instantaneu si consum minim de resurse.

**Independent Test**: Rularea testelor unitare/serviciu care apeleaza `SCardEstablishContext`, `SCardListReaders`, `SCardConnect`, `SCardTransmit` pe hardware-ul conectat.

**Acceptance Scenarios**:
1. **Given** cititorul Identiv SPR332 conectat, **When** serviciul enumera cititoarele, **Then** gaseste `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0`.
2. **Given** un card JCOP 4 introdus, **When** se apeleaza conectarea pe protocolul `SCARD_PROTOCOL_T1`, **Then** conexiunea se deschide cu succes si returneaza ATR-ul valid.
3. **Given** o conexiune activa, **When** se transmite comanda APDU `00 A4 ...`, **Then** se primeste raspunsul cu `SW = 0x9000`.

---

### User Story 3 - WinUI 3 Desktop Hello World App (Priority: P2)

Ca utilizator final, vreau o aplicatie grafica moderna WinUI 3 pe Windows cu doua butoane mari („Citeste” si „Scrie”) si o zona clara de afisare, astfel incat sa pot experimenta interactiv ciclul de citire/scriere pe card:
- Pasul 1: Apas „Citeste” -> aplicatia afiseaza `(Gol)`.
- Pasul 2: Apas „Scrie” -> aplicatia scrie `"Hello, World!"` pe card si afiseaza `OK (9000)`.
- Pasul 3: Apas din nou „Citeste” -> aplicatia citeste cardul si afiseaza `"Hello, World!"`.

**Why this priority**: Reprezinta cerinta de baza a utilizatorului pentru interactiunea vizuala prietenoasa cu cardul inteligent pe desktop.

**Independent Test**: Lansarea aplicatiei WinUI 3 si parcurgerea manuala a fluxului UI Pas 1 -> Pas 2 -> Pas 3.

**Acceptance Scenarios**:
1. **Given** aplicatia WinUI 3 deschisa cu cititorul conectat, **When** utilizatorul da click pe butonul „Citeste” pe un card curat, **Then** eticheta de rezultat `TxtOutput` este exact `(Gol)`.
2. **Given** aplicatia deschisa, **When** utilizatorul da click pe butonul „Scrie”, **Then** cardul primeste payload-ul `"Hello, World!"`, iar eticheta de stare indica `OK (9000)`.
3. **Given** scrierea a fost executata, **When** utilizatorul da click din nou pe „Citeste”, **Then** in caseta de text apare `"Hello, World!"`.

---

### User Story 4 - Automatizare Test UI cu FlaUI & Modelare UFO (Priority: P3)

Ca dezvoltator de calitate si automatizare, vreau un test automatizat folosind FlaUI (pe baza UI Automation 3 - UIA3) care sa lanseze aplicatia WinUI 3, sa localizeze butoanele dupa `AutomationId`, sa execute secventa de click-uri (Citeste -> Scrie -> Citeste) si sa valideze asertiunile fara interventie umana, pregatind terenul pentru agenti autonomi UI (UFO).

**Why this priority**: Garanteaza ca aplicatia functioneaza de la un capat la altul (end-to-end) si previne regresiile vizuale sau functionale.

**Independent Test**: Rularea comenzii `dotnet test` pe proiectul de teste UI FlaUI.

**Acceptance Scenarios**:
1. **Given** executabilul WinUI 3 compilat, **When** se lanseaza testul FlaUI, **Then** fereastra principala este identificata si adusa in prim-plan.
2. **Given** butonul `BtnRead` identificat, **When** se apeleaza `.Click()`, **Then** textul din `TxtOutput` este egal cu `(Gol)`.
3. **Given** butonul `BtnWrite` identificat, **When** se apeleaza `.Click()`, **Then** starea `TxtStatus` este `OK (9000)`.
4. **Given** a doua apasare pe `BtnRead`, **When** se citeste `TxtOutput`, **Then** textul contine exact `"Hello, World!"`.

---

## Edge Cases

- **Cardul nu este introdus in cititor**: Aplicatia WinUI 3 detecteaza lipsa cardului si afiseaza o notificare de tip `InfoBar` („Introduceti cardul in cititor”) fara sa blocheze interfata.
- **Cititorul este deconectat in timpul rularii**: Serviciul PC/SC prinde codul de eroare `SCARD_E_NO_READERS_AVAILABLE` sau `SCARD_E_READER_UNAVAILABLE` si reactualizeaza lista de dispozitive.
- **Payload mai mare de 128 de octeti**: Applet-ul Java Card barebones are o memorie de 128 octeti. Trimiterea unui buffer mai lung este respinsa sau trunchiata in siguranta.
- **Power Cycle / Card Reset**: Datele din `memory` sunt salvate in NVM (Persistent Flash), astfel incat textul `"Hello, World!"` ramane memorat chiar daca utilizatorul scoate cardul din cititor si il reintroduce.

---

## Functional Requirements

- **FR-001**: Codul sursa al applet-ului Java Card (`BerStorageApplet.java`) TREBUIE sa fie sub 100 de linii, comentat integral in limba romana si sa implementeze strict `00 B0` (Read) si `00 D6` (Write) cu buffer NVM de 128 octeti.
- **FR-002**: Solutia gazda TREBUIE dezvoltata in C# pe .NET 11 cu suport Native AOT, fara runtime Python.
- **FR-003**: Comunicarea cu cititorul se va face prin WinSCard (`winscard.dll`) folosind API-ul nativ `[LibraryImport]` pentru compatibilitate Native AOT completa.
- **FR-004**: Aplicatia grafica TREBUIE sa fie dezvoltata in WinUI 3 (Windows App SDK), oferind elemente XAML native, butoane dedicate „Citeste” si „Scrie” si `AutomationId` explicit pentru accesibilitate si testare.
- **FR-005**: Solutia TREBUIE sa includa un proiect de teste automate cu `FlaUI.UIA3` care verifica secventa de citire-scriere-citire.
- **FR-006**: La citire fără date, `TxtOutput` este exact `(Gol)`. După scrierea reușită a payload-ului, `TxtStatus` este exact `OK (9000)`.

---

## Success Criteria *(measurable)*

- **SC-001**: Timpul de pornire al aplicatiei Native AOT WinUI 3 este sub 1 s.
- **SC-002**: Rularea testului FlaUI valideaza ciclul complet (Citeste -> Gol, Scrie -> OK, Citeste -> Hello, World!) cu succes 100% in mod automat.
- **SC-003**: Applet-ul Java Card are 100% code coverage pe masina virtuala simulata jCardSim.
