#!/usr/bin/env -S deno run --allow-net --allow-env --allow-run --allow-write

async function runCommand(cmd: string[]): Promise<string> {
  const process = new Deno.Command(cmd[0], {
    args: cmd.slice(1),
    stdout: 'piped',
    stderr: 'piped',
  });

  const { code, stdout, stderr } = await process.output();

  if (code !== 0) {
    const errorText = new TextDecoder().decode(stderr);
    throw new Error(`Command failed: ${cmd.join(' ')}\n${errorText}`);
  }

  return new TextDecoder().decode(stdout).trim();
}

async function setupGit(): Promise<void> {
  console.log('Setting up Git configuration...');

  await runCommand([
    'git',
    'config',
    '--global',
    'user.email',
    'github-actions[bot]@users.noreply.github.com',
  ]);

  await runCommand([
    'git',
    'config',
    '--global',
    'user.name',
    'github-actions[bot]',
  ]);
}

async function initializeRepo(): Promise<void> {
  const ghToken = Deno.env.get('GH_TOKEN');
  if (!ghToken) {
    throw new Error('GH_TOKEN environment variable not set');
  }

  console.log('Initializing repository...');

  // Check if we're already in a git repository
  try {
    await runCommand(['git', 'rev-parse', '--git-dir']);
    console.log('Already in a git repository');
  } catch {
    console.log('Initializing new git repository');
    await runCommand(['git', 'init']);
  }

  // Check if origin remote already exists
  try {
    await runCommand(['git', 'remote', 'get-url', 'origin']);
    console.log('Origin remote already exists');

    // Update the remote URL with token for authentication
    await runCommand([
      'git',
      'remote',
      'set-url',
      'origin',
      `https://x-access-token:${ghToken}@github.com/thomas3577/DenoHost.git`,
    ]);
  } catch {
    console.log('Adding origin remote');

    await runCommand([
      'git',
      'remote',
      'add',
      'origin',
      `https://x-access-token:${ghToken}@github.com/thomas3577/DenoHost.git`,
    ]);
  }

  await runCommand(['git', 'fetch', 'origin', 'main']);
}

async function createBranch(branchName: string): Promise<void> {
  console.log(`Creating branch: ${branchName}`);

  await runCommand(['git', 'checkout', '-b', branchName, 'origin/main']);
}

async function createPullRequest(branchName: string, denoVersion: string): Promise<number> {
  const ghToken = Deno.env.get('GH_TOKEN');
  if (!ghToken) {
    throw new Error('GH_TOKEN environment variable not set');
  }

  console.log(`Creating pull request for branch: ${branchName}`);

  const title = `Update Deno to v${denoVersion}`;
  const body = `🚀 **Automated Deno Update**

This pull request updates DenoHost to use Deno v${denoVersion}.

## Changes
- Update Deno version to v${denoVersion}
- This will create a new release when merged

## Next Steps
1. Review the changes
2. Test the new Deno version compatibility
3. Merge when ready to create new DenoHost release
4. Create tag manually: \`git tag v${denoVersion} && git push --tags\`

---
*This PR was created automatically by the Deno Release Check action.*`;

  try {
    const response = await fetch('https://api.github.com/repos/thomas3577/DenoHost/pulls', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${ghToken}`,
        'Accept': 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        title: title,
        body: body,
        head: branchName,
        base: 'main',
        draft: true,
      }),
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Failed to create pull request: ${response.status} ${response.statusText} - ${errorText}`);
    }

    const pr = await response.json();
    console.log(`Successfully created pull request #${pr.number}: ${title}`);
    return pr.number;
  } catch (error) {
    console.error(`Failed to create pull request: ${error}`);
    throw error;
  }
}

async function pushBranch(branchName: string): Promise<void> {
  console.log(`Pushing branch: ${branchName}`);

  // Create an empty commit to have something to push
  await runCommand(['git', 'commit', '--allow-empty', '-m', `Prepare for Deno update`]);
  await runCommand(['git', 'push', 'origin', branchName]);
}

async function main() {
  const denoVersion = Deno.env.get('DENO_VERSION');

  if (!denoVersion) {
    console.error('DENO_VERSION environment variable not set');
    Deno.exit(1);
  }

  if (denoVersion === 'null' || denoVersion === 'undefined' || denoVersion.trim() === '') {
    console.error(`Invalid DENO_VERSION value: '${denoVersion}'`);
    Deno.exit(1);
  }

  console.log(`Creating pull request for Deno version: v${denoVersion}`);

  try {
    await setupGit();
    await initializeRepo();

    const expectedTag = `v${denoVersion}`;
    const branchName = `update-deno-v${denoVersion}`;

    console.log(`Expected new tag after merge: ${expectedTag}`);

    await createBranch(branchName);
    await pushBranch(branchName);

    const prNumber = await createPullRequest(branchName, denoVersion);

    // Set GitHub Actions outputs
    const outputFile = Deno.env.get('GITHUB_OUTPUT');
    if (outputFile) {
      await Deno.writeTextFile(outputFile, `pr_number=${prNumber}\nbranch_name=${branchName}\nexpected_tag=${expectedTag}\n`, {
        append: true,
      });
    }

    console.log(`Successfully created PR #${prNumber} for Deno v${denoVersion}`);
    console.log(`Branch: ${branchName}`);
    console.log(`Expected tag after merge: ${expectedTag}`);
    console.log(`To create release: git tag ${expectedTag} && git push --tags`);
  } catch (error) {
    console.error(`Error creating pull request: ${error}`);
    Deno.exit(1);
  }
}

if (import.meta.main) {
  await main();
}
