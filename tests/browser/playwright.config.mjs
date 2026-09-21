import { defineConfig } from "@playwright/test";

const hosts = {
    server: process.env.BB_SERVER_URL || "http://localhost:7172",
    wasm: process.env.BB_WASM_URL || "http://localhost:5184",
    auto: process.env.BB_AUTO_URL || "http://localhost:5185",
};

export default defineConfig({
    testDir: ".",
    testMatch: "*.spec.mjs",
    fullyParallel: true,
    workers: 2,
    retries: 0,
    timeout: 45_000,
    expect: { timeout: 8_000 },
    reporter: [["list"]],
    use: {
        viewport: { width: 1440, height: 1000 },
        locale: "en-GB",
        screenshot: "only-on-failure",
        trace: "retain-on-failure",
    },
    projects: Object.entries(hosts).flatMap(([host, baseURL]) =>
        ["chromium", "webkit"].map((browserName) => ({
            name: `${host}-${browserName}`,
            use: {
                baseURL,
                browserName,
                ...(browserName === "webkit" && process.env.BB_WEBKIT_EXECUTABLE
                    ? { launchOptions: { executablePath: process.env.BB_WEBKIT_EXECUTABLE } }
                    : {}),
                ...(browserName === "chromium" && process.env.BB_CHROMIUM_CHANNEL
                    ? { channel: process.env.BB_CHROMIUM_CHANNEL }
                    : {}),
            },
        })),
    ),
});
