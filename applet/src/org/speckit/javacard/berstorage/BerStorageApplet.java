package org.speckit.javacard.berstorage;

import javacard.framework.APDU;
import javacard.framework.Applet;
import javacard.framework.ISO7816;
import javacard.framework.ISOException;
import javacard.framework.JCSystem;
import javacard.framework.OwnerPIN;
import javacard.framework.Util;

/**
 * Java Card 3.0.5 Reference Applet for Hierarchical BER-TLV Storage.
 *
 * Adheres strictly to the project Constitution:
 * - Principle II: Zero-Allocation in process()
 * - Transient RAM strictly bounded within 3.2 KB
 * - Persistent storage pre-allocated in install()
 * - PIN verified via OwnerPIN
 */
public class BerStorageApplet extends Applet {

    // Status Words (ISO 7816-4)
    public static final short SW_SECURITY_STATUS_NOT_SATISFIED = (short) 0x6982;
    public static final short SW_AUTHENTICATION_BLOCKED        = (short) 0x6983;
    public static final short SW_INCORRECT_DATA_FIELD          = (short) 0x6A80;
    public static final short SW_NOT_ENOUGH_MEMORY             = (short) 0x6A84;
    public static final short SW_RECORD_NOT_FOUND              = (short) 0x6A88;

    // Supported APDU Instructions
    public static final byte INS_VERIFY_PIN                    = (byte) 0x20;
    public static final byte INS_GET_DATA                      = (byte) 0xCA;
    public static final byte INS_PUT_DATA                      = (byte) 0xDA;
    public static final byte INS_DELETE_DATA                   = (byte) 0xE4;

    // Storage Dimensions
    public static final short STORAGE_CAPACITY                 = (short) 4096;
    public static final short SCRATCHPAD_CAPACITY              = (short) 256;

    // PIN Policy
    public static final byte PIN_TRY_LIMIT                     = (byte) 3;
    public static final byte PIN_MAX_SIZE                      = (byte) 8;

    // Persistent storage array (NVM)
    private byte[] storageBuffer;
    private short storageSize;

    // Transient RAM scratchpad (CLEAR_ON_DESELECT, zero heap allocation at runtime)
    private byte[] scratchpad;

    // PIN Credential gating protected BER-TLV branches
    private OwnerPIN pin;

    protected BerStorageApplet(byte[] bArray, short bOffset, byte bLength) {
        // Pre-allocate persistent Flash NVM storage buffer
        storageBuffer = new byte[STORAGE_CAPACITY];
        storageSize = 0;

        // Initialize Storage Header: Magic 'TL', Version 0x01
        storageBuffer[0] = (byte) 0x54; // 'T'
        storageBuffer[1] = (byte) 0x4C; // 'L'
        storageBuffer[2] = (byte) 0x01; // Version 1
        storageBuffer[3] = (byte) 0x00; // Flags
        storageSize = 4;

        // Pre-allocate transient RAM scratchpad
        scratchpad = JCSystem.makeTransientByteArray(SCRATCHPAD_CAPACITY, JCSystem.CLEAR_ON_DESELECT);

        // Initialize OwnerPIN with default "1234"
        pin = new OwnerPIN(PIN_TRY_LIMIT, PIN_MAX_SIZE);
        byte[] defaultPin = new byte[] { (byte) 0x31, (byte) 0x32, (byte) 0x33, (byte) 0x34 };
        pin.update(defaultPin, (short) 0, (byte) defaultPin.length);

        register(bArray, (short) (bOffset + 1), bArray[bOffset]);
    }

    public static void install(byte[] bArray, short bOffset, byte bLength) {
        new BerStorageApplet(bArray, bOffset, bLength);
    }

    public boolean select() {
        // Clear pin validation state on select
        pin.reset();
        return true;
    }

    public void deselect() {
        // Reset PIN validation on applet deselect
        pin.reset();
    }

    public void process(APDU apdu) {
        if (selectingApplet()) {
            byte[] buffer = apdu.getBuffer();
            // Return storage header info on select
            Util.arrayCopyNonReentrant(storageBuffer, (short) 0, buffer, (short) 0, (short) 4);
            // Append free memory size
            short freeMem = (short) (STORAGE_CAPACITY - storageSize);
            buffer[4] = (byte) (freeMem >> 8);
            buffer[5] = (byte) (freeMem & 0xFF);
            apdu.setOutgoingAndSend((short) 0, (short) 6);
            return;
        }

        byte[] buffer = apdu.getBuffer();
        byte ins = buffer[ISO7816.OFFSET_INS];

        switch (ins) {
            case INS_VERIFY_PIN:
                processVerifyPin(apdu);
                break;
            case INS_GET_DATA:
                processGetData(apdu);
                break;
            case INS_PUT_DATA:
                processPutData(apdu);
                break;
            default:
                ISOException.throwIt(ISO7816.SW_INS_NOT_SUPPORTED);
        }
    }

