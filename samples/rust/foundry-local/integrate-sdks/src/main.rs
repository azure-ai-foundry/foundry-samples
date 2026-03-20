// <integrate_sdks>
use anyhow::{Context, Result};

#[tokio::main]
async fn main() -> Result<()> {
    // Discover the Foundry Local service endpoint.
    // Foundry Local must be running (start it with `foundry model run qwen2.5-0.5b`).
    let client = reqwest::Client::new();

    let status_url = "http://127.0.0.1:57822/openai/status";
    let status: serde_json::Value = client.get(status_url)
        .send()
        .await
        .context("Foundry Local service is not running. Start it with: foundry model run qwen2.5-0.5b")?
        .json()
        .await?;

    let endpoint = status["endpoints"][0]
        .as_str()
        .context("No endpoint found in Foundry Local status")?;

    // List available models to get the model ID
    let models: serde_json::Value = client.get(format!("{}/v1/models", endpoint))
        .send()
        .await?
        .json()
        .await?;

    let model_id = models["data"][0]["id"]
        .as_str()
        .context("No model loaded. Run: foundry model run qwen2.5-0.5b")?;

    println!("Using model: {}", model_id);

    // Use the OpenAI-compatible REST API to generate a chat completion
    let response = client.post(format!("{}/v1/chat/completions", endpoint))
        .header("Content-Type", "application/json")
        .json(&serde_json::json!({
            "model": model_id,
            "messages": [{"role": "user", "content": "What is the golden ratio?"}],
        }))
        .send()
        .await?;

    let result = response.json::<serde_json::Value>().await?;
    println!("{}", result["choices"][0]["message"]["content"]);

    Ok(())
}
// </integrate_sdks>
