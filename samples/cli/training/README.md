# Training Job Templates

This directory contains starter templates for submitting training jobs in Azure AI Foundry using the Azure Developer CLI (azd) training extension (`azure.ai.training`).

The goal is a code-first, automatable CLI workflow for custom training jobs in Foundry, optimized for ML engineers and data scientists who prefer terminal workflows and need repeatable execution.

When you run `azd ai training init` with a template flag, these templates are pulled locally to provide sample configurations for your training jobs:

```bash
azd ai training init -t <template-url>
```

## Templates

| Template | Link | Description |
|----------|------|-------------|
| Hello World | [sample_hello_world.yaml](sample_hello_world.yaml) | Minimal `commandJob` template that echoes `"hello world"`. Useful as a starting point to validate your compute and environment setup. |
| Custom Training Job | [sample_training_job.yaml](sample_training_job.yaml) | End-to-end `commandJob` that runs a Python training script ([src/train.py](src/train.py)) over a JSONL dataset ([train_data/sample.jsonl](train_data/sample.jsonl)) and writes a result file output. |

## Layout

```
training/
├── sample_hello_world.yaml     # Minimal job
├── sample_training_job.yaml    # Custom code training job
├── src/
│   └── train.py                # Training script invoked by sample_training_job.yaml
└── train_data/
    └── sample.jsonl            # Sample input data mounted as the train_data input
```

## Placeholders

Templates use `<user to add>` for fields that depend on the user's workspace or job requirements, such as `identity`, `compute`, `environment`, `instance_type`, `slaTier`, and `priority`. Replace these values before submitting the job.