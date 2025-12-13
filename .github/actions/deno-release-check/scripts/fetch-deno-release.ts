#!/usr/bin/env -S deno run --allow-net --allow-run

interface DenoRelease {
  tag_name: string;
}

async function fetchLatestDenoReleaseFromGitHub(): Promise<string | null> {
  console.log('Fetching latest Deno release from GitHub API...');

  try {
    const headers: Record<string, string> = {
      'Accept': 'application/vnd.github.v3+json',
      'User-Agent': 'DenoHost-Release-Check',
    };

    // Add GitHub token if available to avoid rate limiting
    const githubToken = Deno.env.get('GH_TOKEN') || Deno.env.get('GITHUB_TOKEN');
    if (githubToken) {
      headers['Authorization'] = `token ${githubToken}`;
    }

    const response = await fetch('https://api.github.com/repos/denoland/deno/releases/latest', {
      headers,
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`GitHub API request failed: ${response.status} - ${errorText}`);
    }

    const data: DenoRelease = await response.json();

    if (!data.tag_name) {
      throw new Error('No tag_name found in release data');
    }

    return data.tag_name;
  } catch (error) {
    console.error(`GitHub API failed: ${error}`);
    return null;
  }
}

async function fetchLatestDenoReleaseFromDeno(): Promise<string | null> {
  try {
    console.log('Trying to get version from installed Deno...');

    const versionOutput = await new Deno.Command('deno', {
      args: ['--version'],
      stdout: 'piped',
      stderr: 'piped',
    }).output();

    if (versionOutput.success) {
      const versionText = new TextDecoder().decode(versionOutput.stdout);
      const match = versionText.match(/deno (\d+\.\d+\.\d+)/);
      if (match) {
        console.log(`Found installed Deno version: v${match[1]}`);
        return `v${match[1]}`;
      }
    }
  } catch (fallbackError) {
    console.error(`Failed to get version from deno command: ${fallbackError}`);
  }
  return null;
}

async function fetchLatestDenoRelease(): Promise<string> {
  // Try GitHub API first
  let version = await fetchLatestDenoReleaseFromGitHub();

  // If GitHub API fails, try getting version from installed deno
  if (!version) {
    version = await fetchLatestDenoReleaseFromDeno();
  }

  if (!version) {
    console.error('All methods to fetch Deno version failed');
    Deno.exit(1);
  }

  return version;
}

async function main() {
  const fullTag = await fetchLatestDenoRelease();
  console.log(`Latest Deno release: ${fullTag}`);

  // Remove leading 'v' if present
  const tagCore = fullTag.replace(/^v/, '');

  if (!tagCore || tagCore === 'null' || tagCore === 'undefined') {
    console.error(`Invalid tag core extracted: '${tagCore}' from '${fullTag}'`);
    Deno.exit(1);
  }

  // Set GitHub Actions outputs
  const outputFile = Deno.env.get('GITHUB_OUTPUT');
  if (outputFile) {
    await Deno.writeTextFile(outputFile, `tag_core=${tagCore}\nfull_tag=${fullTag}\n`, {
      append: true,
    });
  }

  console.log(`Deno release info extracted: ${tagCore}`);
}

if (import.meta.main) {
  await main();
}
