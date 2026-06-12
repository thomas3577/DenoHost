import { assertEquals, assertRejects } from '@std/assert';
import { buildPackageUrl, getBackoffSeconds, waitForPackageAvailability } from './wait-nuget-propagation.ts';

Deno.test('buildPackageUrl normalizes package id to lowercase', () => {
  assertEquals(
    buildPackageUrl('DenoHost.Core', '2.3.0-alpha.1'),
    'https://api.nuget.org/v3-flatcontainer/denohost.core/2.3.0-alpha.1/denohost.core.2.3.0-alpha.1.nupkg',
  );
});

Deno.test('getBackoffSeconds uses the expected retry schedule', () => {
  assertEquals(getBackoffSeconds(1), 10);
  assertEquals(getBackoffSeconds(2), 20);
  assertEquals(getBackoffSeconds(3), 30);
  assertEquals(getBackoffSeconds(4), 45);
  assertEquals(getBackoffSeconds(5), 60);
  assertEquals(getBackoffSeconds(10), 60);
});

Deno.test('waitForPackageAvailability resolves when all packages are visible', async () => {
  const calls: string[] = [];
  const sleeps: number[] = [];

  const result = await waitForPackageAvailability({
    packageVersion: '2.3.0-alpha.1',
    packageIds: ['DenoHost.Core', 'DenoHost.Runtime.linux-x64'],
    baseUrl: 'https://example.test/v3-flatcontainer',
    fetchImpl: async (input) => {
      const requestUrl = String(input);
      calls.push(requestUrl);
      if (requestUrl.includes('denohost.core')) {
        return calls.length === 1 ? new Response(null, { status: 404 }) : new Response(null, { status: 200 });
      }

      return new Response(null, { status: 200 });
    },
    sleepImpl: async (delayMs) => {
      sleeps.push(delayMs);
    },
    logger: { log: () => {}, error: () => {} },
  });

  assertEquals(result, [
    {
      packageId: 'DenoHost.Core',
      url: 'https://example.test/v3-flatcontainer/denohost.core/2.3.0-alpha.1/denohost.core.2.3.0-alpha.1.nupkg',
      attempts: 2,
    },
    {
      packageId: 'DenoHost.Runtime.linux-x64',
      url: 'https://example.test/v3-flatcontainer/denohost.runtime.linux-x64/2.3.0-alpha.1/denohost.runtime.linux-x64.2.3.0-alpha.1.nupkg',
      attempts: 1,
    },
  ]);
  assertEquals(calls, [
    'https://example.test/v3-flatcontainer/denohost.core/2.3.0-alpha.1/denohost.core.2.3.0-alpha.1.nupkg',
    'https://example.test/v3-flatcontainer/denohost.core/2.3.0-alpha.1/denohost.core.2.3.0-alpha.1.nupkg',
    'https://example.test/v3-flatcontainer/denohost.runtime.linux-x64/2.3.0-alpha.1/denohost.runtime.linux-x64.2.3.0-alpha.1.nupkg',
  ]);
  assertEquals(sleeps, [10000]);
});

Deno.test('waitForPackageAvailability fails after exhausting retries', async () => {
  const sleeps: number[] = [];

  await assertRejects(
    () => waitForPackageAvailability({
      packageVersion: '2.3.0-alpha.1',
      packageIds: ['DenoHost.Core'],
      baseUrl: 'https://example.test/v3-flatcontainer',
      fetchImpl: async () => new Response(null, { status: 404 }),
      sleepImpl: async (delayMs) => {
        sleeps.push(delayMs);
      },
      logger: { log: () => {}, error: () => {} },
    }),
    Error,
    'Package DenoHost.Core 2.3.0-alpha.1 did not appear on nuget.org within timeout.',
  );

  assertEquals(sleeps, [10000, 20000, 30000, 45000, 60000, 60000, 60000, 60000, 60000, 60000, 60000, 60000, 60000, 60000, 60000, 60000, 60000, 60000, 60000]);
});

Deno.test('waitForPackageAvailability retries when fetch throws', async () => {
  const sleeps: number[] = [];
  const errors: string[] = [];
  let attempts = 0;

  const result = await waitForPackageAvailability({
    packageVersion: '2.3.0-alpha.1',
    packageIds: ['DenoHost.Core'],
    baseUrl: 'https://example.test/v3-flatcontainer',
    fetchImpl: async () => {
      attempts += 1;
      if (attempts === 1) {
        throw new Error('temporary network error');
      }

      return new Response(null, { status: 200 });
    },
    sleepImpl: async (delayMs) => {
      sleeps.push(delayMs);
    },
    logger: {
      log: () => {},
      error: (message) => {
        errors.push(message);
      },
    },
    requestTimeoutMs: 10,
  });

  assertEquals(result[0]?.attempts, 2);
  assertEquals(sleeps, [10000]);
  assertEquals(errors, ['Failed to check DenoHost.Core 2.3.0-alpha.1 on attempt 1: temporary network error']);
});

Deno.test('waitForPackageAvailability retries when fetch times out', async () => {
  const sleeps: number[] = [];
  const errors: string[] = [];
  let attempts = 0;

  const result = await waitForPackageAvailability({
    packageVersion: '2.3.0-alpha.1',
    packageIds: ['DenoHost.Core'],
    baseUrl: 'https://example.test/v3-flatcontainer',
    fetchImpl: async () => {
      attempts += 1;
      if (attempts === 1) {
        return await new Promise<Response>(() => {});
      }

      return new Response(null, { status: 200 });
    },
    sleepImpl: async (delayMs) => {
      sleeps.push(delayMs);
    },
    logger: {
      log: () => {},
      error: (message) => {
        errors.push(message);
      },
    },
    requestTimeoutMs: 10,
  });

  assertEquals(result[0]?.attempts, 2);
  assertEquals(sleeps, [10000]);
  assertEquals(errors, ['Failed to check DenoHost.Core 2.3.0-alpha.1 on attempt 1: Request timed out after 10ms.']);
});
