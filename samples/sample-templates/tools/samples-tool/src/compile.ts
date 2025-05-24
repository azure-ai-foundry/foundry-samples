import _ from "lodash";
import fs from "fs";
import { parse } from "yaml";
import path from "path";
import { Sample } from "./interfaces";


function parseData(dataContent: string) {
  try {
    return JSON.parse(dataContent);
  } catch {
    try {
      return parse(dataContent);
    } catch {
      console.error(
        "Failed to parse data file. Please provide a valid JSON or YAML file.",
      );
      process.exit(1);
    }
  }
}

function isDirectory(path: string): boolean {
  return fs.existsSync(path) && fs.lstatSync(path).isDirectory();
}

function resolveSampleFile(samplePath: string): string {
  if (isDirectory(samplePath)) {
    return path.join(samplePath, "sample.yaml");
  }
  return samplePath;
}

function readSample(samplePath: string): Sample {
  const sampleFile = resolveSampleFile(samplePath);
  const fileContents = fs.readFileSync(sampleFile, "utf-8");
  return parse(fileContents) as Sample;
}

function createOutputDirectory(outputPath: string) {
  if (!fs.existsSync(outputPath)) {
    fs.mkdirSync(outputPath, { recursive: true });
  }
}



export function compileSample(
  samplePath: string,
  dataPath: string,
  outputPath: string,
  options: { project: boolean },
) {
  const sample = readSample(samplePath);

  sample.template
  const templateContent = fs.readFileSync(sampleFile, "utf-8");
  parse(templateContent);
  const dataContent = fs.readFileSync(dataPath, "utf-8");

  const data = parseData(dataContent);

  const compiledTemplate = _.template(templateContent);
  fs.writeFileSync(outputPath, compiledTemplate(data));
}
