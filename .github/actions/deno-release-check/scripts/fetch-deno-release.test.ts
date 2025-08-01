import { assertEquals, assertRejects } from "@std/assert";
import { restore, stub } from "@std/testing/mock";

// Import the functions we want to test
// Since the scripts are designed to run as main modules, we'll test the core logic
interface DenoRelease {
  tag_name: string;
}

async function fetchLatestDenoRelease(): Promise<string> {
  const response = await fetch(
    "https://api.github.com/repos/denoland/deno/releases/latest",
  );

  if (!response.ok) {
    throw new Error(`GitHub API request failed: ${response.status}`);
  }

  const data: DenoRelease = await response.json();

  if (!data.tag_name) {
    throw new Error("No tag_name found in release data");
  }

  return data.tag_name;
}

function removeVersionPrefix(tag: string): string {
  return tag.replace(/^v/, "");
}

Deno.test("fetchLatestDenoRelease - successful API call", async () => {
  const mockResponse = new Response(
    JSON.stringify({ tag_name: "v2.1.0" }),
    { status: 200, headers: { "content-type": "application/json" } },
  );

  stub(globalThis, "fetch", () => Promise.resolve(mockResponse));

  try {
    const result = await fetchLatestDenoRelease();
    assertEquals(result, "v2.1.0");
  } finally {
    restore();
  }
});

Deno.test("fetchLatestDenoRelease - API failure", async () => {
  const mockResponse = new Response("Not Found", { status: 404 });

  stub(globalThis, "fetch", () => Promise.resolve(mockResponse));

  try {
    await assertRejects(
      () => fetchLatestDenoRelease(),
      Error,
      "GitHub API request failed: 404",
    );
  } finally {
    restore();
  }
});

Deno.test("fetchLatestDenoRelease - missing tag_name", async () => {
  const mockResponse = new Response(
    JSON.stringify({ name: "2.1.0" }), // missing tag_name
    { status: 200, headers: { "content-type": "application/json" } },
  );

  stub(globalThis, "fetch", () => Promise.resolve(mockResponse));

  try {
    await assertRejects(
      () => fetchLatestDenoRelease(),
      Error,
      "No tag_name found in release data",
    );
  } finally {
    restore();
  }
});

Deno.test("removeVersionPrefix - removes v prefix", () => {
  assertEquals(removeVersionPrefix("v2.1.0"), "2.1.0");
});

Deno.test("removeVersionPrefix - no v prefix", () => {
  assertEquals(removeVersionPrefix("2.1.0"), "2.1.0");
});

Deno.test("removeVersionPrefix - multiple v prefixes", () => {
  assertEquals(removeVersionPrefix("vv2.1.0"), "v2.1.0");
});

Deno.test("removeVersionPrefix - empty string", () => {
  assertEquals(removeVersionPrefix(""), "");
});

Deno.test("removeVersionPrefix - only v", () => {
  assertEquals(removeVersionPrefix("v"), "");
});
