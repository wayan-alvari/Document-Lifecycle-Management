import { copyFile, mkdir, rm } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const vendorRoot = resolve(
  repositoryRoot,
  "src",
  "DocumentLifecycle.Web",
  "wwwroot",
  "vendor",
);

const assets = [
  ["node_modules/bootstrap/dist/css/bootstrap.min.css", "bootstrap/bootstrap.min.css"],
  ["node_modules/bootstrap/dist/js/bootstrap.bundle.min.js", "bootstrap/bootstrap.bundle.min.js"],
  ["node_modules/admin-lte/dist/css/adminlte.min.css", "adminlte/adminlte.min.css"],
  ["node_modules/admin-lte/dist/js/adminlte.min.js", "adminlte/adminlte.min.js"],
];

await rm(vendorRoot, { force: true, recursive: true });

for (const [source, destination] of assets) {
  const outputPath = resolve(vendorRoot, destination);
  await mkdir(dirname(outputPath), { recursive: true });
  await copyFile(resolve(repositoryRoot, source), outputPath);
}

console.log(`Copied ${assets.length} production vendor assets.`);
