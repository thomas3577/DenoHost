const DEFAULT_PACKAGES = ['DenoHost.Core', 'DenoHost.Runtime.linux-x64'];
const MAX_ATTEMPTS = 10;

export interface WaitOptions {
  packageVersion: string;
  packageIds: string[];
  baseUrl?: string;
  fetchImpl?: typeof fetch;
  sleepImpl?: (delayMs: number) => Promise<void>;
  logger?: Pick<Console, 'log' | 'error'>;
}

export interface WaitResult {
  packageId: string;
  url: string;
  attempts: number;
}

function getBackoffSeconds(attempt: number): number {
  switch (attempt) {
    case 1:
      return 10;
    case 2:
      return 20;
    case 3:
      return 30;
    case 4:
      return 45;
    default:
      return 60;
  }
}

function buildPackageUrl(packageId: string, packageVersion: string, baseUrl = 'https://api.nuget.org/v3-flatcontainer'): string {
  const normalizedPackageId = packageId.toLowerCase();
  return `${baseUrl}/${normalizedPackageId}/${packageVersion}/${normalizedPackageId}.${packageVersion}.nupkg`;
}

async function waitForPackageAvailability(options: WaitOptions): Promise<WaitResult[]> {
  const fetchImpl = options.fetchImpl ?? fetch;
  const sleepImpl = options.sleepImpl ?? ((delayMs: number) => new Promise((resolve) => setTimeout(resolve, delayMs)));
  const logger = options.logger ?? console;
  const baseUrl = options.baseUrl ?? 'https://api.nuget.org/v3-flatcontainer';
  const results: WaitResult[] = [];

  for (const packageId of options.packageIds) {
    const url = buildPackageUrl(packageId, options.packageVersion, baseUrl);
    logger.log(`Waiting for ${packageId} ${options.packageVersion}`);

    let found = false;
    for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
      const response = await fetchImpl(url);
      if (response.ok) {
        logger.log(`Found ${packageId} ${options.packageVersion} on attempt ${attempt}`);
        results.push({ packageId, url, attempts: attempt });
        found = true;
        break;
      }

      const delaySeconds = getBackoffSeconds(attempt);
      if (attempt < MAX_ATTEMPTS) {
        logger.log(`${packageId} ${options.packageVersion} not visible yet (attempt ${attempt}/${MAX_ATTEMPTS}). Retrying in ${delaySeconds}s...`);
        await sleepImpl(delaySeconds * 1000);
      }
    }

    if (!found) {
      throw new Error(`Package ${packageId} ${options.packageVersion} did not appear on nuget.org within timeout.`);
    }
  }

  return results;
}

async function main(): Promise<number> {
  const packageVersion = Deno.env.get('PACKAGE_VERSION')?.trim();
  if (!packageVersion) {
    console.error('PACKAGE_VERSION is required.');
    return 2;
  }

  const packageIds = [
    Deno.env.get('CORE_PACKAGE_ID')?.trim() || DEFAULT_PACKAGES[0],
    Deno.env.get('RUNTIME_PACKAGE_ID')?.trim() || DEFAULT_PACKAGES[1],
  ];

  try {
    await waitForPackageAvailability({
      packageVersion,
      packageIds,
    });
    return 0;
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    return 1;
  }
}

if (import.meta.main) {
  Deno.exit(await main());
}

export { buildPackageUrl, getBackoffSeconds, waitForPackageAvailability };
