import { cp, mkdir, rm } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const vendorRoot = resolve(repositoryRoot, "src/DeviceRental.Web/wwwroot/vendor");
const assets = [
  ["node_modules/htmx.org/dist/htmx.min.js", "htmx/htmx.min.js"],
  ["node_modules/htmx.org/LICENSE", "htmx/LICENSE"],
  ["node_modules/lucide/dist/umd/lucide.min.js", "lucide/lucide.min.js"],
  ["node_modules/lucide/LICENSE", "lucide/LICENSE"],
];

await rm(vendorRoot, { recursive: true, force: true });
for (const [source, destination] of assets) {
  const output = resolve(vendorRoot, destination);
  await mkdir(dirname(output), { recursive: true });
  await cp(resolve(repositoryRoot, source), output);
}
