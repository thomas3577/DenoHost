#!/usr/bin/env -S deno run --allow-net

interface DenoRelease {
  tag_name: string;
}

async function fetchLatestDenoRelease(): Promise<string> {
  console.log("🔍 Fetching latest Deno release...");

  try {
    const response = await fetch(
      "https://api.github.com/repos/denoland/deno/releases/latest",
    );

    if (!response.ok) {
      throw new Error(`GitHub API request failed: ${response.status}`);
    }

    const data: DenoRelease = await response.json();

    if (!data.tag_name) {
      throw new Error("No tag_name found in release data");
    }

    return data.tag_name;
  } catch (error) {
    console.error(`❌ Failed to fetch Deno release: ${error}`);
    Deno.exit(1);
  }
}

async function main() {
  const fullTag = await fetchLatestDenoRelease();
  console.log(`Latest Deno release: ${fullTag}`);

  // Remove leading 'v' if present
  const tagCore = fullTag.replace(/^v/, "");

  // Set GitHub Actions outputs
  const outputFile = Deno.env.get("GITHUB_OUTPUT");
  if (outputFile) {
    await Deno.writeTextFile(
      outputFile,
      `tag_core=${tagCore}\nfull_tag=${fullTag}\n`,
      { append: true },
    );
  }

  console.log(`✅ Deno release info extracted: ${tagCore}`);
}

if (import.meta.main) {
  await main();
}
