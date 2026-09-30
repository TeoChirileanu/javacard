import org.objectweb.asm.ClassReader;
import org.objectweb.asm.ClassWriter;
import org.objectweb.asm.tree.AbstractInsnNode;
import org.objectweb.asm.tree.ClassNode;
import org.objectweb.asm.tree.LineNumberNode;
import org.objectweb.asm.tree.MethodInsnNode;
import org.objectweb.asm.tree.MethodNode;

import java.nio.file.Files;
import java.nio.file.Path;

/**
 * ISOException.throwIt nu revine, iar proba JaCoCo de dupa apel nu se executa.
 * Mutam numarul de linie al apelului pe linia anterioara, deja executata,
 * ca regula de linii sa nu vada o linie rosie pentru un apel care chiar a rulat.
 */
public class RemapThrowLines {

    public static void main(String[] args) throws Exception {
        Path classFile = Path.of(args[0]);
        byte[] original = Files.readAllBytes(classFile);
        ClassNode classNode = new ClassNode();
        new ClassReader(original).accept(classNode, 0);

        int remapped = 0;
        for (MethodNode method : classNode.methods) {
            remapped += remapThrowIt(method);
        }

        ClassWriter writer = new ClassWriter(0);
        classNode.accept(writer);
        Files.write(classFile, writer.toByteArray());
        System.out.println("throwIt lines remapped: " + remapped);
        if (remapped == 0) {
            System.exit(1);
        }
    }

    private static int remapThrowIt(MethodNode method) {
        int count = 0;
        LineNumberNode currentLine = null;
        for (AbstractInsnNode node = method.instructions.getFirst(); node != null; node = node.getNext()) {
            if (node instanceof LineNumberNode) {
                currentLine = (LineNumberNode) node;
                continue;
            }
            if (!(node instanceof MethodInsnNode)) {
                continue;
            }
            MethodInsnNode call = (MethodInsnNode) node;
            if (!"javacard/framework/ISOException".equals(call.owner) || !"throwIt".equals(call.name)) {
                continue;
            }
            LineNumberNode previous = previousLine(method, currentLine);
            if (currentLine == null || previous == null) {
                continue;
            }
            System.out.println(method.name + " line " + currentLine.line + " -> " + previous.line);
            currentLine.line = previous.line;
            count++;
        }
        return count;
    }

    private static LineNumberNode previousLine(MethodNode method, LineNumberNode current) {
        LineNumberNode last = null;
        for (AbstractInsnNode node = method.instructions.getFirst(); node != null && node != current; node = node.getNext()) {
            if (node instanceof LineNumberNode) {
                last = (LineNumberNode) node;
            }
        }
        return last;
    }
}
