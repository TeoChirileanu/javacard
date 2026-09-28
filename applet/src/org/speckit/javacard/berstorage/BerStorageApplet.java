package org.speckit.javacard.berstorage;

import javacard.framework.APDU;
import javacard.framework.Applet;
import javacard.framework.ISO7816;
import javacard.framework.ISOException;
import javacard.framework.Util;

/**
 * Barebones "Hello, World!" Java Card Applet.
 *
 * Creat special pentru a fi cat mai simplu, educational si usor de inteles.
 * Fara protectii de PIN, fara parsare complicata de TLV, fara straturi suplimentare.
 *
 * Comenzi APDU suportate:
 * 1. SELECT Applet: 00 A4 04 00 07 F0 73 70 65 63 01 01 00 -> returneaza 9000 OK
 * 2. CITEȘTE (READ BINARY)  : 00 B0 00 00 00 -> trimite octetii stocati (sau 0 octeti daca e gol)
 * 3. SCRIE   (UPDATE BINARY): 00 D6 00 00 Lc [Bytes] -> salveaza octetii in memoria Flash
 */
public class BerStorageApplet extends Applet {

    // Memorie persistenta salvata in Flash-ul cardului (NVM)
    private byte[] memory;
    private short dataLength;

    // Constructor: ruleaza O SINGURA DATA la instalarea applet-ului pe card
    protected BerStorageApplet() {
        memory = new byte[128]; // Buffer de 128 octeti in Flash
        dataLength = 0;         // Initial este gol (Citeste returneaza gol)
    }

    // Metoda standard de instalare ceruta de masina virtuala Java Card
    public static void install(byte[] bArray, short bOffset, byte bLength) {
        new BerStorageApplet().register();
    }

    // Punctul central de intrare: proceseaza fiecare comanda APDU trimisa de cititor
    public void process(APDU apdu) {
        // Daca comanda este doar de selectare a applet-ului, returneaza direct 9000 OK
        if (selectingApplet()) {
            return;
        }

        byte[] buffer = apdu.getBuffer();
        byte ins = buffer[ISO7816.OFFSET_INS];

        switch (ins) {

            // CITEȘTE: 00 B0 00 00 00 (Standard ISO 7816-4 READ BINARY)
            case (byte) 0xB0:
                if (dataLength > 0) {
                    // Copiaza octetii din memorie in buffer-ul APDU si ii trimite catre PC
                    Util.arrayCopy(memory, (short) 0, buffer, (short) 0, dataLength);
                    apdu.setOutgoingAndSend((short) 0, dataLength);
                } else {
                    // Daca memoria este goala, trimite 0 octeti inapoi (gol)
                    apdu.setOutgoingAndSend((short) 0, (short) 0);
                }
                break;

            // SCRIE: 00 D6 00 00 Lc [Bytes] (Standard ISO 7816-4 UPDATE BINARY)
            case (byte) 0xD6:
                // Citeste octetii veniti de la PC (ex: "Hello, World!")
                short bytesRead = apdu.setIncomingAndReceive();

                // Salveaza octetii primiti in memoria persistenta Flash a cardului
                Util.arrayCopy(buffer, ISO7816.OFFSET_CDATA, memory, (short) 0, bytesRead);
                dataLength = bytesRead;
                break;

            // Orice alta instructiune este respinsa cu "Instruction Not Supported"
            default:
                ISOException.throwIt(ISO7816.SW_INS_NOT_SUPPORTED);
        }
    }
}
