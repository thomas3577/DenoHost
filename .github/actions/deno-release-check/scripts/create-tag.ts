#!/usr/bin/env -S deno run --allow-net --allow-env --allow-run

interface GitTag {
  name: string;
}

interface PreReleaseInfo {
  type: string;
  number: number;
}

async function fetchGitTags(): Promise<string[]> {
  console.log("🔍 Fetching DenoHost tags for pre-release analysis...");

  try {
    const response = await fetch(
      "https://api.github.com/repos/thomas3577/DenoHost/tags",
    );

    if (!response.ok) {
      throw new Error(`GitHub API request failed: ${response.status}`);
    }

    const tags: GitTag[] = await response.json();
    return tags.map((tag) => tag.name);
  } catch (error) {
    console.error(`❌ Failed to fetch tags: ${error}`);
    return [];
  }
}

async function runCommand(cmd: string[]): Promise<string> {
  const process = new Deno.Command(cmd[0], {
    args: cmd.slice(1),
    stdout: "piped",
    stderr: "piped",
  });

  const { code, stdout, stderr } = await process.output();

  if (code !== 0) {
    const errorText = new TextDecoder().decode(stderr);
    throw new Error(`Command failed: ${cmd.join(" ")}\n${errorText}`);
  }

  return new TextDecoder().decode(stdout).trim();
}

function parsePreReleaseTypes(tags: string[]): PreReleaseInfo[] {
  const preReleaseTypes = ["alpha", "beta", "rc", "preview"];
  const preReleases: PreReleaseInfo[] = [];

  for (const tag of tags) {
    for (const type of preReleaseTypes) {
      const pattern = new RegExp(`${type}\\.(\\d+)$`);
      const match = tag.match(pattern);
      if (match) {
        preReleases.push({
          type,
          number: parseInt(match[1], 10),
        });
      }
    }
  }

  return preReleases;
}

async function setupGit(): Promise<void> {
  console.log("🔧 Setting up Git configuration...");

  await runCommand([
    "git",
    "config",
    "--global",
    "user.email",
    "github-actions[bot]@users.noreply.github.com",
  ]);
  await runCommand([
    "git",
    "config",
    "--global",
    "user.name",
    "github-actions[bot]",
  ]);
}

async function initializeRepo(): Promise<void> {
  const ghToken = Deno.env.get("GH_TOKEN");
  if (!ghToken) {
    throw new Error("GH_TOKEN environment variable not set");
  }

  console.log("📦 Initializing repository...");

  // Check if we're already in a git repository
  try {
    await runCommand(["git", "rev-parse", "--git-dir"]);
    console.log("🔍 Already in a git repository");
  } catch {
    console.log("🆕 Initializing new git repository");
    await runCommand(["git", "init"]);
  }

  // Check if origin remote already exists
  try {
    await runCommand(["git", "remote", "get-url", "origin"]);
    console.log("🔗 Origin remote already exists");

    // Update the remote URL with token for authentication
    await runCommand([
      "git",
      "remote",
      "set-url",
      "origin",
      `https://x-access-token:${ghToken}@github.com/thomas3577/DenoHost.git`,
    ]);
  } catch {
    console.log("➕ Adding origin remote");
    await runCommand([
      "git",
      "remote",
      "add",
      "origin",
      `https://x-access-token:${ghToken}@github.com/thomas3577/DenoHost.git`,
    ]);
  }

  await runCommand(["git", "fetch", "origin", "main"]);
}

async function createAndPushTag(tag: string): Promise<void> {
  console.log(`🏷️ Creating and pushing tag: ${tag}`);

  await runCommand(["git", "tag", tag, "origin/main"]);
  await runCommand(["git", "push", "origin", tag]);
}

async function main() {
  const tagCore = Deno.env.get("TAG_CORE");
  const preReleaseType = Deno.env.get("PRERELEASE_TYPE") || "alpha";

  if (!tagCore) {
    console.error("❌ TAG_CORE environment variable not set");
    Deno.exit(1);
  }

  if (tagCore === "null" || tagCore === "undefined" || tagCore.trim() === "") {
    console.error(`❌ Invalid TAG_CORE value: '${tagCore}'`);
    Deno.exit(1);
  }

  console.log(`🏷️ Creating new tag for Deno version: ${tagCore}`);

  try {
    await setupGit();
    await initializeRepo();

    const gitTags = await fetchGitTags();
    const preReleases = parsePreReleaseTypes(gitTags);

    let nextNumber = 1;
    let finalPreReleaseType = preReleaseType;

    if (preReleaseType && preReleaseType !== "auto") {
      // Use specified pre-release type
      const existingOfType = preReleases
        .filter((pr) => pr.type === preReleaseType)
        .map((pr) => pr.number);

      if (existingOfType.length > 0) {
        nextNumber = Math.max(...existingOfType) + 1;
        console.log(
          `Highest existing ${preReleaseType} version: ${preReleaseType}.${
            Math.max(...existingOfType)
          }`,
        );
      } else {
        console.log(
          `No existing ${preReleaseType} versions found, starting with ${preReleaseType}.1`,
        );
      }
    } else if (preReleases.length > 0) {
      // Auto-detect: find the type with the highest number
      const highest = preReleases.reduce((prev, current) =>
        prev.number > current.number ? prev : current
      );
      finalPreReleaseType = highest.type;
      nextNumber = highest.number + 1;
      console.log(
        `Highest existing pre-release version: ${highest.type}.${highest.number}`,
      );
    } else {
      console.log(
        "No existing pre-release versions found, starting with alpha.1",
      );
    }

    const newTag = `v${tagCore}-${finalPreReleaseType}.${nextNumber}`;
    console.log(
      `Next pre-release version: ${finalPreReleaseType}.${nextNumber}`,
    );

    await createAndPushTag(newTag);

    // Set GitHub Actions outputs
    const outputFile = Deno.env.get("GITHUB_OUTPUT");
    if (outputFile) {
      await Deno.writeTextFile(outputFile, `new_tag=${newTag}\n`, {
        append: true,
      });
    }

    console.log(`✅ Successfully created tag: ${newTag}`);
  } catch (error) {
    console.error(`❌ Error creating tag: ${error}`);
    Deno.exit(1);
  }
}

if (import.meta.main) {
  await main();
}
