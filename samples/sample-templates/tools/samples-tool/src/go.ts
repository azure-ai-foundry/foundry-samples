export type Include = string | { module: string; condition?: boolean };

export function includes(...items: Include[]): string {
  let imports = "import (\n";

  const included = items.filter((i) => typeof i === "string" || i.condition).map(i => typeof i === "string" ? i : i.module);

  const system = included
    .filter((i) => !i.startsWith("github.com/") && !i.startsWith("golang.org/"))
    .sort()
    .map((i) => `\t"${i}"`)
    .join("\n");

  const nonSystem = included
    .filter((i) => i.startsWith("github.com/") || i.startsWith("golang.org/"))
    .sort()
    .map((i) => `\t"${i}"`)
    .join("\n");

  imports += [system, nonSystem].filter(i => i !== null && i.length > 0).join("\n");

  imports += "\n)";
  return imports;
}

export function valueOrEnvironment(
  indentationLevel: number,
  useEnvironmentVariable: boolean,
  variableName: string,
  environmentVariable: string,
  value?: string,
): string {
    if (!variableName) {
        console.error("Variable name must be provided.");
        process.exit(1);
    }
    const indent = "\t".repeat(indentationLevel);

    if (useEnvironmentVariable && environmentVariable) {
      return `if ${variableName} := os.Getenv("${environmentVariable}"); len(${variableName}) == 0 {\n` +
        `${indent}\tfmt.Println("Please set the ${environmentVariable} environment variable.")\n` +
        `${indent}\tos.Exit(1)\n` +
        `${indent}}\n`;
  } else if (value) {
    return `"${value}"`;
  } else {
    console.error("No value provided for variable or environment variable.");
    process.exit(1);
  }
}