    private void processVerifyPin(APDU apdu) {
        byte[] buffer = apdu.getBuffer();
        byte lc = buffer[ISO7816.OFFSET_LC];
        short bytesRead = apdu.setIncomingAndReceive();

        if (bytesRead != lc) {
            ISOException.throwIt(ISO7816.SW_WRONG_LENGTH);
        }

        if (!pin.check(buffer, ISO7816.OFFSET_CDATA, lc)) {
            byte remaining = pin.getTriesRemaining();
            if (remaining == 0) {
                ISOException.throwIt(SW_AUTHENTICATION_BLOCKED);
            }
            ISOException.throwIt((short) (0x63C0 | (remaining & 0x0F)));
        }
    }

    private void processGetData(APDU apdu) {
        byte[] buffer = apdu.getBuffer();
        byte p1 = buffer[ISO7816.OFFSET_P1];
        byte p2 = buffer[ISO7816.OFFSET_P2];

        // Security Check: Protected tag ranges (0xEFxx, 0xBFxx) require PIN validation
        if (isProtectedTag(p1, p2)) {
            if (!pin.isValidated()) {
                ISOException.throwIt(SW_SECURITY_STATUS_NOT_SATISFIED);
            }
        }

        // Search for tag in pre-allocated storageBuffer
        short offset = findTagOffset(p1, p2);
        if (offset < 0) {
            ISOException.throwIt(SW_RECORD_NOT_FOUND);
        }

        short len = getTlvTotalLength(offset);
        Util.arrayCopyNonReentrant(storageBuffer, offset, buffer, (short) 0, len);
        apdu.setOutgoingAndSend((short) 0, len);
    }

    private void processPutData(APDU apdu) {
        byte[] buffer = apdu.getBuffer();
        byte p1 = buffer[ISO7816.OFFSET_P1];
        byte p2 = buffer[ISO7816.OFFSET_P2];
        byte lc = buffer[ISO7816.OFFSET_LC];

        // Security Check: Protected tags require PIN validation
        if (isProtectedTag(p1, p2)) {
            if (!pin.isValidated()) {
                ISOException.throwIt(SW_SECURITY_STATUS_NOT_SATISFIED);
            }
        }

        short bytesRead = apdu.setIncomingAndReceive();
        if (bytesRead != lc) {
            ISOException.throwIt(ISO7816.SW_WRONG_LENGTH);
        }

        // Check buffer boundary
        if ((short) (storageSize + bytesRead) > STORAGE_CAPACITY) {
            ISOException.throwIt(SW_NOT_ENOUGH_MEMORY);
        }

        // Atomic update transaction
        JCSystem.beginTransaction();
        try {
            Util.arrayCopy(buffer, ISO7816.OFFSET_CDATA, storageBuffer, storageSize, bytesRead);
            storageSize += bytesRead;
            JCSystem.commitTransaction();
        } catch (Exception e) {
            JCSystem.abortTransaction();
            ISOException.throwIt(SW_INCORRECT_DATA_FIELD);
        }
    }

    private boolean isProtectedTag(byte p1, byte p2) {
        // Tag class Context/Private with bit 5 set (e.g. 0xEF, 0xBF)
        return (p1 == (byte) 0xEF || p1 == (byte) 0xBF);
    }

    private short findTagOffset(byte p1, byte p2) {
        short curr = 4; // Start past Storage Header
        while (curr < storageSize) {
            if (storageBuffer[curr] == p1 && storageBuffer[(short) (curr + 1)] == p2) {
                return curr;
            }
            short tlvLen = getTlvTotalLength(curr);
            if (tlvLen <= 0) break;
            curr += tlvLen;
        }
        return -1;
    }

    private short getTlvTotalLength(short offset) {
        if (offset >= storageSize) return 0;
        short tagLen = 2; // Default 2-byte tag
        short lenOffset = (short) (offset + tagLen);
        byte b0 = storageBuffer[lenOffset];
        if (b0 >= 0) {
            // 1-byte length
            return (short) (tagLen + 1 + b0);
        } else if (b0 == (byte) 0x81) {
            short valLen = (short) (storageBuffer[(short) (lenOffset + 1)] & 0xFF);
            return (short) (tagLen + 2 + valLen);
        } else if (b0 == (byte) 0x82) {
            short valLen = Util.getShort(storageBuffer, (short) (lenOffset + 1));
            return (short) (tagLen + 3 + valLen);
        }
        return 0;
    }
}
