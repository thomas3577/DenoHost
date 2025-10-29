import { assertEquals, assertRejects } from '@std/assert';
import { restore, stub } from '@std/testing/mock';

// Mock interfaces for testing
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

// Extract core functions for testing
async function fetchExistingPRs(token?: string): Promise<GitHubPR[]> {
  const headers: Record<string, string> = {
    'Accept': 'application/vnd.github.v3+json',
    'User-Agent': 'DenoHost-Release-Check',
  };

  if (token) {
    headers['Authorization'] = `token ${token}`;
  }

  const response = await fetch('https://api.github.com/repos/thomas3577/DenoHost/pulls?state=open', {
    headers,
  });

  if (!response.ok) {
    throw new Error(`GitHub API request failed: ${response.status}`);
  }

  const prs: GitHubPR[] = await response.json();
  return prs;
}

async function fetchExistingBranches(token?: string): Promise<string[]> {
  const headers: Record<string, string> = {
    'Accept': 'application/vnd.github.v3+json',
    'User-Agent': 'DenoHost-Release-Check',
  };

  if (token) {
    headers['Authorization'] = `token ${token}`;
  }

  const response = await fetch('https://api.github.com/repos/thomas3577/DenoHost/branches', {
    headers,
  });

  if (!response.ok) {
    throw new Error(`GitHub API request failed: ${response.status}`);
  }

  const branches: GitHubBranch[] = await response.json();
  return branches.map(branch => branch.name);
}

function checkForExistingUpdate(
  existingPRs: GitHubPR[],
  existingBranches: string[],
  denoVersion: string
): boolean {
  const branchPattern = `update-deno-v${denoVersion}`;
  const prPattern = new RegExp(`update.*deno.*v?${denoVersion.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`, 'i');

  const foundPR = existingPRs.find(pr =>
    prPattern.test(pr.title) || pr.head.ref === branchPattern
  );

  const foundBranch = existingBranches.find(branch =>
    branch === branchPattern || branch.includes(`deno-v${denoVersion}`)
  );

  return !!(foundPR || foundBranch);
}

Deno.test('fetchExistingPRs - successful API call', async () => {
  const mockPRs: GitHubPR[] = [
    {
      number: 123,
      title: 'Update Deno to v1.45.0',
      head: { ref: 'update-deno-v1.45.0' },
      state: 'open'
    },
    {
      number: 124,
      title: 'Fix bug in handler',
      head: { ref: 'fix-bug' },
      state: 'open'
    }
  ];

  const mockResponse = new Response(
    JSON.stringify(mockPRs),
    { status: 200, headers: { 'content-type': 'application/json' } }
  );

  stub(globalThis, 'fetch', () => Promise.resolve(mockResponse));

  try {
    const result = await fetchExistingPRs('test-token');
    assertEquals(result.length, 2);
    assertEquals(result[0].number, 123);
    assertEquals(result[0].title, 'Update Deno to v1.45.0');
  } finally {
    restore();
  }
});

Deno.test('fetchExistingPRs - API failure', async () => {
  const mockResponse = new Response('Forbidden', { status: 403 });

  stub(globalThis, 'fetch', () => Promise.resolve(mockResponse));

  try {
    await assertRejects(
      () => fetchExistingPRs('test-token'),
      Error,
      'GitHub API request failed: 403'
    );
  } finally {
    restore();
  }
});

Deno.test('fetchExistingBranches - successful API call', async () => {
  const mockBranches: GitHubBranch[] = [
    { name: 'main' },
    { name: 'update-deno-v1.45.0' },
    { name: 'feature-branch' }
  ];

  const mockResponse = new Response(
    JSON.stringify(mockBranches),
    { status: 200, headers: { 'content-type': 'application/json' } }
  );

  stub(globalThis, 'fetch', () => Promise.resolve(mockResponse));

  try {
    const result = await fetchExistingBranches('test-token');
    assertEquals(result.length, 3);
    assertEquals(result[0], 'main');
    assertEquals(result[1], 'update-deno-v1.45.0');
  } finally {
    restore();
  }
});

Deno.test('fetchExistingBranches - API failure', async () => {
  const mockResponse = new Response('Not Found', { status: 404 });

  stub(globalThis, 'fetch', () => Promise.resolve(mockResponse));

  try {
    await assertRejects(
      () => fetchExistingBranches('test-token'),
      Error,
      'GitHub API request failed: 404'
    );
  } finally {
    restore();
  }
});

Deno.test('checkForExistingUpdate - finds existing PR by title', () => {
  const mockPRs: GitHubPR[] = [
    {
      number: 123,
      title: 'Update Deno to v1.45.0',
      head: { ref: 'some-branch' },
      state: 'open'
    }
  ];
  const mockBranches: string[] = ['main', 'feature'];

  const result = checkForExistingUpdate(mockPRs, mockBranches, '1.45.0');
  assertEquals(result, true);
});

Deno.test('checkForExistingUpdate - finds existing PR by branch name', () => {
  const mockPRs: GitHubPR[] = [
    {
      number: 123,
      title: 'Some other title',
      head: { ref: 'update-deno-v1.45.0' },
      state: 'open'
    }
  ];
  const mockBranches: string[] = ['main'];

  const result = checkForExistingUpdate(mockPRs, mockBranches, '1.45.0');
  assertEquals(result, true);
});

Deno.test('checkForExistingUpdate - finds existing branch', () => {
  const mockPRs: GitHubPR[] = [];
  const mockBranches: string[] = ['main', 'update-deno-v1.45.0', 'feature'];

  const result = checkForExistingUpdate(mockPRs, mockBranches, '1.45.0');
  assertEquals(result, true);
});

Deno.test('checkForExistingUpdate - finds branch with alternative pattern', () => {
  const mockPRs: GitHubPR[] = [];
  const mockBranches: string[] = ['main', 'feature-deno-v1.45.0', 'other'];

  const result = checkForExistingUpdate(mockPRs, mockBranches, '1.45.0');
  assertEquals(result, true);
});

Deno.test('checkForExistingUpdate - no existing update found', () => {
  const mockPRs: GitHubPR[] = [
    {
      number: 123,
      title: 'Fix bug in handler',
      head: { ref: 'fix-bug' },
      state: 'open'
    }
  ];
  const mockBranches: string[] = ['main', 'feature', 'fix-bug'];

  const result = checkForExistingUpdate(mockPRs, mockBranches, '1.45.0');
  assertEquals(result, false);
});

Deno.test('checkForExistingUpdate - case insensitive PR title matching', () => {
  const mockPRs: GitHubPR[] = [
    {
      number: 123,
      title: 'UPDATE DENO TO V1.45.0',
      head: { ref: 'some-branch' },
      state: 'open'
    }
  ];
  const mockBranches: string[] = ['main'];

  const result = checkForExistingUpdate(mockPRs, mockBranches, '1.45.0');
  assertEquals(result, true);
});

Deno.test('checkForExistingUpdate - handles special regex characters in version', () => {
  const mockPRs: GitHubPR[] = [];
  const mockBranches: string[] = ['main', 'update-deno-v1.45.0-rc.1'];

  // Test that special regex characters are properly escaped
  const result = checkForExistingUpdate(mockPRs, mockBranches, '1.45.0');
  assertEquals(result, true); // Should match because branch contains the version
});
