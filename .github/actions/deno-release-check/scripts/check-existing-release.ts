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
    return branches.map((branch) => branch.name);
  } catch (error) {
    console.error(`Failed to fetch branches: ${error}`);
    return [];
  }
}

async function checkCurrentVersion(): Promise<string | null> {
  console.log('Checking current Deno version in Directory.Build.props...');

  try {
    const workspace = Deno.env.get('GITHUB_WORKSPACE') || Deno.cwd();
    const filePath = `${workspace}/Directory.Build.props`;

    const content = await Deno.readTextFile(filePath);
    const match = content.match(/<DenoVersion>([\d.]+)<\/DenoVersion>/);

    if (match && match[1]) {
      console.log(`Current Deno version in repository: ${match[1]}`);
      return match[1];
    }

    console.log('Could not find DenoVersion in Directory.Build.props');
    return null;
  } catch (error) {
    console.error(`Failed to read Directory.Build.props: ${error}`);
    return null;
  }
}

async function main() {
  const denoVersion = Deno.env.get('DENO_VERSION');
  if (!denoVersion) {
    console.error('DENO_VERSION environment variable not set');
    Deno.exit(1);
  }

  console.log(`Checking for existing PR/branch for Deno version: ${denoVersion}`);

  // Check if version is already in the repository
  const currentVersion = await checkCurrentVersion();
  if (currentVersion === denoVersion) {
    console.log(`Deno version ${denoVersion} is already present in Directory.Build.props. Nothing to do.`);

    const outputFile = Deno.env.get('GITHUB_OUTPUT');
    if (outputFile) {
      await Deno.writeTextFile(outputFile, `already_exists=true\n`, {
        append: true,
      });
    }

    Deno.exit(0);
  }

  const [existingPRs, existingBranches] = await Promise.all([
    fetchExistingPRs(),
    fetchExistingBranches(),
  ]);

  console.log('Existing open PRs:');
  existingPRs.forEach((pr) => {
    console.log(`  #${pr.number}: ${pr.title} (${pr.head.ref})`);
  });

  console.log('Existing branches:');
  existingBranches.forEach((branch) => {
    console.log(`  ${branch}`);
  });

  // Check if any PR or branch exists for this Deno version
  const branchPattern = `release/v${denoVersion}`;
  const prPattern = new RegExp(`update.*deno.*v?${denoVersion.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`, 'i');
  const foundPR = existingPRs.find((pr) => prPattern.test(pr.title) || pr.head.ref === branchPattern);
  const foundBranch = existingBranches.find((branch) => branch === branchPattern);
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
