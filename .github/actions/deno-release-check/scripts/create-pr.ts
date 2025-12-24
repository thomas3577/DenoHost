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

  const gitUserEmail = Deno.env.get('GIT_USER_EMAIL');
  const gitUserName = Deno.env.get('GIT_USER_NAME');

  if (!gitUserEmail || !gitUserName) {
    throw new Error('GIT_USER_EMAIL and GIT_USER_NAME environment variables must be set.');
  }

  await runCommand([
    'git',
    'config',
    '--local',
    'user.email',
    gitUserEmail,
  ]);

  await runCommand([
    'git',
    'config',
    '--local',
    'user.name',
    gitUserName,
  ]);
}

async function initializeRepo(): Promise<void> {
  const ghToken = Deno.env.get('GH_TOKEN');
  if (!ghToken) {
    throw new Error('GH_TOKEN environment variable not set');
  }

  const repository = Deno.env.get('GITHUB_REPOSITORY');
  if (!repository) {
    throw new Error('GITHUB_REPOSITORY environment variable not set');
  }

  console.log('Initializing repository...');

  // Update the remote URL with token for authentication (needed for push)
  await runCommand([
    'git',
    'remote',
    'set-url',
    'origin',
    `https://x-access-token:${ghToken}@github.com/${repository}.git`,
  ]);
}

async function createBranch(branchName: string): Promise<void> {
  console.log(`Creating branch: ${branchName}`);

  // Create and switch to new branch from current HEAD
  await runCommand(['git', 'checkout', '-b', branchName]);

  // Verify we're on the new branch
  const currentBranch = await runCommand(['git', 'branch', '--show-current']);
  console.log(`Currently on branch: ${currentBranch}`);
}

async function updateDenoVersion(newVersion: string): Promise<void> {
  console.log(`Updating Directory.Build.props to Deno version ${newVersion}`);

  // Use GITHUB_WORKSPACE to get the repository root
  const workspace = Deno.env.get('GITHUB_WORKSPACE') || Deno.cwd();
  const filePath = `${workspace}/Directory.Build.props`;

  console.log(`Working directory: ${Deno.cwd()}`);
  console.log(`GITHUB_WORKSPACE: ${workspace}`);
  console.log(`Looking for Directory.Build.props at: ${filePath}`);

  // List files in workspace for debugging
  try {
    const files = [];
    for await (const entry of Deno.readDir(workspace)) {
      files.push(entry.name);
    }
    console.log(`Files in workspace: ${files.join(', ')}`);
  } catch (error) {
    console.error('Failed to list workspace files:', error);
  }

  try {
    // Read the current file
    const content = await Deno.readTextFile(filePath);

    // Replace the DenoVersion
    const updatedContent = content.replace(
      /<DenoVersion>[\d.]+<\/DenoVersion>/,
      `<DenoVersion>${newVersion}</DenoVersion>`,
    );

    // Write to file
    await Deno.writeTextFile(filePath, updatedContent);

    console.log(`Successfully updated DenoVersion to ${newVersion} in ${filePath}`);

    // Stage the file for commit
    await runCommand(['git', 'add', filePath]);
  } catch (error) {
    console.error(`Failed to update ${filePath}: ${error}`);
    throw error;
  }
}

async function createPullRequest(branchName: string, denoVersion: string): Promise<number> {
  const ghToken = Deno.env.get('GH_TOKEN');
  if (!ghToken) {
    throw new Error('GH_TOKEN environment variable not set');
  }

  console.log(`Creating pull request for branch: ${branchName}`);

  const repository = Deno.env.get('GITHUB_REPOSITORY');
  if (!repository) {
    throw new Error('GITHUB_REPOSITORY environment variable not set');
  }

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
    const response = await fetch(`https://api.github.com/repos/${repository}/pulls`, {
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

async function pushBranch(branchName: string, denoVersion: string): Promise<void> {
  console.log(`Pushing branch: ${branchName}`);

  // Commit the changes
  await runCommand(['git', 'commit', '-m', `chore: update Deno to v${denoVersion}`]);
  await runCommand(['git', 'push', '--force', 'origin', branchName]);
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
    const branchName = `release/v${denoVersion}`;

    console.log(`Expected new tag after merge: ${expectedTag}`);

    await createBranch(branchName);
    await updateDenoVersion(denoVersion);
    await pushBranch(branchName, denoVersion);

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
