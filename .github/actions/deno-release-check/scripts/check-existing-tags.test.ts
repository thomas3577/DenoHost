import { assertEquals } from '@std/assert';
import { restore, stub } from '@std/testing/mock';

interface GitTag {
  name: string;
}

async function fetchGitTags(repoOwner = 'thomas3577', repoName = 'DenoHost'): Promise<string[]> {
  const response = await fetch(`https://api.github.com/repos/${repoOwner}/${repoName}/tags`);

  if (!response.ok) {
    throw new Error(`GitHub API request failed: ${response.status}`);
  }

  const tags: GitTag[] = await response.json();
  return tags.map((tag) => tag.name);
}

function checkTagExists(tags: string[], tagCore: string): boolean {
  const pattern = new RegExp(
    `^v${tagCore.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(-|$)`,
  );
  return tags.some((tag) => pattern.test(tag));
}

function findExistingTag(tags: string[], tagCore: string): string | undefined {
  const pattern = new RegExp(
    `^v${tagCore.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(-|$)`,
  );
  return tags.find((tag) => pattern.test(tag));
}

Deno.test('fetchGitTags - successful API call', async () => {
  const mockTags = [
    { name: 'v2.4.2-alpha.6' },
    { name: 'v2.4.1' },
    { name: 'v2.4.0-beta.1' },
  ];

  const mockResponse = new Response(
    JSON.stringify(mockTags),
    { status: 200, headers: { 'content-type': 'application/json' } },
  );

  const _fetchStub = stub(globalThis, 'fetch', () => Promise.resolve(mockResponse));

  try {
    const result = await fetchGitTags();
    assertEquals(result, ['v2.4.2-alpha.6', 'v2.4.1', 'v2.4.0-beta.1']);
  } finally {
    restore();
  }
});

Deno.test('checkTagExists - exact version match', () => {
  const tags = ['v2.4.2-alpha.6', 'v2.4.1', 'v2.4.0-beta.1'];
  assertEquals(checkTagExists(tags, '2.4.1'), true);
});

Deno.test('checkTagExists - version with prerelease match', () => {
  const tags = ['v2.4.2-alpha.6', 'v2.4.1', 'v2.4.0-beta.1'];
  assertEquals(checkTagExists(tags, '2.4.2'), true);
});

Deno.test('checkTagExists - no match', () => {
  const tags = ['v2.4.2-alpha.6', 'v2.4.1', 'v2.4.0-beta.1'];
  assertEquals(checkTagExists(tags, '2.5.0'), false);
});

Deno.test('checkTagExists - regex special characters', () => {
  const tags = ['v1.2.3-alpha.1'];
  assertEquals(checkTagExists(tags, '1.2.3'), true);
});

Deno.test('findExistingTag - returns exact match', () => {
  const tags = ['v2.4.2-alpha.6', 'v2.4.1', 'v2.4.0-beta.1'];
  assertEquals(findExistingTag(tags, '2.4.1'), 'v2.4.1');
});

Deno.test('findExistingTag - returns prerelease match', () => {
  const tags = ['v2.4.2-alpha.6', 'v2.4.1', 'v2.4.0-beta.1'];
  assertEquals(findExistingTag(tags, '2.4.2'), 'v2.4.2-alpha.6');
});

Deno.test('findExistingTag - returns undefined for no match', () => {
  const tags = ['v2.4.2-alpha.6', 'v2.4.1', 'v2.4.0-beta.1'];
  assertEquals(findExistingTag(tags, '2.5.0'), undefined);
});

Deno.test('checkTagExists - empty tags array', () => {
  const tags: string[] = [];
  assertEquals(checkTagExists(tags, '2.4.1'), false);
});

Deno.test('checkTagExists - partial version match should not match', () => {
  const tags = ['v2.4.10'];
  assertEquals(checkTagExists(tags, '2.4.1'), false);
});
