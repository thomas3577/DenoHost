#!/usr/bin/env -S deno run --allow-net --allow-env --env-file

interface GitHubPR {
  number: number;
  title: string;
  head: {
    ref: string;
  };
  state: string;
}

interface GitHubBranch {
  name: string;
}

async function fetchExistingPRs(): Promise<GitHubPR[]> {
  console.log('Fetching existing pull requests...');

  try {
    const githubToken = Deno.env.get('GH_TOKEN');
    const headers: Record<string, string> = {
      'Accept': 'application/vnd.github.v3+json',
      'User-Agent': 'DenoHost-Release-Check',
    };

    if (githubToken) {
      headers['Authorization'] = `token ${githubToken}`;
    }

    const response = await fetch('https://api.github.com/repos/thomas3577/DenoHost/pulls?state=open', {
      headers,
    });

    if (!response.ok) {
      throw new Error(`GitHub API request failed: ${response.status}`);
    }

    const prs: GitHubPR[] = await response.json();
    return prs;
  } catch (error) {
    console.error(`Failed to fetch pull requests: ${error}`);
    return [];
  }
}

async function fetchExistingBranches(): Promise<string[]> {
  console.log('Fetching existing branches...');

  try {
    const githubToken = Deno.env.get('GH_TOKEN');
    const headers: Record<string, string> = {
      'Accept': 'application/vnd.github.v3+json',
      'User-Agent': 'DenoHost-Release-Check',
    };

    if (githubToken) {
      headers['Authorization'] = `token ${githubToken}`;
    }

    const response = await fetch('https://api.github.com/repos/thomas3577/DenoHost/branches', {
      headers,
    });

    if (!response.ok) {
      throw new Error(`GitHub API request failed: ${response.status}`);
    }

    const branches: GitHubBranch[] = await response.json();
    return branches.map(branch => branch.name);
  } catch (error) {
    console.error(`Failed to fetch branches: ${error}`);
    return [];
  }
}

async function main() {
  const denoVersion = Deno.env.get('DENO_VERSION');
  if (!denoVersion) {
    console.error('DENO_VERSION environment variable not set');
    Deno.exit(1);
  }

  console.log(`Checking for existing PR/branch for Deno version: ${denoVersion}`);

  const [existingPRs, existingBranches] = await Promise.all([
    fetchExistingPRs(),
    fetchExistingBranches()
  ]);

  console.log('Existing open PRs:');
  existingPRs.forEach(pr => {
    console.log(`  #${pr.number}: ${pr.title} (${pr.head.ref})`);
  });

  console.log('Existing branches:');
  existingBranches.forEach(branch => {
    console.log(`  ${branch}`);
  });

  // Check if any PR or branch exists for this Deno version
  const branchPattern = `update-deno-v${denoVersion}`;
  const prPattern = new RegExp(`update.*deno.*v?${denoVersion.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`, 'i');

  const foundPR = existingPRs.find(pr =>
    prPattern.test(pr.title) || pr.head.ref === branchPattern
  );

  const foundBranch = existingBranches.find(branch =>
    branch === branchPattern || branch.includes(`deno-v${denoVersion}`)
  );

  const alreadyExists = (foundPR || foundBranch) ? 'true' : 'false';

  // Set GitHub Actions outputs
  const outputFile = Deno.env.get('GITHUB_OUTPUT');
  if (outputFile) {
    await Deno.writeTextFile(outputFile, `already_exists=${alreadyExists}\n`, {
      append: true,
    });
  }

  if (foundPR) {
    console.log(`Found existing PR: #${foundPR.number} - ${foundPR.title}`);
  } else if (foundBranch) {
    console.log(`Found existing branch: ${foundBranch}`);
  } else {
    console.log(`No existing PR or branch found for Deno v${denoVersion}`);
  }
}

if (import.meta.main) {
  await main();
}
