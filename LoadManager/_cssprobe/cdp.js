// Cliente WebSocket minimo (modulos nativos) para hablar CDP con el WebView del Verifone.
const net = require('net');
const crypto = require('crypto');

const WS_PATH = process.argv[2]; // /devtools/page/XXer
const EXPR = process.argv[3];

const key = crypto.randomBytes(16).toString('base64');
const sock = net.connect(9222, 'localhost', () => {
  sock.write(
    `GET ${WS_PATH} HTTP/1.1\r\n` +
    `Host: localhost:9222\r\n` +
    `Upgrade: websocket\r\n` +
    `Connection: Upgrade\r\n` +
    `Sec-WebSocket-Key: ${key}\r\n` +
    `Sec-WebSocket-Version: 13\r\n\r\n`
  );
});

let handshakeDone = false;
let buf = Buffer.alloc(0);

function sendFrame(str) {
  const payload = Buffer.from(str, 'utf8');
  const len = payload.length;
  let header;
  const mask = crypto.randomBytes(4);
  if (len < 126) {
    header = Buffer.from([0x81, 0x80 | len]);
  } else if (len < 65536) {
    header = Buffer.from([0x81, 0x80 | 126, (len >> 8) & 255, len & 255]);
  } else {
    header = Buffer.from([0x81, 0x80 | 127, 0,0,0,0, (len>>24)&255,(len>>16)&255,(len>>8)&255,len&255]);
  }
  const masked = Buffer.alloc(len);
  for (let i = 0; i < len; i++) masked[i] = payload[i] ^ mask[i % 4];
  sock.write(Buffer.concat([header, mask, masked]));
}

function parseFrames() {
  while (buf.length >= 2) {
    const len0 = buf[1] & 127;
    let offset = 2, payloadLen = len0;
    if (len0 === 126) { payloadLen = buf.readUInt16BE(2); offset = 4; }
    else if (len0 === 127) { payloadLen = Number(buf.readBigUInt64BE(2)); offset = 10; }
    process.stderr.write(`FRAME op=0x${buf[0].toString(16)} len=${payloadLen} bufLen=${buf.length}\n`);
    if (buf.length < offset + payloadLen) break;
    const payload = buf.slice(offset, offset + payloadLen).toString('utf8');
    buf = buf.slice(offset + payloadLen);
    process.stderr.write('PAYLOAD: ' + payload.slice(0, 300) + '\n');
    try {
      const msg = JSON.parse(payload);
      if (msg.id === 1) {
        const r = msg.result && msg.result.result;
        console.log(r ? r.value : JSON.stringify(msg));
        sock.end();
        process.exit(0);
      }
    } catch (e) {}
  }
}

sock.on('data', (data) => {
  if (!handshakeDone) {
    buf = Buffer.concat([buf, data]);
    const idx = buf.indexOf('\r\n\r\n');
    if (idx === -1) return;
    handshakeDone = true;
    process.stderr.write('HANDSHAKE:\n' + buf.slice(0, idx).toString() + '\n---\n');
    buf = buf.slice(idx + 4);
    sendFrame(JSON.stringify({
      id: 1,
      method: 'Runtime.evaluate',
      params: { expression: EXPR, returnByValue: true }
    }));
    parseFrames();
  } else {
    buf = Buffer.concat([buf, data]);
    parseFrames();
  }
});
sock.on('error', (e) => { console.log('ERR', e.message); process.exit(1); });
setTimeout(() => { console.log('timeout'); process.exit(1); }, 8000);
