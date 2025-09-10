# Deno Release Check Action

This action checks for new Deno releases and creates appropriate tags in the DenoHost repository.

## Features

- **Rate limit handling**: Uses GitHub token authentication to avoid API rate limits
- **Fallback mechanism**: Falls back to installed Deno version if GitHub API fails
- **Robust error handling**: Multiple methods to ensure version detection works
- **Pre-release tag creation**: Automatically creates alpha/beta/rc tags for new Deno versions

## Scripts

- `fetch-deno-release.ts` - Fetches the latest Deno release from GitHub API with fallback support
- `check-existing-tags.ts` - Checks if a tag for the Deno version already exists
- `create-tag.ts` - Creates and pushes a new pre-release tag

## Authentication

The action requires a GitHub token to avoid rate limiting issues. The token is automatically provided by GitHub Actions via `secrets.GITHUB_TOKEN`.

## Testing

### Run all tests

```bash
deno task test
```

### Run tests in watch mode

```bash
deno task test:watch
```

### Run tests with coverage

```bash
deno task test:coverage
```

### Cache dependencies

```bash
deno task cache
```

## Test Coverage

### `fetch-deno-release.test.ts`

- Tests API success/failure scenarios
- Tests version prefix removal logic
- Tests edge cases with malformed responses

### `check-existing-tags.test.ts`

- Tests GitHub API tag fetching
- Tests regex pattern matching for existing tags
- Tests edge cases with special characters and partial matches

### `create-tag.test.ts`

- Tests pre-release type parsing from existing tags
- Tests next version determination logic
- Tests tag name building
- Tests both auto-detection and explicit type specification

## Manual Testing

You can also test the scripts manually:

```bash
# Test fetch Deno release
deno run --allow-net scripts/fetch-deno-release.ts

# Test check existing tags (requires TAG_CORE env var)
TAG_CORE=2.5.0 deno run --allow-net scripts/check-existing-tags.ts

# Test create tag (requires multiple env vars - be careful with this one!)
TAG_CORE=2.5.0 PRERELEASE_TYPE=alpha GH_TOKEN=your_token deno run --allow-net --allow-env --allow-run scripts/create-tag.ts
```

**Note**: Be very careful with the `create-tag.ts` script as it will actually create and push tags to the repository!
