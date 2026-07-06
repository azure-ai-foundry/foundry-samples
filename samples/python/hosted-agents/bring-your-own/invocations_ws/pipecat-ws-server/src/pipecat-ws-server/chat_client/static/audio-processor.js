// Shared mic capture AudioWorklet.
//
// Receives Float32 frames at the host AudioContext's sampleRate, converts to
// Int16 LE PCM, and posts ~100ms blocks to the main thread.
//
// The host is responsible for setting `chunkSamples` (samples per emitted
// chunk) via the processor options. Defaults to 1600 (= 100ms @ 16kHz).

class MicProcessor extends AudioWorkletProcessor {
    constructor(options) {
        super();
        const cfg = (options && options.processorOptions) || {};
        this._chunkSamples = cfg.chunkSamples || 1600;
        this._buffer = new Int16Array(this._chunkSamples);
        this._offset = 0;
    }

    process(inputs) {
        const input = inputs[0];
        if (!input || input.length === 0) return true;
        const channel = input[0];
        if (!channel) return true;

        for (let i = 0; i < channel.length; i++) {
            const s = Math.max(-1, Math.min(1, channel[i]));
            this._buffer[this._offset++] = s < 0 ? s * 0x8000 : s * 0x7fff;
            if (this._offset >= this._chunkSamples) {
                this.port.postMessage(this._buffer.buffer, [this._buffer.buffer]);
                this._buffer = new Int16Array(this._chunkSamples);
                this._offset = 0;
            }
        }
        return true;
    }
}

registerProcessor("mic-processor", MicProcessor);
