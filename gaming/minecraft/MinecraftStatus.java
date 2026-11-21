import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;

/** Bounded, unauthenticated server-list ping only; never logs in or bypasses account checks. */
public final class MinecraftStatus {
    private static final int SOCKET_TIMEOUT_MS = 3000;
    private static final int MAX_PACKET_BYTES = 1024 * 1024;
    private static int readVarInt(InputStream in) throws IOException {
        int value = 0;
        for (int i = 0; i < 5; i++) {
            int b = in.read(); if (b < 0) throw new EOFException();
            value |= (b & 127) << (7 * i);
            if ((b & 128) == 0) return value;
        }
        throw new IOException("Oversized VarInt");
    }
    private static void writeVarInt(OutputStream out, int value) throws IOException {
        do { int b = value & 127; value >>>= 7; out.write(value == 0 ? b : b | 128); } while (value != 0);
    }
    private static void packet(OutputStream out, byte[] payload) throws IOException {
        writeVarInt(out, payload.length); out.write(payload); out.flush();
    }
    public static void main(String[] args) throws Exception {
        boolean ready = args.length > 0 && args[0].equals("--ready");
        int offset = ready ? 1 : 0;
        String host = args.length > offset ? args[offset] : "127.0.0.1";
        int port = args.length > offset + 1 ? Integer.parseInt(args[offset + 1]) : 25565;
        if (ready) {
            Path log = Path.of("/data/logs/latest.log");
            if (!Files.exists(log) || !Files.readString(log).contains("Done ("))
                throw new IOException("Server startup not complete");
        }
        try (Socket socket = new Socket()) {
            socket.connect(new InetSocketAddress(host, port), SOCKET_TIMEOUT_MS);
            socket.setSoTimeout(SOCKET_TIMEOUT_MS);
            ByteArrayOutputStream handshake = new ByteArrayOutputStream();
            writeVarInt(handshake, 0); writeVarInt(handshake, -1); // status requests work without a matching login protocol
            byte[] address = host.getBytes(StandardCharsets.UTF_8);
            writeVarInt(handshake, address.length); handshake.write(address);
            new DataOutputStream(handshake).writeShort(port); writeVarInt(handshake, 1);
            packet(socket.getOutputStream(), handshake.toByteArray());
            packet(socket.getOutputStream(), new byte[] { 0 });
            InputStream input = socket.getInputStream();
            int length = readVarInt(input);
            if (length <= 0 || length > MAX_PACKET_BYTES) throw new IOException("Invalid status length");
            byte[] payload = input.readNBytes(length);
            if (payload.length != length) throw new EOFException();
            ByteArrayInputStream response = new ByteArrayInputStream(payload);
            if (readVarInt(response) != 0) throw new IOException("Unexpected status packet");
            int textLength = readVarInt(response);
            if (textLength <= 0 || textLength != response.available()) throw new IOException("Invalid status text length");
            String json = new String(response.readNBytes(textLength), StandardCharsets.UTF_8);
            if (!json.startsWith("{") || !json.contains("\"version\"") || !json.contains("\"players\""))
                throw new IOException("Invalid status body");
            System.out.println(json);
        }
    }
}
