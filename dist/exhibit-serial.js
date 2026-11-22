export const TOUCH_CHANNELS = ['touchA', 'touchB', 'fabric'];
export const OFF = Object.freeze({lampA: Object.freeze([0, 0, 0]), lampB: Object.freeze([0, 0, 0]), servo: null});

export function encodeExhibitOutput(sequence, {lampA, lampB, servo} = OFF) {
  const color = rgb => Array.from({length: 3}, (_, index) => {
    const value = rgb?.[index];
    return Number.isFinite(value) ? Math.max(0, Math.min(64, Math.round(value))) : 0;
  });
  const angle = Number.isFinite(servo) ? Math.max(60, Math.min(120, Math.round(servo))) : -1;
  return ['EOL3', 'SET', sequence, ...color(lampA), ...color(lampB), angle].join(' ') + '\n';
}

export function readExhibitFrame(line) {
  const state = /^EOL3 IN ([0-7]) ([0-7])$/.exec(line);
  if (state) {
    const mask = Number(state[1]), enabled = Number(state[2]);
    return {
      type: 'inputs',
      inputs: Object.fromEntries(TOUCH_CHANNELS.map((name, i) => [
        name, enabled & (1 << i) ? Boolean(mask & (1 << i)) : null,
      ])),
    };
  }
  const ack = /^EOL3 ACK (\d{1,5})$/.exec(line);
  return ack && Number(ack[1]) <= 65535 ? {type: 'ack', sequence: Number(ack[1])} : null;
}

// Visual effects can subscribe to touch/input events and call setOutputs.
// The independently armed serial connection is the only path to real outputs.
export class ExhibitSerial extends EventTarget {
  constructor(serial = globalThis.navigator?.serial) {
    super();
    this.serial = serial;
    this.sequence = 0;
    this.outputs = OFF;
    this.armed = false;
    this.ready = false;
    this.inputs = null;
  }

  emit(type, detail) {
    this.dispatchEvent(new CustomEvent(type, {detail}));
  }

  async connect() {
    if (!this.serial) throw new Error('请用桌面版 Edge 或 Chrome 打开 HTTPS 或 localhost。');
    if (this.port || this.connecting) return;
    this.connecting = true;
    try {
      this.port = await this.serial.requestPort();
      await this.port.open({baudRate: 115200});
      this.writer = this.port.writable.getWriter();
      this.reader = this.port.readable.getReader();
      this.lastInputAt = performance.now();
      this.emit('status', '串口已打开，等待 EOL3 固件');
      this.reading = this.readLoop(this.reader);
      this.timer = setInterval(() => this.tick(), 100);
    } catch (error) {
      await this.disconnect();
      throw error;
    } finally {
      this.connecting = false;
    }
  }

  async readLoop(reader) {
    const decoder = new TextDecoder();
    let pending = '', overflow = false;
    try {
      while (true) {
        const {done, value} = await reader.read();
        if (done) break;
        for (const char of decoder.decode(value, {stream: true})) {
          if (char === '\n') {
            if (!overflow) this.handleFrame(readExhibitFrame(pending.trimEnd()));
            pending = '';
            overflow = false;
          } else if (!overflow) {
            if (pending.length >= 80) overflow = true;
            else pending += char;
          }
        }
      }
    } catch {
      // A disconnected USB cable rejects pending reads.
    } finally {
      reader.releaseLock();
      if (!this.closing) queueMicrotask(() => void this.disconnect());
    }
  }

  handleFrame(frame) {
    if (!frame) return;
    if (frame.type === 'ack') {
      this.emit('ack', frame.sequence);
      return;
    }
    const previous = this.inputs;
    this.lastInputAt = performance.now();
    this.inputs = frame.inputs;
    if (!this.ready) {
      this.ready = true;
      this.emit('status', 'EOL3 已连接，可以测试输入');
    }
    this.emit('inputs', this.inputs);
    for (const source of TOUCH_CHANNELS) {
      if (previous?.[source] !== null && previous?.[source] !== undefined &&
          this.inputs[source] !== null && previous[source] !== this.inputs[source]) {
        this.emit('touch', {source, pressed: this.inputs[source]});
      }
    }
  }

  tick() {
    if (!this.port || this.closing) return;
    if (performance.now() - this.lastInputAt > 1500) {
      if (this.ready) {
        this.ready = false;
        this.inputs = null;
        this.setArmed(false);
        this.emit('inputs', null);
      }
      this.emit('status', '等待 EOL3 串口数据，请确认烧录了展台固件');
    }
    if (this.ready) this.flush();
  }

  setArmed(enabled) {
    this.armed = Boolean(enabled && this.ready);
    if (!this.armed) {
      this.outputs = OFF;
      this.flush();
    }
    this.emit('armed', this.armed);
  }

  setOutputs(outputs) {
    if (this.armed) this.outputs = outputs;
  }

  flush() {
    if (!this.writer || this.writing || this.closing || !this.ready) return;
    const packet = encodeExhibitOutput(this.sequence++ & 65535, this.armed ? this.outputs : OFF);
    this.writing = this.writer.write(new TextEncoder().encode(packet))
      .catch(() => queueMicrotask(() => void this.disconnect()))
      .finally(() => { this.writing = null; });
  }

  async disconnect() {
    if (this.closing) return;
    this.closing = true;
    clearInterval(this.timer);
    const wasReady = this.ready;
    this.armed = this.ready = false;
    this.outputs = OFF;
    this.inputs = null;
    const port = this.port;
    try {
      if (this.writer) {
        await Promise.race([this.writing, new Promise(resolve => setTimeout(resolve, 200))]);
        if (wasReady && !this.writing) {
          await Promise.race([
            this.writer.write(new TextEncoder().encode(encodeExhibitOutput(this.sequence++ & 65535))),
            new Promise(resolve => setTimeout(resolve, 200)),
          ]).catch(() => {});
        }
      }
      await this.reader?.cancel().catch(() => {});
      await this.reading;
    } finally {
      this.writer?.releaseLock();
      this.reader = this.writer = this.port = this.reading = null;
      await port?.close().catch(() => {});
      this.closing = false;
      this.emit('armed', false);
      this.emit('inputs', null);
      this.emit('status', '未连接');
    }
  }
}
