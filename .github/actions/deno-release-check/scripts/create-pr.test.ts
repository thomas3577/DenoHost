import { assertEquals, assertRejects } from '@std/assert';
import { restore, stub } from '@std/testing/mock';

// Mock interfaces for testing
interface GitHubPRResponse {
  number: number;
  title: string;
  html_url: string;
}

// Extract core functions for testing
async function createPullRequest(
  branchName: string,
  denoVersion: string,
  token: string,
): Promise<number> {
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

  const response = await fetch('https://api.github.com/repos/thomas3577/DenoHost/pulls', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
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

  const pr: GitHubPRResponse = await response.json();
  return pr.number;
}

Deno.test('createPullRequest - successful creation', async () => {
  const mockPRResponse: GitHubPRResponse = {
    number: 123,
    title: 'Update Deno to v1.45.0',
    html_url: 'https://github.com/thomas3577/DenoHost/pull/123',
  };

  const mockResponse = new Response(
    JSON.stringify(mockPRResponse),
    { status: 201, headers: { 'content-type': 'application/json' } },
  );

  stub(globalThis, 'fetch', () => Promise.resolve(mockResponse));

  try {
    const result = await createPullRequest(
      'update-deno-v1.45.0',
      '1.45.0',
      'test-token',
    );
    assertEquals(result, 123);
  } finally {
    restore();
  }
});
Deno.test('createPullRequest - API failure', async () => {
  const mockResponse = new Response(
    'Validation failed',
    { status: 422 },
  );

  stub(globalThis, 'fetch', () => Promise.resolve(mockResponse));

  try {
    await assertRejects(
      () =>
        createPullRequest(
          'update-deno-v1.45.0',
          '1.45.0',
          'test-token',
        ),
      Error,
      'Failed to create pull request: 422  - Validation failed',
    );
  } finally {
    restore();
  }
});

Deno.test('createPullRequest - generates correct title and body', async () => {
  let capturedRequestBody: Record<string, unknown> = {};

  const mockResponse = new Response(
    JSON.stringify({ number: 123, title: 'test', html_url: 'test' }),
    { status: 201, headers: { 'content-type': 'application/json' } },
  );

  // deno-lint-ignore no-explicit-any
  stub(globalThis, 'fetch', (_input: string | URL | Request, init?: any) => {
    if (init?.body) {
      capturedRequestBody = JSON.parse(init.body as string);
    }
    return Promise.resolve(mockResponse);
  });

  try {
    await createPullRequest(
      'update-deno-v1.45.0',
      '1.45.0',
      'test-token',
    );

    assertEquals(capturedRequestBody.title, 'Update Deno to v1.45.0');
    assertEquals(capturedRequestBody.head, 'update-deno-v1.45.0');
    assertEquals(capturedRequestBody.base, 'main');
    assertEquals(capturedRequestBody.draft, true);

    // Check that body contains expected content
    const body = capturedRequestBody.body as string;
    assertEquals(body.includes('Deno v1.45.0'), true);
    assertEquals(body.includes('This will create a new release when merged'), true);
    assertEquals(body.includes('git tag v1.45.0 && git push --tags'), true);
    assertEquals(body.includes('automatically by the Deno Release Check'), true);
  } finally {
    restore();
  }
});
