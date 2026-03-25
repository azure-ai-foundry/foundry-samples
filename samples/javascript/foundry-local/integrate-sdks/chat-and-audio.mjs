/**
 * Foundry Local example combining chat and audio transcription in one app.
 * A single FoundryLocalManager can manage both chat and audio models simultaneously.
 */

// <chat_and_audio>
import { FoundryLocalManager } from "foundry-local-sdk";

const manager = FoundryLocalManager.create({ appName: "foundry_local_samples" });

// Load both models
const chatModel = await manager.catalog.getModel("phi-3.5-mini");
await chatModel.download();
await chatModel.load();

const whisperModel = await manager.catalog.getModel("whisper-tiny");
await whisperModel.download();
await whisperModel.load();

// Step 1: Transcribe audio
const audioClient = whisperModel.createAudioClient();
audioClient.settings.language = "en";
const transcription = await audioClient.transcribe("recording.wav");
console.log("You said:", transcription.text);

// Step 2: Analyze with chat model
const chatClient = chatModel.createChatClient();
const analysis = await chatClient.completeChat([
  { role: "system", content: "Summarize the following text." },
  { role: "user", content: transcription.text },
]);
console.log("Summary:", analysis.choices[0].message.content);

// Clean up
await chatModel.unload();
await whisperModel.unload();
// </chat_and_audio>
