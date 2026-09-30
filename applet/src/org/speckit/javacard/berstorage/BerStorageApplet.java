package org.speckit.javacard.berstorage;

import javacard.framework.APDU;
import javacard.framework.Applet;
import javacard.framework.ISO7816;
import javacard.framework.ISOException;
import javacard.framework.JCSystem;
import javacard.framework.Util;

/**
 * Applet Hello World pentru Java Card 3.0.5.
 * Un singur buffer de 128 de octeti in Flash, alocat la instalare.
 * SELECT: 00 A4 04 00 08 F0 73 70 65 63 01 01 00 -> 9000
 * READ BINARY (INS B0): 00 B0 00 00 00 -> octetii salvati, sau zero octeti daca e gol
 * UPDATE BINARY (INS D6): 00 D6 00 00 Lc [octeti] -> salveaza in Flash
 * AID applet: F0 73 70 65 63 01 01 00
 */
public class BerStorageApplet extends Applet {

    // Memorie persistenta. dataLength 0 inseamna card gol.
    private byte[] memory;
    private short dataLength;

    // Constructorul ruleaza o singura data, din install(). Fara alocari in process().
    protected BerStorageApplet() {
        memory = new byte[128];
        dataLength = 0;
    }

    public static void install(byte[] bArray, short bOffset, byte bLength) {
        new BerStorageApplet().register();
    }

    public void process(APDU apdu) {
        // Platforma a trimis SELECT. Raspunsul ramas este 9000, fara date.
        if (selectingApplet()) {
            return;
        }

        byte[] buffer = apdu.getBuffer();
        byte ins = buffer[ISO7816.OFFSET_INS];

        if (ins == (byte) 0xB0) {
            citeste(apdu, buffer);
        } else if (ins == (byte) 0xD6) {
            scrie(apdu, buffer);
        } else {
            // Orice alta instructiune: 6D00, memoria ramane neschimbata.
            short sw = ISO7816.SW_INS_NOT_SUPPORTED;
            ISOException.throwIt(sw);
        }
    }

    // Copiaza octetii salvati in raspuns. Gol: trimite lungime 0 si 9000.
    private void citeste(APDU apdu, byte[] buffer) {
        if (dataLength > 0) {
            Util.arrayCopy(memory, (short) 0, buffer, (short) 0, dataLength);
        }
        apdu.setOutgoingAndSend((short) 0, dataLength);
    }

    // Lc peste 128 -> 6700, fara modificare. Altfel copiaza si lungimea in aceeasi tranzactie.
    private void scrie(APDU apdu, byte[] buffer) {
        short primiti = apdu.setIncomingAndReceive();
        if (primiti > (short) 128) {
            short sw = ISO7816.SW_WRONG_LENGTH;
            ISOException.throwIt(sw);
        }

        JCSystem.beginTransaction();
        Util.arrayCopy(buffer, ISO7816.OFFSET_CDATA, memory, (short) 0, primiti);
        dataLength = primiti;
        JCSystem.commitTransaction();
    }
}
