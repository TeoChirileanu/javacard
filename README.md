# Java Card Development (NXP JCOP 4 P71)

Mediu de dezvoltare, testare și administrare pentru smart carduri Java Card **NXP JCOP 4 P71D321** conectate prin cititor securizat USB **Identiv (SCM) SPR332 / SPRx32**.

---

## 🛠️ Hardware & Specificații

| Componentă | Specificație |
| :--- | :--- |
| **Card Reader** | Identiv / SCM Microsystems SPR332 v2 (`SPRx32`) - PINpad Clasa 2 (fără LCD) |
| **Smart Card** | NXP SmartMX3 P71D321 (CC EAL 6+) |
| **Sistem de Operare** | NXP JCOP 4 |
| **Java Card VM** | Java Card 3.0.5 Classic Edition |
| **GlobalPlatform** | v2.3 (SCP02 Opțiunea 55, chei test 3DES de fabrică) |
| **Protocol Transmisie** | ISO/IEC 7816-3 T=1 (Contact) & ISO/IEC 14443 Type A (NFC Contactless) |
| **Memorie NVM (Flash)** | ~142 KB disponibili pentru applet-uri și stocare |
| **Memorie RAM** | ~3.2 KB memorie tranzitorie |
| **Criptografie Hardware** | RSA (până la 4096-bit), ECC (NIST / Brainpool până la 521-bit), AES (128/192/256), 3DES, SHA-2/3 |

---

## 📂 Structura Proiectului

- [`AGENTS.md`](AGENTS.md) - Ghid complet pentru agenți AI și dezvoltatori (reguli de siguranță a memoriei, AIDs, coprocesoare criptografice).
- [`memory.md`](memory.md) - Harta detaliată a memoriei, decodarea ATR, CPLC și structura de tag-uri GP.
- [`docs/identiv_spr332.md`](docs/identiv_spr332.md) - Manualul și explicația sunetelor/buzzerului pentru cititorul Identiv SPR332.
- [`tools/card_tool.py`](tools/card_tool.py) - Utilitar Python PC/SC nativ (`winscard.dll`) pentru trimitere de comenzi APDU și autentificare GlobalPlatform.
- [`gp.pdf`](gp.pdf) - Specificația oficială GlobalPlatform Card v2.3.
- [`p71.pdf`](p71.pdf) - Fișa tehnică NXP JCOP 4 P71 Short Data Sheet.
- [`jcop.pdf`](jcop.pdf) - Specificații și prezentare NXP JCOP 4.
- [`nxp.pdf`](nxp.pdf) - Documentația Common Criteria Security Target Lite.
- [`kit.zip`](kit.zip) - Specificațiile Java Card 3.0.5 Classic Edition API & VM.

---

## 🚀 Utilizare Rapidă

Cititorul și cardul pot fi interogate direct prin utilitarul Python inclus (fără a fi nevoie de instalare de drivere suplimentare):

```powershell
# Afișare cititoare conectate și stare ATR
python tools/card_tool.py atr

# Autentificare GlobalPlatform SCP02 și listare pachete / applet-uri instalate
python tools/card_tool.py list

# Trimitere APDU manual (ex: SELECT Card Manager)
python tools/card_tool.py apdu 00A4040008A000000151000000
```
