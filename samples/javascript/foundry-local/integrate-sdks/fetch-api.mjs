/**
 * Foundry Local integration example using the Fetch API.
 */

// <fetch_api_completion>
import { FoundryLocalManager } from 'foundry-local-sdk';

// Initialize the Foundry Local SDK
console.log('Initializing Foundry Local SDK...');

const config = {
    appName: 'foundry_local_samples',
    logLevel: 'info',
    webServiceUrls: 'http://localhost:5764'
};

const manager = FoundryLocalManager.create(config);
console.log('✓ SDK initialized successfully');

// Get the model object
const modelAlias = 'qwen2.5-0.5b'; // Using an available model from the list above
const model = await manager.catalog.getModel(modelAlias);

// Download the model
console.log(`\nDownloading model ${modelAlias}...`);
await model.download((progress) => {
    process.stdout.write(`\rDownloading... ${progress.toFixed(2)}%`);
});
console.log('\n✓ Model downloaded');

// Load the model
console.log(`\nLoading model ${modelAlias}...`);
await model.load();
console.log('✓ Model loaded');

// Start the web service
console.log('\nStarting web service...');
manager.startWebService();
console.log('✓ Web service started');
        
async function queryModel() {
  const response = await fetch(
    config.webServiceUrls + "/v1/chat/completions",
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        model: model.id,
        messages: [{ role: "user", content: "What is the golden ratio?" }],
      }),
    }
  );

  const data = await response.json();
  console.log(data.choices[0].message.content);
}


await queryModel();

// Tidy up
console.log('Unloading model and stopping web service...');
await model.unload();
manager.stopWebService();
console.log(`✓ Model unloaded and web service stopped`);
// </fetch_api_completion>
