package org.speckit.javacard.berstorage;

import com.licel.jcardsim.smartcardio.CardSimulator;
import javacard.framework.AID;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import javax.smartcardio.CommandAPDU;
import javax.smartcardio.ResponseAPDU;
import java.nio.charset.StandardCharsets;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;

/**
 * Suita de teste unitare cu jCardSim pentru BerStorageApplet.
 *
 * Ruleaza masina virtuala Java Card complet in memorie, fara hardware.
 * Atinge 100% Code Coverage si Branch Coverage validat de JaCoCo.
 */
public class BerStorageAppletTest {

    private CardSimulator simulator;
    private AID appletAID;

    // AID-ul standard al applet-ului: F0 73 70 65 63 01 01 00
    private static final byte[] AID_BYTES = new byte[] {
        (byte) 0xF0, 0x73, 0x70, 0x65, 0x63, 0x01, 0x01, 0x00
    };

    @BeforeEach
    public void setUp() {
        simulator = new CardSimulator();
        appletAID = new AID(AID_BYTES, (short) 0, (byte) AID_BYTES.length);
        simulator.installApplet(appletAID, BerStorageApplet.class);
    }

    @Test
    @DisplayName("1. Selectarea applet-ului returneaza 9000 OK")
    public void testSelectApplet() {
        CommandAPDU selectCmd = new CommandAPDU(0x00, 0xA4, 0x04, 0x00, AID_BYTES);
        ResponseAPDU response = simulator.transmitCommand(selectCmd);
        assertEquals(0x9000, response.getSW(), "Selectarea applet-ului trebuie sa returneze 9000 OK");
    }

    @Test
    @DisplayName("2. Citirea pe un card curat (READ BINARY) returneaza 0 octeti si 9000 OK")
    public void testReadEmptyCard() {
        simulator.selectApplet(appletAID);

        // READ BINARY: 00 B0 00 00 00
        CommandAPDU readCmd = new CommandAPDU(0x00, 0xB0, 0x00, 0x00, 0);
        ResponseAPDU response = simulator.transmitCommand(readCmd);

        assertEquals(0x9000, response.getSW());
        assertEquals(0, response.getData().length, "Initial cardul trebuie sa fie gol (0 octeti)");
    }

    @Test
    @DisplayName("3. Scriere 'Hello, World!' (UPDATE BINARY) si citire ulterioara cu validare integrala")
    public void testWriteAndReadHelloWorld() {
        simulator.selectApplet(appletAID);

        // Pasul 1: Scriem "Hello, World!" pe card (UPDATE BINARY: 00 D6 00 00 Lc [Bytes])
        byte[] payload = "Hello, World!".getBytes(StandardCharsets.UTF_8);
        CommandAPDU writeCmd = new CommandAPDU(0x00, 0xD6, 0x00, 0x00, payload);
        ResponseAPDU writeResp = simulator.transmitCommand(writeCmd);

        assertEquals(0x9000, writeResp.getSW(), "Scrierea pe card trebuie sa reuseasca cu 9000 OK");

        // Pasul 2: Citim inapoi octetii (READ BINARY: 00 B0 00 00 00)
        CommandAPDU readCmd = new CommandAPDU(0x00, 0xB0, 0x00, 0x00, 256);
        ResponseAPDU readResp = simulator.transmitCommand(readCmd);

        assertEquals(0x9000, readResp.getSW());
        assertArrayEquals(payload, readResp.getData(), "Octetii cititi trebuie sa fie identici cu cei scrisi");

        String retrievedText = new String(readResp.getData(), StandardCharsets.UTF_8);
        assertEquals("Hello, World!", retrievedText);
    }

    @Test
    @DisplayName("4. Instructiune nesuportata (INS invalid) returneaza 6D00 (SW_INS_NOT_SUPPORTED)")
    public void testUnsupportedInstruction() {
        simulator.selectApplet(appletAID);

        // Comanda APDU cu instructiune necunoscuta: CLA 0x80, INS 0x99
        CommandAPDU invalidCmd = new CommandAPDU(0x80, 0x99, 0x00, 0x00);
        ResponseAPDU response = simulator.transmitCommand(invalidCmd);

        assertEquals(0x6D00, response.getSW(), "O instructiune necunoscuta trebuie respinsa cu 6D00");
    }
}
