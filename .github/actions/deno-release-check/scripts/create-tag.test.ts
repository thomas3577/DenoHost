import { assertEquals } from '@std/assert';

interface GitTag {
  name: string;
}

interface PreReleaseInfo {
  type: string;
  number: number;
}

function parsePreReleaseTypes(tags: string[]): PreReleaseInfo[] {
  const preReleaseTypes = ['alpha', 'beta', 'rc', 'preview'];
  const preReleases: PreReleaseInfo[] = [];

  for (const tag of tags) {
    for (const type of preReleaseTypes) {
      const pattern = new RegExp(`${type}\\.(\\d+)$`);
      const match = tag.match(pattern);
      if (match) {
        preReleases.push({
          type,
          number: parseInt(match[1], 10),
        });
      }
    }
  }

  return preReleases;
}

function determineNextPreRelease(
  preReleases: PreReleaseInfo[],
  requestedType?: string,
): { type: string; number: number } {
  if (requestedType && requestedType !== 'auto') {
    // Use specified pre-release type
    const existingOfType = preReleases
      .filter((pr) => pr.type === requestedType)
      .map((pr) => pr.number);

    const nextNumber = existingOfType.length > 0 ? Math.max(...existingOfType) + 1 : 1;

    return { type: requestedType, number: nextNumber };
  } else {
    // Auto-detect: find the type with the highest number
    if (preReleases.length > 0) {
      const highest = preReleases.reduce((prev, current) =>
        prev.number > current.number ? prev : current
      );
      return { type: highest.type, number: highest.number + 1 };
    }

    return { type: 'alpha', number: 1 };
  }
}

function buildTagName(tagCore: string, preRelease: { type: string; number: number }): string {
  return `v${tagCore}-${preRelease.type}.${preRelease.number}`;
}

Deno.test('parsePreReleaseTypes - mixed pre-release types', () => {
  const tags = [
    'v2.4.2-alpha.6',
    'v2.4.1',
    'v2.4.0-beta.2',
    'v2.3.0-rc.1',
    'v2.2.0-preview.3',
  ];

  const result = parsePreReleaseTypes(tags);

  assertEquals(result.length, 4);
  assertEquals(result[0], { type: 'alpha', number: 6 });
  assertEquals(result[1], { type: 'beta', number: 2 });
  assertEquals(result[2], { type: 'rc', number: 1 });
  assertEquals(result[3], { type: 'preview', number: 3 });
});

Deno.test('parsePreReleaseTypes - no pre-releases', () => {
  const tags = ['v2.4.1', 'v2.3.0', 'v2.2.0'];
  const result = parsePreReleaseTypes(tags);
  assertEquals(result.length, 0);
});

Deno.test('parsePreReleaseTypes - only alpha versions', () => {
  const tags = ['v2.4.2-alpha.6', 'v2.3.0-alpha.1', 'v2.2.0-alpha.10'];
  const result = parsePreReleaseTypes(tags);

  assertEquals(result.length, 3);
  assertEquals(result[0], { type: 'alpha', number: 6 });
  assertEquals(result[1], { type: 'alpha', number: 1 });
  assertEquals(result[2], { type: 'alpha', number: 10 });
});

Deno.test('determineNextPreRelease - specific type with existing versions', () => {
  const preReleases = [
    { type: 'alpha', number: 6 },
    { type: 'beta', number: 2 },
    { type: 'alpha', number: 3 },
  ];

  const result = determineNextPreRelease(preReleases, 'alpha');
  assertEquals(result, { type: 'alpha', number: 7 });
});

Deno.test('determineNextPreRelease - specific type with no existing versions', () => {
  const preReleases = [
    { type: 'alpha', number: 6 },
    { type: 'beta', number: 2 },
  ];

  const result = determineNextPreRelease(preReleases, 'rc');
  assertEquals(result, { type: 'rc', number: 1 });
});

Deno.test('determineNextPreRelease - auto mode with existing versions', () => {
  const preReleases = [
    { type: 'alpha', number: 6 },
    { type: 'beta', number: 10 },
    { type: 'rc', number: 2 },
  ];

  const result = determineNextPreRelease(preReleases, 'auto');
  assertEquals(result, { type: 'beta', number: 11 });
});

Deno.test('determineNextPreRelease - auto mode with no existing versions', () => {
  const preReleases: PreReleaseInfo[] = [];

  const result = determineNextPreRelease(preReleases, 'auto');
  assertEquals(result, { type: 'alpha', number: 1 });
});

Deno.test('determineNextPreRelease - undefined type (auto mode)', () => {
  const preReleases = [
    { type: 'alpha', number: 5 },
    { type: 'beta', number: 3 },
  ];

  const result = determineNextPreRelease(preReleases);
  assertEquals(result, { type: 'alpha', number: 6 });
});

Deno.test('buildTagName - standard format', () => {
  const result = buildTagName('2.4.3', { type: 'alpha', number: 7 });
  assertEquals(result, 'v2.4.3-alpha.7');
});

Deno.test('buildTagName - beta version', () => {
  const result = buildTagName('2.5.0', { type: 'beta', number: 1 });
  assertEquals(result, 'v2.5.0-beta.1');
});

Deno.test('buildTagName - rc version', () => {
  const result = buildTagName('3.0.0', { type: 'rc', number: 2 });
  assertEquals(result, 'v3.0.0-rc.2');
});

Deno.test('parsePreReleaseTypes - edge cases', () => {
  const tags = [
    'v2.4.2-alpha.999', // Large number
    'v2.4.1-beta.0', // Zero
    'invalid-tag', // Invalid format
    'v2.4.0-gamma.1', // Unsupported type
  ];

  const result = parsePreReleaseTypes(tags);

  assertEquals(result.length, 2); // Only alpha and beta should match
  assertEquals(result[0], { type: 'alpha', number: 999 });
  assertEquals(result[1], { type: 'beta', number: 0 });
});
