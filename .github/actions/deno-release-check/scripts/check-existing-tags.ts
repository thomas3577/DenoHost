#!/usr/bin/env -S deno run --allow-net

interface GitTag {
  name: string;
}

async function fetchGitTags(): Promise<string[]> {
  console.log('🔍 Fetching DenoHost tags...');

  try {
    const response = await fetch(
      'https://api.github.com/repos/thomas3577/DenoHost/tags',
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

async function main() {
  const tagCore = Deno.env.get('TAG_CORE');
  if (!tagCore) {
    console.error('❌ TAG_CORE environment variable not set');
    Deno.exit(1);
  }

  console.log(`🔍 Checking existing tags for Deno version: ${tagCore}`);

  const gitTags = await fetchGitTags();

  console.log('Existing tags in DenoHost:');
  console.log(gitTags.join('\n'));

  // Check if any tag exists for this Deno version
  const pattern = new RegExp(
    `^v${tagCore.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(-|$)`,
  );
  const found = gitTags.find((tag) => pattern.test(tag));

  const alreadyExists = found ? 'true' : 'false';

  // Set GitHub Actions outputs
  const outputFile = Deno.env.get('GITHUB_OUTPUT');
  if (outputFile) {
    await Deno.writeTextFile(
      outputFile,
      `already_exists=${alreadyExists}\n`,
      { append: true },
    );
  }

  if (found) {
    console.log(`✅ Already released: ${found}`);
  } else {
    console.log(`⚠️ No existing tag for v${tagCore}`);
  }
}

if (import.meta.main) {
  await main();
}
