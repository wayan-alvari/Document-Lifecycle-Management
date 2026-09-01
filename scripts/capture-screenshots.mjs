import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";

const [appUrl = "http://127.0.0.1:5187", cdpUrl = "http://127.0.0.1:9222"] = process.argv.slice(2);
const outputDirectory = path.resolve("docs", "screenshots");

class DevToolsClient {
  #nextId = 1;
  #pending = new Map();
  #listeners = new Map();
  #subscribers = new Map();

  constructor(webSocketUrl) {
    this.socket = new WebSocket(webSocketUrl);
  }

  async connect() {
    await new Promise((resolve, reject) => {
      this.socket.addEventListener("open", resolve, { once: true });
      this.socket.addEventListener("error", reject, { once: true });
    });
    this.socket.addEventListener("message", (event) => this.#handleMessage(event.data));
  }

  send(method, params = {}) {
    const id = this.#nextId++;
    const response = new Promise((resolve, reject) => this.#pending.set(id, { resolve, reject }));
    this.socket.send(JSON.stringify({ id, method, params }));
    return response;
  }

  waitFor(method, timeoutMilliseconds = 15000) {
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        this.#listeners.delete(method);
        reject(new Error(`Timed out waiting for ${method}.`));
      }, timeoutMilliseconds);
      this.#listeners.set(method, (params) => {
        clearTimeout(timeout);
        this.#listeners.delete(method);
        resolve(params);
      });
    });
  }

  on(method, listener) {
    const subscribers = this.#subscribers.get(method) ?? new Set();
    subscribers.add(listener);
    this.#subscribers.set(method, subscribers);
  }

  #handleMessage(data) {
    const message = JSON.parse(data);
    if (message.id) {
      const pending = this.#pending.get(message.id);
      if (!pending) {
        return;
      }

      this.#pending.delete(message.id);
      if (message.error) {
        pending.reject(new Error(message.error.message));
      } else {
        pending.resolve(message.result);
      }
      return;
    }

    this.#listeners.get(message.method)?.(message.params);
    this.#subscribers.get(message.method)?.forEach((subscriber) => subscriber(message.params));
  }
}

async function createTarget() {
  const response = await fetch(`${cdpUrl}/json/new?${encodeURIComponent(`${appUrl}/Account/Login`)}`, {
    method: "PUT",
  });
  if (!response.ok) {
    throw new Error(`Browser debugging endpoint returned ${response.status}.`);
  }

  return response.json();
}

async function navigate(client, url) {
  const loaded = client.waitFor("Page.loadEventFired");
  await client.send("Page.navigate", { url });
  await loaded;
  await new Promise((resolve) => setTimeout(resolve, 400));
}

async function setViewport(client, width, height, mobile) {
  await client.send("Emulation.setDeviceMetricsOverride", {
    width,
    height,
    deviceScaleFactor: 1,
    mobile,
    screenWidth: width,
    screenHeight: height,
  });
}

async function capture(client, filename) {
  const screenshot = await client.send("Page.captureScreenshot", {
    format: "png",
    fromSurface: true,
    captureBeyondViewport: true,
  });
  await writeFile(path.join(outputDirectory, filename), Buffer.from(screenshot.data, "base64"));
}

await mkdir(outputDirectory, { recursive: true });
const target = await createTarget();
const client = new DevToolsClient(target.webSocketDebuggerUrl);
async function closeBrowserAndThrow(message) {
  await client.send("Browser.close").catch(() => {});
  throw new Error(message);
}

await client.connect();
await client.send("Page.enable");
await client.send("Runtime.enable");
await client.send("Log.enable");

const browserErrors = [];
client.on("Runtime.exceptionThrown", ({ exceptionDetails }) => {
  browserErrors.push(exceptionDetails.exception?.description ?? exceptionDetails.text);
});
client.on("Log.entryAdded", ({ entry }) => {
  if (entry.level === "error") {
    browserErrors.push(entry.text);
  }
});

await setViewport(client, 1440, 1000, false);
await navigate(client, `${appUrl}/Account/Login`);
await capture(client, "login-desktop.png");

const signedIn = client.waitFor("Page.loadEventFired");
await client.send("Runtime.evaluate", {
  expression: `(() => {
    document.querySelector("#Email").value = "manager@documents.demo";
    document.querySelector("#Password").value = "PortfolioDemo123!";
    document.querySelector("form[action$='/Account/Login']").requestSubmit();
  })()`,
});
await signedIn;
await new Promise((resolve) => setTimeout(resolve, 500));
const location = await client.send("Runtime.evaluate", {
  expression: "window.location.pathname",
  returnByValue: true,
});
if (location.result.value === "/Account/Login") {
  await closeBrowserAndThrow("Demo sign-in did not leave the login page.");
}
await capture(client, "dashboard-desktop.png");

await setViewport(client, 390, 844, true);
await navigate(client, `${appUrl}/Documents`);
await capture(client, "documents-mobile.png");

if (browserErrors.length > 0) {
  await closeBrowserAndThrow(`Browser console errors:\n${browserErrors.join("\n")}`);
}

await client.send("Browser.close").catch(() => {});
