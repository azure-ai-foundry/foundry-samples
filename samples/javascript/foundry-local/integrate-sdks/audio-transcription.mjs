/**
 * Foundry Local audio transcription example using the native SDK API.
 * Demonstrates standard and streaming transcription with the Whisper model.
 */

// <standard_transcription>
import { FoundryLocalManager } from "foundry-local-sdk";

// Initialize the SDK
const manager = FoundryLocalManager.create({ appName: "foundry_local_samples" });

// Get the Whisper model from the catalog
const whisperModel = await manager.catalog.getModel("whisper-tiny");

// Download the model if not already cached
if (!whisperModel.isCached) {
  await whisperModel.download();
}

// Load the model into memory
await whisperModel.load();

// Create an audio client and transcribe
const audioClient = whisperModel.createAudioClient();
audioClient.settings.language = "en";

const result = await audioClient.transcribe("recording.wav");
console.log("Transcription:", result.text);

// Clean up
await whisperModel.unload();
// </standard_transcription>

// <streaming_transcription>
await audioClient.transcribeStreaming("recording.wav", (chunk) => {
  process.stdout.write(chunk.text);
});
// </streaming_transcription>
