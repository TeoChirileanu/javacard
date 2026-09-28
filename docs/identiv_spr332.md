# Documentație Tehnică: Identiv (SCM) SPR332 v2 (SPRx32)

## 1. Prezentare Generală

- **Dispozitiv**: Identiv (SCM Microsystems) SPR332 v2.0
- **Nume raportat în Windows PC/SC**: `SCM Microsystems Inc. SPRx32 USB Smart Card Reader 0`
- **Tip dispozitiv**: **Cititor de smart carduri securizat Clasa 2 cu PINpad numeric (fără ecran LCD)**
- **Interfață**: USB 2.0 Full Speed, conform specificației USB CCID
- **Protocoale Smart Card suportate**: ISO/IEC 7816-3 T=0, T=1 (Class A, B, C - 5V, 3V, 1.8V)

---

## 2. De ce scoate sunete (Buzzer-ul intern)?

Deoarece cititorul **nu are ecran LCD** (`wLcdLayout = 0`), **buzzerul acustic este canalul principal de feedback** prin care dispozitivul confirmă starea și operațiunile către utilizator:

1. **Feedback la apăsarea tastelor (Keypress Beep)**:
   - Fiecare apăsare a unei taste numerice (`0-9`), a tastei `Clear` (galbenă/corecție) sau `Cancel` (roșie) emite un **bip scurt**.
   - Rol: Oferă certitudinea că cifra a fost înregistrată fizic de tastatură, fără ca aceasta să fie afișată pe vreun ecran.

2. **Introducere / Resetare Smart Card**:
   - Când introduci un smart card în slot și cititorul detectează contactele electrice (sau când PC/SC trimite un semnal de reset / warm reset), emite un **bip scurt de detectare**.

3. **Sesiune de introducere securizată a PIN-ului (Secure PIN Entry)**:
   - Când o aplicație de pe PC (utilitar de semnătură digitală, client VPN, browser sau middleware PKI) cere verificarea PIN-ului prin standardul **PC/SC Part 10** (`FEATURE_VERIFY_PIN_DIRECT`):
     - **Prompt sonor**: Cititorul emite un semnal de atenționare că a intrat în modul PINpad securizat.
     - În acest mod, cifrele tastate pe tastatură sunt trimise **direct către smart card prin magistrala internă a cititorului**, fără a trece prin cablul USB către calculator. Keyloggerele sau malware-ul de pe PC nu pot intercepta PIN-ul.
     - **Confirmare**: La apăsarea tastei `Enter` (verde), cititorul emite un bip de confirmare și trimite comanda `VERIFY` către card.
     - **Anulare / Timeout**: Dacă apeși `Cancel` sau dacă expiră timpul de așteptare, emite două bipuri scurte sau un bip descendent.

4. **Bipăituri continue sau repetate**:
   - Dacă cititorul bipăie repetat fără să atingi tastatura, de obicei o aplicație de pe PC (un serviciu de certificate Windows, un software de banking/token sau un script care rulează într-o buclă) se conectează și se deconectează continuu sau interoghează cititorul prin escape commands.
   - De asemenea, poate indica un contact imperfect cu cipul cardului.

---

## 3. Funcționalități PC/SC Part 10 (Verificate pe hardware)

La interogarea directă a cititorului prin `IOCTL_GET_FEATURE_REQUEST` (0x00313520), cititorul a raportat următoarele capabilități active:

| Caracteristică PC/SC Part 10 | Cod IOCTL | Rol |
| :--- | :--- | :--- |
| **`FEATURE_VERIFY_PIN_DIRECT`** | `0x00313538` | Permite introducerea PIN-ului direct de pe tastatura cititorului |
| **`FEATURE_MODIFY_PIN_DIRECT`** | `0x0031353C` | Permite schimbarea PIN-ului securizat direct pe tastatură |
| **`FEATURE_IFD_PIN_PROPERTIES`** | `0x00313548` | Returnează `wLcdLayout = 0` (confirmă absența ecranului LCD) |
| **`FEATURE_GET_KEY_PRESSED`** | `0x00313534` | Permite citirea evenimentelor de tastatură |
| **`FEATURE_ABORT`** | `0x0031354C` | Anulează o sesiune de introducere PIN în curs |

---

## 4. Controlul Buzzer-ului din Software

Buzzer-ul este activat implicit la pornire (*enabled by default*).
Conform manualului de referință al SPR332, aplicațiile pot trimite o comandă de tip **Escape** (`SCardControl`) pentru a opri sau porni buzzer-ul:

- Dacă se folosește driverul nativ Microsoft CCID din Windows, pentru a permite comenzi Escape trebuie activată cheia în registry:
  ```text
  HKLM\SYSTEM\CurrentControlSet\Enum\USB\<DeviceID>\Device Parameters
  DWORD: EscapeCommandEnable = 1
  ```
- În mod normal nu este recomandată dezactivarea buzzer-ului pe acest model, deoarece fiind lipsit de ecran LCD, utilizatorul nu ar mai avea nicio confirmare senzorială că tastele au fost acționate.
