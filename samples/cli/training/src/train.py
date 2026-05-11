from __future__ import annotations

import argparse
import json
import random
from pathlib import Path
from time import time

import mlflow


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Print the provided JSONL input folder contents to stdout."
    )
    parser.add_argument(
        "--input-folder",
        required=True,
        help="Path to the mounted JSONL input folder.",
    )
    parser.add_argument(
        "--input-file-name",
        default="sample.jsonl",
        help="JSONL file name to read from the mounted input folder.",
    )
    parser.add_argument(
        "--result-file",
        required=True,
        help="Path to the output folder where result_file will be written.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    input_folder = Path(args.input_folder).expanduser()
    if not input_folder.exists():
        raise FileNotFoundError(f"Input folder does not exist: {input_folder}")
    if not input_folder.is_dir():
        raise NotADirectoryError(f"Expected a folder path but got a file: {input_folder}")

    input_path = input_folder / args.input_file_name
    if not input_path.exists():
        raise FileNotFoundError(
            f"Input file does not exist under the mounted folder: {input_path}"
        )

    print(f"Reading input file from folder: {input_path}")
    print("=== BEGIN RAW INPUT ===")
    raw_text = input_path.read_text(encoding="utf-8")
    print(raw_text.rstrip())
    print("=== END RAW INPUT ===")

    print("=== BEGIN PARSED JSONL ===")
    for line_number, line in enumerate(raw_text.splitlines(), start=1):
        if not line.strip():
            continue
        record = json.loads(line)
        print(f"Line {line_number}:")
        print(json.dumps(record, indent=2, sort_keys=True))
    print("=== END PARSED JSONL ===")

    # ── MLflow logging ─────────────────────────────────────────────────
    print("=== BEGIN MLFLOW LOGGING ===")
    mlflow.autolog(disable=True)

    num_epochs = 5
    for epoch in range(1, num_epochs + 1):
        loss = 1.0 / epoch + random.uniform(-0.05, 0.05)
        accuracy = 1.0 - loss / 2.0
        mlflow.log_metrics({"loss": loss, "accuracy": accuracy}, step=epoch)
        print(f"  Epoch {epoch}: loss={loss:.4f}  accuracy={accuracy:.4f}")

    mlflow.log_params({
        "epochs": num_epochs,
        "learning_rate": 0.001,
        "batch_size": 32,
        "optimizer": "adam",
    })
    mlflow.log_metric("final_loss", loss)
    mlflow.log_metric("final_accuracy", accuracy)
    print("=== END MLFLOW LOGGING ===")

    # ── Write result_file output ───────────────────────────────────────
    output_folder = Path(args.result_file).expanduser()
    output_folder.mkdir(parents=True, exist_ok=True)
    result_path = output_folder / "result_file"
    result_path.write_text(
        json.dumps(
            {
                "final_loss": loss,
                "final_accuracy": accuracy,
                "epochs": num_epochs,
                "input_file": str(input_path),
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    print(f"Wrote result file to: {result_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
